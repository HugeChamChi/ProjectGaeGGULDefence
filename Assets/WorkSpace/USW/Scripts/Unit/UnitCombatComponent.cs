using System;
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

    public void Init(UnitBase unit, UnitDependencies deps, UnitStatsModifier stats, UnitResourceComponent resource)
    {
        _unit = unit;
        _deps = deps;
        _stats = stats;
        _resource = resource;
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

            _attackTimer += dt;
            _skillTimer += dt;
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
            LaunchProjectile(_stats.GetAttackDamage());
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
            LaunchProjectile(_stats.GetSkillDamage());
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

    public void LaunchProjectile(int damage)
    {
        var boss = LiveBoss;
        var bossArea = boss?.GetComponent<BossAreaTarget>();

        if (gameObject == null) return;

        if (bossArea != null && _deps?.ProjectileManager != null)
        {
            // 발사 시작 위치를 유닛의 중심(발밑 + 0.5f)으로 조정
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            
            _deps.ProjectileManager.Launch(spawnPos, bossArea.GetRandomWorldPosition(), () => 
            {
                if (boss != null && !boss.IsDead)
                {
                    boss.TakeDamage(damage);
                }
            });
        }
    }

    private void OnDestroy()
    {
        StopLoops();
    }
}
