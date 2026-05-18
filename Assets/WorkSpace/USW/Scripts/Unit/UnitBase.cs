using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 모든 유닛의 기반 클래스
///
/// 루프 구조:
///   AttackLoopAsync — 1초마다 일반 공격 (atk), 공속 버프 적용
///   SkillLoopAsync  — skillCooldown마다 스킬 공격 (skillAtk) + 식량 생산, 게이지속도 버프 적용
///
/// 서브클래스 훅:
///   OnUnitPlaced()  — 배치 시 추가 처리
///   OnUnitRemoved() — 제거 시 추가 처리
///   OnSkillFull()   — 스킬 발동 시 추가 처리 (식량 생산 로직 오버라이드 가능)
/// </summary>
public abstract class UnitBase : MonoBehaviour
{
    public UnitData unitData;

    public UnityEvent onSkillFull; // 스킬 발동 시 VFX/애니메이션 훅
    public UnityEvent onAttack;    // 일반 공격 시 VFX 훅

    /// <summary>배치/제거 시 전역 알림 — TotemProximityBuff 등에서 구독</summary>
    public static event Action OnAnyUnitChanged;

    protected CurrencyManager _currency;
    protected BossBase        _boss;
    public GridCell currentCell { get; private set; }

    protected UnitAnimator _animator;
    protected UnitSoundController _sound;
    private CancellationTokenSource _loopCts;
    private bool _paused = false;

    // ── 상태 및 타이머 (관찰 가능) ──────────────────────────────
    public enum UnitState { Idle, Attacking, Skilling, Sealed }
    public UnitState CurrentState { get; private set; } = UnitState.Idle;

    private float _attackTimer;
    private float _skillTimer;

    public float SkillGaugeProgress 
    {
        get 
        {
            if (unitData == null) return 0f;
            float interval = GetCurrentSkillInterval();
            return Mathf.Clamp01(_skillTimer / interval);
        }
    }

    // ── 레벨업 특수 효과용 ─────────────────────────────────────
    private int   _hitCount;                      // 공격 횟수 (AttackEveryNHits용)
    private float _burstEndTime;                  // 버스트 버프 종료 시각 (Time.time 기준)
    protected float _unemployedAtkBonus = 0f;     // 3056 식충이 — 스킬마다 누적 공격력

    protected virtual void Awake()
    {
        _animator = GetComponentInChildren<UnitAnimator>();
    }

    // ── 배치/제거 ──────────────────────────────────────────────

    /// <summary>UnitSpawner 또는 DragHandler.PlaceSelfAt() 이 셀에 배치한 뒤 호출</summary>
    public void OnPlaced(CurrencyManager currency, BossBase boss, GridCell cell = null)
    {
        if (currency == null)
        {
            Debug.LogError($"UnitBase({name}): CurrencyManager가 null입니다.");
            return;
        }

        _currency    = currency;
        _boss        = boss;
        currentCell = cell;

        StopLoops();
        _paused = false;
        _loopCts = new CancellationTokenSource();
        UnitControlLoopAsync(_loopCts.Token).Forget(Debug.LogException);

        OnUnitPlaced();
        Manager.Population?.Add(unitData?.populationCost ?? 1);
        OnAnyUnitChanged?.Invoke();

        _sound = new(this);
        _animator.Initialize(this);
    }

    /// <summary>하위 호환 오버로드 — 셀 참조 없이 호출하는 기존 코드 지원</summary>
    public void OnPlaced(CurrencyManager currency, BossBase boss)
        => OnPlaced(currency, boss, null);

    /// <summary>셀 제거 또는 게임 종료 시 호출</summary>
    public void OnRemoved()
    {
        OnUnitRemoved();
        StopLoops();
        Manager.Population?.Remove(unitData?.populationCost ?? 1);
        currentCell = null;
        OnAnyUnitChanged?.Invoke();
    }

    protected virtual void OnUnitPlaced()  { }
    protected virtual void OnUnitRemoved() { }

    private void OnDestroy() => StopLoops();

    /// <summary>인구수·셀 상태 변경 없이 공격/스킬을 일시 중단. LevelUpUI 등 전용.</summary>
    public void PauseLoops() => _paused = true;

    /// <summary>PauseLoops 이후 루프 재개. 셀·재화·보스 참조는 기존 값 유지.</summary>
    public void ResumeLoops()
    {
        if (_currency == null) return;
        _paused = false;
        if (_loopCts == null)
        {
            _loopCts = new CancellationTokenSource();
            UnitControlLoopAsync(_loopCts.Token).Forget(Debug.LogException);
        }
    }

    private void StopLoops()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
    }

    // ── 통합 제어 루프 ──────────────────────────────────────────

    /// <summary>
    /// 유닛의 행동을 제어하는 메인 루프.
    /// Idle 상태에서 스킬 게이지(쿨다운)와 공격 쿨다운을 확인하여 우선순위에 따라 행동을 결정합니다.
    /// </summary>
    private async UniTask UnitControlLoopAsync(CancellationToken token)
    {
        if (unitData == null) return;

        _attackTimer = GetCurrentAttackInterval(); // 첫 공격은 즉시 혹은 짧은 대기 후 가능하도록 설정
        _skillTimer = 0f;

        // 비용 최적화: try-catch 대신 cancellationToken 상태 직접 체크
        while (!token.IsCancellationRequested)
        {
            if (_paused)
            {
                if (CurrentState != UnitState.Idle)
                {
                    CurrentState = UnitState.Idle;
                    _animator?.PlayIdle();
                }
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                    return;
                continue;
            }

            if (currentCell != null && currentCell.Model.IsSealed)
            {
                CurrentState = UnitState.Sealed;
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                    return;
                continue;
            }

            // UI 대응: 게임 일시정지(Time.timeScale=0)에 영향을 받지 않도록 unscaledDeltaTime 사용
            float dt = Time.unscaledDeltaTime;
            _attackTimer += dt;
            _skillTimer += dt;

            float attackInterval = GetCurrentAttackInterval();
            float skillInterval = GetCurrentSkillInterval();

            // 우선순위 결정: Skill > Attack > Idle
            if (_skillTimer >= skillInterval)
            {
                CurrentState = UnitState.Skilling;
                if (_animator != null) await _animator.PlaySkillAsync(token);
                ExecuteSkill();
                
                _skillTimer = 0f;
            }
            else if (_attackTimer >= attackInterval)
            {
                CurrentState = UnitState.Attacking;
                if (_animator != null) await _animator.PlayAttackAsync(token);
                ExecuteAttack();
                
                _attackTimer = 0f;
            }
            else
            {
                CurrentState = UnitState.Idle;
                if (_animator != null) _animator.PlayIdle();
                
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                    return;
            }
        }
    }

    private float GetCurrentAttackInterval()
    {
        int row = currentCell?.GridPosition.y ?? 0;
        float rowSpeedMult = Mathf.Max(Manager.LevelUp?.GetRowSpeedMultiplier(row) ?? 1f, 0.01f);
        float interval = UpgradedAttackInterval
                       * Manager.Buff.SpeedMultiplier
                       * (currentCell?.Model.SpeedModifier ?? 1f)
                       / rowSpeedMult;
        return Mathf.Max(interval, 0.05f);
    }

    private float GetCurrentSkillInterval()
    {
        int row = currentCell?.GridPosition.y ?? 0;
        float rowSpeedMult = Mathf.Max(Manager.LevelUp?.GetRowSpeedMultiplier(row) ?? 1f, 0.01f);
        float interval = unitData.skillCooldown
                       * Manager.Buff.GaugeSpeedMultiplier
                       * Manager.Buff.FoodSpeedMultiplier
                       * (currentCell?.Model.SpeedModifier ?? 1f)
                       / rowSpeedMult;
        return Mathf.Max(interval, 0.05f);
    }

    private void ExecuteAttack()
    {
        bool attackDisabled = currentCell != null &&
            (currentCell.Model.IsAttackDisabled || currentCell.Model.TotemAttackDisabled);

        if (!attackDisabled && _boss != null && !_boss.IsDead)
        {
            LaunchProjectile();
            _boss.TakeDamage(GetAttackDamage());
            onAttack?.Invoke();
            _hitCount++;
            TriggerBonusAttacks(attackDisabled);
        }
    }

    private void ExecuteSkill()
    {
        OnSkillFull();
    }

    // 기존 AttackLoopAsync와 SkillLoopAsync는 제거됨


    /// <summary>스킬 발동 — 식량 생산 + 스킬 공격. 서브클래스에서 오버라이드 가능</summary>
    protected virtual void OnSkillFull()
    {
        onSkillFull?.Invoke();

        // 재화 생산량: 시트 currency_per_second × skillCooldown. 미로드 시 SO foodPerTick 폴백
        float foodAmount = Manager.GameData != null && Manager.GameData.IsLoaded
            ? Manager.GameData.GetCurrencyPerSecond(unitData.characterId) * unitData.skillCooldown
            : unitData.foodPerTick;
        _currency.AddCurrency(foodAmount * Manager.Buff.FoodAmountMultiplier);

        bool attackDisabled = currentCell != null &&
            (currentCell.Model.IsAttackDisabled || currentCell.Model.TotemAttackDisabled);

        if (!attackDisabled && _boss != null && !_boss.IsDead)
        {
            LaunchProjectile();
            _boss.TakeDamage(GetSkillDamage());
        }

        // ── 레벨업 스킬 특수 효과 ─────────────────────────────
        var lu = Manager.LevelUp;
        if (lu == null) return;

        if (lu.HasBurstOnSkillFull)
            _burstEndTime = Time.time + lu.BurstDurationSeconds;

        if (lu.HasExtraAttackOnSkillFull && !attackDisabled && _boss != null && !_boss.IsDead)
        {
            LaunchProjectile();
            _boss.TakeDamage(GetAttackDamage());
        }
    }

    // ── 데미지 계산 ────────────────────────────────────────────

    // 강화 데이터 로드 전에는 SO 기본값으로 폴백
    private float UpgradedAtk => Manager.Upgrade != null && Manager.Upgrade.IsLoaded
        ? Manager.Upgrade.GetCurrentAtk(unitData.characterId)
        : unitData.atk;

    private float UpgradedAttackInterval => Manager.Upgrade != null && Manager.Upgrade.IsLoaded
        ? Manager.Upgrade.GetCurrentAttackSpeed(unitData.characterId)
        : 1.0f;

    public int GetAttackDamage() => ComputeDamage(UpgradedAtk);
    public int GetSkillDamage()  => ComputeDamage(unitData.skillAtk);

    private int ComputeDamage(float baseDamage)
    {
        if (unitData == null) return 0;

        float cellModifier  = currentCell != null
            ? (currentCell.Model.NullifyDamageDebuff ? 1f : currentCell.Model.DamageModifier)
            : 1f;
        float totemModifier = currentCell?.Model.TotemAttackModifier ?? 1f;
        int   row           = currentCell?.GridPosition.y ?? 0;
        var   lu            = Manager.LevelUp;
        float rowModifier   = lu?.GetRowAttackMultiplier(row) ?? 1f;

        // 부족별 공격력 보너스
        float tribeAtk     = 1f + (lu?.GetTribeAtkBonus(unitData.unitTribe) ?? 0f);

        // 투사체 크기 → 공격력 보너스
        float projAtk      = 1f + (lu?.GetProjectileSizeAtkBonus() ?? 0f);

        // 버스트 버프 (스킬 풀 시 일시적)
        float burstAtk     = (Time.time < _burstEndTime && lu != null)
            ? (1f + lu.BurstAttackBonus)
            : 1f;

        // 족장 전용 공격력 버프 (3008 위엄)
        float chieftainAtk = (Manager.Chieftain?.ChieftainUnit == this)
            ? 1f + (lu?.ChieftainAttackBonus ?? 0f)
            : 1f;

        float damage = (baseDamage + _unemployedAtkBonus)
                     * Manager.Buff.AttackMultiplier
                     * cellModifier
                     * totemModifier
                     * rowModifier
                     * tribeAtk
                     * projAtk
                     * burstAtk
                     * chieftainAtk;

        float critChance = (lu?.CritChance ?? 0f) + Manager.Buff.CritChanceBonus;
        if (critChance > 0f && UnityEngine.Random.value < critChance)
        {
            float critMult = (lu?.CritDamageMultiplier ?? 1.5f) + Manager.Buff.CritDamageBonus;
            damage *= critMult;
        }

        return Mathf.RoundToInt(damage);
    }

    // ── 보너스 공격 (레벨업 특수 효과) ───────────────────────

    private void TriggerBonusAttacks(bool attackDisabled)
    {
        if (attackDisabled || _boss == null || _boss.IsDead) return;

        var lu = Manager.LevelUp;
        if (lu == null) return;

        // N회마다 추가 공격 (연속 공격, 정밀 연타, 폭풍 연격)
        foreach (int n in lu.BonusAttackEveryNHits)
        {
            if (n > 0 && _hitCount % n == 0)
            {
                LaunchProjectile();
                _boss.TakeDamage(GetAttackDamage());
            }
        }

        // 30% 확률 추가 공격 (연쇄 타격)
        if (lu.RandomExtraAttackChance > 0f && UnityEngine.Random.value < lu.RandomExtraAttackChance)
        {
            LaunchProjectile();
            _boss.TakeDamage(GetAttackDamage());
        }

        // 5% 확률 50% 데미지 (변칙 타격)
        if (lu.HasRandomProcAttack && UnityEngine.Random.value < lu.RandomProcChance)
        {
            LaunchProjectile();
            _boss.TakeDamage(Mathf.RoundToInt(GetAttackDamage() * lu.RandomProcDamagePct));
        }

        // 매 공격마다 추가 공격 (양손잡이)
        if (lu.HasExtraAttackEveryAttack)
        {
            LaunchProjectile();
            _boss.TakeDamage(GetAttackDamage());
        }
    }

    // ── 투사체 ─────────────────────────────────────────────────

    protected void LaunchProjectile()
    {
        var bossArea = Manager.Boss?.CurrentBoss?.GetComponent<BossAreaTarget>();
        if (bossArea != null && Manager.Projectile != null)
            Manager.Projectile.Launch(transform.position, bossArea.GetRandomWorldPosition());
    }
}
