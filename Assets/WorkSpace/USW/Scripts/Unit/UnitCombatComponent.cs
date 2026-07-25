using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class UnitCombatComponent : MonoBehaviour
{
    private UnitBase _unit;
    private UnitDependencies _deps;
    private UnitStatsModifier _stats;
    private UnitResourceComponent _resource;
    
    private CancellationTokenSource _loopCts;
    private bool _paused = false;

    private float _attackTimer;
    private float _skillTimer;
    private int _hitCount;

    public float SkillTimer
    {
        get => _skillTimer;
        set => _skillTimer = value;
    }

    /// <summary>CanAutoSkill이 꺼져있는 유닛(예: TestUnit)도 스킬을 즉시 발동시켜볼 수 있도록,
    /// 자동 루프의 타이머/쿨타임 체크를 거치지 않고 바로 ExecuteSkill을 호출한다.</summary>
    public void TriggerSkillManually() => ExecuteSkill();

    public void Init(UnitBase unit, UnitDependencies deps, UnitStatsModifier stats, UnitResourceComponent resource)
    {
        _unit = unit;
        _deps = deps;
        _stats = stats;
        _resource = resource;
    }

    // 공격/스킬 타이머는 매 Update마다 실시간으로 누적합니다. UnitControlLoopAsync는 공격/스킬
    // 애니메이션 재생 동안 다음 루프 반복으로 넘어가지 못해 값을 여기서 관리하지 않으면 그 시간만큼
    // 게이지가 멈춰 보였다가 한 번에 점프하는 문제(예: 족장 스킬 쿨타임 UI)가 생깁니다.
    private void Update()
    {
        if (_paused || (_unit.currentCell != null && _unit.currentCell.Model.IsSealed)) return;

        _attackTimer += Time.deltaTime;
        _skillTimer += Time.deltaTime;
    }

    public void StartLoops()
    {
        if (_unit.IsFirstPlacement)
        {
            _attackTimer = _stats.GetCurrentAttackInterval();
            _skillTimer = 0f;
        }

        StopLoops();
        _paused = false;
        _loopCts = new CancellationTokenSource();
        UnitControlLoopAsync(_loopCts.Token).Forget(e => { if (e is not System.OperationCanceledException) UnityEngine.Debug.LogException(e); });
    }

    public void StopLoops()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
    }

    public void PauseLoops() => _paused = true;

    public void ResumeLoops()
    {
        if (_deps?.CurrencyManager == null) return;
        _paused = false;
        if (_loopCts == null)
        {
            _loopCts = new CancellationTokenSource();
            UnitControlLoopAsync(_loopCts.Token).Forget(e => { if (e is not System.OperationCanceledException) UnityEngine.Debug.LogException(e); });
        }
    }

    private BossBase LiveBoss => _deps?.BossManager?.CurrentBoss ?? _unit.Boss;

    private async UniTask UnitControlLoopAsync(CancellationToken token)
    {
        if (_unit.unitData == null) return;

        float lastUpdateTime = Time.time;

        while (!token.IsCancellationRequested)
        {
            float currentTime = Time.time;
            float dt = currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;

            if (_paused || (_unit.currentCell != null && _unit.currentCell.Model.IsSealed))
            {
                if (_unit.CurrentState != UnitBase.UnitState.Idle && _unit.CurrentState != UnitBase.UnitState.Sealed)
                {
                    _unit.SetState(_paused ? UnitBase.UnitState.Idle : UnitBase.UnitState.Sealed);
                    _unit.animator?.PlayIdle();
                }
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                    return;
                continue;
            }

            _resource.TickFoodProduction(dt);

            float attackInterval = _stats.GetCurrentAttackInterval();
            float skillInterval = _stats.GetCurrentSkillInterval();
            
            bool canAttack = _unit.currentCell != null && !_unit.currentCell.Model.IsAttackDisabled && !_unit.currentCell.Model.TotemAttackDisabled && LiveBoss != null && !LiveBoss.IsDead;

            if (_unit.CanAutoSkill && _skillTimer >= skillInterval && canAttack)
            {
                _unit.SetState(UnitBase.UnitState.Skilling);
                _skillTimer = 0f;
                
                ExecuteSkill();
                
                if (_unit.animator != null) 
                {
                    _unit.animator.SetSpeed(1f);
                    await _unit.animator.PlaySkillAsync(token);
                }
            }
            else if (_unit.CanBasicAttack && _attackTimer >= attackInterval && canAttack)
            {
                _unit.SetState(UnitBase.UnitState.Attacking);
                _attackTimer = 0f;
                
                ExecuteAttack();
                
                if (_unit.animator != null) 
                {
                    await _unit.animator.PlayAttackAsync(token, attackInterval);
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(attackInterval), cancellationToken: token);
                }
            }
            else
            {
                if (_unit.CurrentState != UnitBase.UnitState.Idle)
                {
                    _unit.SetState(UnitBase.UnitState.Idle);
                    if (_unit.animator != null) _unit.animator.PlayIdle();
                }
                
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                    return;
            }
        }
    }

    private void ExecuteAttack()
    {
        if (_unit.unitData == null || _unit.unitData.atk <= 0) return;

        bool attackDisabled = _unit.currentCell != null &&
            (_unit.currentCell.Model.IsAttackDisabled || _unit.currentCell.Model.TotemAttackDisabled);

        var boss = LiveBoss;
        if (!attackDisabled && boss != null && !boss.IsDead)
        {
            var attackData = _unit.unitData.basicAttackData;
            var attackAction = attackData?.action;
            if (attackAction != null)
            {
                attackData.castEffect?.Play(transform.position, transform, _deps?.AudioManager);
                attackAction.Execute(_unit, this, attackData.hitEffects, attackData.additionalEffects);
            }
            else
            {
                LaunchProjectile(_stats.GetAttackDamage());
            }

            _unit.InvokeOnAttack();
            _hitCount++;
            TriggerBonusAttacks(attackDisabled);
        }
    }

    private void ExecuteSkill()
    {
        _unit.InvokeOnSkillFull();

        bool attackDisabled = _unit.currentCell != null &&
            (_unit.currentCell.Model.IsAttackDisabled || _unit.currentCell.Model.TotemAttackDisabled);

        var boss = LiveBoss;
        if (!attackDisabled && boss != null && !boss.IsDead)
        {
            var skillData = _unit.unitData?.skillData;
            skillData?.castEffect?.Play(transform.position, transform, _deps?.AudioManager);

            var skillAction = skillData?.action;
            skillAction?.Execute(_unit, this, skillData.hitEffects, skillData.additionalEffects);
        }

        var lu = _deps?.LevelUpManager;
        if (lu == null) return;

        if (lu.HasBurstOnSkillFull)
            _stats.BurstEndTime = Time.time + lu.BurstDurationSeconds;

        if (lu.HasExtraAttackOnSkillFull && !attackDisabled && boss != null && !boss.IsDead)
        {
            LaunchProjectile(_stats.GetAttackDamage());
        }
    }

    private void TriggerBonusAttacks(bool attackDisabled)
    {
        var boss = LiveBoss;
        if (attackDisabled || boss == null || boss.IsDead) return;

        var lu = _deps?.LevelUpManager;
        if (lu == null) return;

        foreach (int n in lu.BonusAttackEveryNHits)
        {
            if (n > 0 && _hitCount % n == 0)
            {
                LaunchProjectile(_stats.GetAttackDamage());
            }
        }

        if (lu.RandomExtraAttackChance > 0f && UnityEngine.Random.value < lu.RandomExtraAttackChance)
        {
            LaunchProjectile(_stats.GetAttackDamage());
        }

        if (lu.HasRandomProcAttack && UnityEngine.Random.value < lu.RandomProcChance)
        {
            LaunchProjectile(Mathf.RoundToInt(_stats.GetAttackDamage() * lu.RandomProcDamagePct));
        }

        if (lu.HasExtraAttackEveryAttack)
        {
            LaunchProjectile(_stats.GetAttackDamage());
        }
    }

    /// <summary>sizeMultiplier: 이 발사 1회에만 적용되는 추가 투사체 크기 배율(예: 스킬 데이터의 투사체 크기 증가치). 기본 1(변화 없음).
    /// projectileData: 이 발사에만 쓸 투사체 구성(이동/이펙트/프리팹). null이면 ProjectilePool의 기본 구성을 사용한다.</summary>
    public void LaunchProjectile(int damage, float sizeMultiplier = 1f, ProjectileData projectileData = null)
        => LaunchProjectileInternal(sizeMultiplier, projectileData, (boss, targetPos) => boss.TakeDamage(damage, targetPos));

    /// <summary>적중 시 결과를 hitEffects에, 부가 연출을 additionalEffects에 위임하는 발사(데미지 외의 효과도 가능).</summary>
    public void LaunchProjectile(List<IEffect> hitEffects, List<IAdditionalEffect> additionalEffects, float sizeMultiplier = 1f, ProjectileData projectileData = null)
        => LaunchProjectileInternal(sizeMultiplier, projectileData, (boss, targetPos) =>
        {
            if (hitEffects != null)
                foreach (var effect in hitEffects)
                    effect?.Apply(_unit, boss, targetPos);

            if (additionalEffects != null)
                foreach (var effect in additionalEffects)
                    effect?.Apply(_unit, boss, targetPos);
        });

    private void LaunchProjectileInternal(float sizeMultiplier, ProjectileData projectileData, Action<BossBase, Vector3> onHitApply)
    {
        var boss = LiveBoss;
        var bossArea = boss?.GetComponent<BossAreaTarget>();

        if (gameObject == null) return;

        if (boss != null && !boss.IsDead && _deps?.ProjectileManager != null)
        {
            // 발사 시작 위치를 유닛의 중심(발밑 + 0.5f)으로 조정
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            Vector3 targetPos = bossArea != null ? bossArea.GetRandomWorldPosition() : boss.transform.position;

            Action onHit = () =>
            {
                if (boss != null && !boss.IsDead)
                {
                    onHitApply(boss, targetPos);
                }
            };

            if (projectileData != null)
                _deps.ProjectileManager.Launch(spawnPos, targetPos, projectileData, onHit, _unit, sizeMultiplier);
            else
                _deps.ProjectileManager.Launch(spawnPos, targetPos, onHit, _unit, sizeMultiplier);
        }
    }

    private void OnDestroy()
    {
        StopLoops();
    }
}
