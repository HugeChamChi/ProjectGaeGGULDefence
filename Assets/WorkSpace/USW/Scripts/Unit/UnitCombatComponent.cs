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

    /// <summary>실제 일반 공격 시작 시에만 발행된다. 그림자/스킬/보너스 발사는 발행하지 않는다.</summary>
    public event Action<BasicAttackReplay> OnBasicAttackStarted;
    /// <summary>동기 액션이 시작한 일반 공격 문맥. 연발 액션은 이를 캡처해 각 발사로 전달한다.</summary>
    public BasicAttackReplay CurrentBasicAttack { get; private set; }

    /// <summary>소환 드론의 일반 공격을 본체 셀의 디버프/그림자 구독에 전달한다. 본체 공격이나 스킬은 실행하지 않는다.</summary>
    public BasicAttackReplay BeginDroneBasicAttack(BossBase target, Transform visualSource, Func<bool> isSourceValid)
    {
        if (_unit == null || _unit.IsStunned || target == null || target.IsDead) return null;
        _deps?.TotemBuffManager?.ApplyAttackDebuff(_unit.currentCell, target);
        if (OnBasicAttackStarted == null) return null;
        var replay = new BasicAttackReplay(_unit, target, visualSource, isSourceValid);
        OnBasicAttackStarted.Invoke(replay);
        return replay;
    }

    public float SkillTimer
    {
        get => _skillTimer;
        set => _skillTimer = _unit != null && !_unit.CanUseSkill ? 0f : value;
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
        if (_unit == null || _paused || (_unit.currentCell != null && _unit.currentCell.Model.IsSealed)) return;

        if (_unit.CanBasicAttack) _attackTimer += Time.deltaTime;
        if (_unit.CanUseSkill) _skillTimer += Time.deltaTime;
        // 공격 애니메이션 대기 시간과 무관하게 생산을 진행한다. 스턴 시간은 누적하지 않는다.
        if (_loopCts != null && _unit.currentCell != null && !_unit.IsStunned)
            _resource?.TickFoodProduction(Time.deltaTime);
    }

    public void StartLoops()
    {
        if (_unit.IsFirstPlacement)
        {
            _attackTimer = _unit.CanBasicAttack ? _stats.GetCurrentAttackInterval() : 0f;
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

        while (!token.IsCancellationRequested)
        {
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

            if (_unit.IsStunned)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
                continue;
            }

            if (!_unit.CanBasicAttack && !_unit.CanUseSkill)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                    return;
                continue;
            }

            float attackInterval = _stats.GetCurrentAttackInterval();
            float skillInterval = _stats.GetCurrentSkillInterval();
            
            bool canAttack = _unit.currentCell != null && !_unit.currentCell.Model.IsAttackDisabled && !_unit.currentCell.Model.TotemAttackDisabled && LiveBoss != null && !LiveBoss.IsDead;

            if (_unit.CanUseSkill && _unit.CanAutoSkill && _skillTimer >= skillInterval && canAttack)
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
        if (_unit == null || _unit.IsStunned || !_unit.CanBasicAttack || _unit.unitData == null || _unit.unitData.atk.Get(_unit.currentTier) <= 0) return;

        bool attackDisabled = _unit.currentCell != null &&
            (_unit.currentCell.Model.IsAttackDisabled || _unit.currentCell.Model.TotemAttackDisabled);

        var boss = LiveBoss;
        if (!attackDisabled && boss != null && !boss.IsDead)
        {
            _deps?.TotemBuffManager?.ApplyAttackDebuff(_unit.currentCell, boss);
            var attackData = _unit.unitData.basicAttackData;
            var attackAction = attackData?.action;
            CurrentBasicAttack = OnBasicAttackStarted != null ? new BasicAttackReplay(_unit, boss) : null;
            try
            {
                if (CurrentBasicAttack != null) OnBasicAttackStarted?.Invoke(CurrentBasicAttack);
                if (attackAction != null)
                {
                    attackData.castEffect?.Play(transform.position, transform, _deps?.AudioManager);
                    attackAction.Execute(_unit, this, attackData.hitEffects, attackData.additionalEffects);
                }
                else
                {
                    LaunchAttackProjectile();
                }
            }
            finally { CurrentBasicAttack = null; }

            _unit.InvokeOnAttack();
            _hitCount++;
            TriggerBonusAttacks(attackDisabled);
        }
    }

    private void ExecuteSkill()
    {
        if (_unit == null || _unit.IsStunned || !_unit.CanUseSkill) return;
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
            LaunchAttackProjectile();
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
                LaunchAttackProjectile();
            }
        }

        if (lu.RandomExtraAttackChance > 0f && UnityEngine.Random.value < lu.RandomExtraAttackChance)
        {
            LaunchAttackProjectile();
        }

        if (lu.HasRandomProcAttack && UnityEngine.Random.value < lu.RandomProcChance)
        {
            LaunchAttackProjectile(lu.RandomProcDamagePct);
        }

        if (lu.HasExtraAttackEveryAttack)
        {
            LaunchAttackProjectile();
        }
    }

    /// <summary>sizeMultiplier: 이 발사 1회에만 적용되는 추가 투사체 크기 배율(예: 스킬 데이터의 투사체 크기 증가치). 기본 1(변화 없음).
    /// projectileData: 이 발사에만 쓸 투사체 구성(이동/이펙트/프리팹). null이면 ProjectilePool의 기본 구성을 사용한다.</summary>
    /// <param name="critical">표시용 — 이 피해가 치명타 추첨에 당첨됐는지.</param>
    public void LaunchProjectile(int damage, float sizeMultiplier = 1f, ProjectileData projectileData = null, bool critical = false)
    {
        var replay = CurrentBasicAttack;
        var kind = critical ? BossDamageKind.Critical : BossDamageKind.Normal;
        Action<BossBase, Vector3> hit = replay?.IsEnabled == true
            ? new RecordedDamage(damage, kind).Apply
            : (boss, targetPos) => boss.TakeDamage(damage, targetPos, kind);
        LaunchProjectileInternal(sizeMultiplier, projectileData, hit, replay);
    }

    // 기본/보너스 공격 1발 — 공격력 추첨(치명타 포함) 결과를 표시용 종류와 함께 넘긴다.
    private void LaunchAttackProjectile(float damageScale = 1f)
    {
        int damage = _stats.GetAttackDamage(out bool critical);
        if (!Mathf.Approximately(damageScale, 1f)) damage = Mathf.RoundToInt(damage * damageScale);
        LaunchProjectile(damage, critical: critical);
    }

    /// <summary>적중 시 결과를 hitEffects에, 부가 연출을 additionalEffects에 위임하는 발사(데미지 외의 효과도 가능).</summary>
    public void LaunchProjectile(List<IEffect> hitEffects, List<IAdditionalEffect> additionalEffects, float sizeMultiplier = 1f, ProjectileData projectileData = null, BasicAttackReplay replay = null)
    {
        replay ??= CurrentBasicAttack;
        if (replay?.IsEnabled == true)
        {
            LaunchProjectileInternal(sizeMultiplier, projectileData,
                new RecordedHitEffects(_unit, hitEffects, additionalEffects).Apply, replay);
            return;
        }
        LaunchProjectileInternal(sizeMultiplier, projectileData, (boss, targetPos) =>
        {
            if (hitEffects != null)
                foreach (var effect in hitEffects)
                    effect?.Apply(_unit, boss, targetPos);

            if (additionalEffects != null)
                foreach (var effect in additionalEffects)
                    effect?.Apply(_unit, boss, targetPos);
        }, replay);
    }

    private void LaunchProjectileInternal(float sizeMultiplier, ProjectileData projectileData, Action<BossBase, Vector3> onHitApply, BasicAttackReplay replay = null)
    {
        var boss = replay?.IsEnabled == true ? replay.Target : LiveBoss;
        var bossArea = boss?.GetComponent<BossAreaTarget>();

        if (gameObject == null) return;

        if (boss != null && !boss.IsDead && _deps?.ProjectileManager != null)
        {
            // 발사 시작 위치를 유닛의 중심(발밑 + 0.5f)으로 조정
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            Vector3 targetPos = bossArea != null ? bossArea.GetRandomWorldPosition() : boss.transform.position;

            bool originalHit = false;
            bool shadowArrivedEarly = false;
            Action applyHit = () =>
            {
                if (boss != null && !boss.IsDead)
                {
                    onHitApply(boss, targetPos);
                }
            };
            Action onHit = () =>
            {
                applyHit();
                originalHit = true;
                if (shadowArrivedEarly && replay?.CanReplay == true) applyHit();
            };
            Action shadowHit = () =>
            {
                // 이동 방식이 달라 그림자가 먼저 도착해도 원본 적중 결과를 기다린다.
                if (originalHit) applyHit();
                else shadowArrivedEarly = true;
            };

            if (projectileData != null)
                _deps.ProjectileManager.Launch(spawnPos, targetPos, projectileData, onHit, _unit, sizeMultiplier);
            else
                _deps.ProjectileManager.Launch(spawnPos, targetPos, onHit, _unit, sizeMultiplier);

            replay?.RecordShot(_deps.ProjectileManager, spawnPos, targetPos, projectileData, sizeMultiplier, shadowHit);
        }
    }

    private void OnDestroy()
    {
        StopLoops();
    }
}
