using System;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
using System.Threading;

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

    private CancellationTokenSource _loopCts;

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
        _loopCts = new CancellationTokenSource();
        AttackLoopAsync(_loopCts.Token).Forget(Debug.LogException);
        SkillLoopAsync(_loopCts.Token).Forget(Debug.LogException);

        OnUnitPlaced();
        Manager.Population?.Add(unitData?.populationCost ?? 1);
        OnAnyUnitChanged?.Invoke();
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

    private void StopLoops()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
    }

    // ── 일반 공격 루프 — 1초마다, 공속 버프 적용 ──────────────

    private async UniTask AttackLoopAsync(CancellationToken token)
    {
        if (unitData == null)
        {
            Debug.LogError($"UnitBase({name}): unitData가 null입니다.");
            return;
        }

        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (currentCell != null && currentCell.Model.IsSealed)
                {
                    await UniTask.Yield(token);
                    continue;
                }

                int   row          = currentCell?.GridPosition.y ?? 0;
                float rowSpeedMult = Mathf.Max(Manager.LevelUp?.GetRowSpeedMultiplier(row) ?? 1f, 0.01f);

                float interval = UpgradedAttackInterval
                               * Manager.Buff.SpeedMultiplier
                               * (currentCell?.Model.SpeedModifier ?? 1f)
                               / rowSpeedMult;
                interval = Mathf.Max(interval, 0.05f);

                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                token.ThrowIfCancellationRequested();

                bool attackDisabled = currentCell != null &&
                    (currentCell.Model.IsAttackDisabled || currentCell.Model.TotemAttackDisabled);

                if (!attackDisabled && _boss != null && !_boss.IsDead)
                {
                    LaunchProjectile();
                    _boss.TakeDamage(GetAttackDamage());
                    onAttack?.Invoke();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── 스킬 루프 — skillCooldown마다, 게이지속도 버프 적용 ────

    private async UniTask SkillLoopAsync(CancellationToken token)
    {
        if (unitData == null) return;

        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (currentCell != null && currentCell.Model.IsSealed)
                {
                    await UniTask.Yield(token);
                    continue;
                }

                int   row          = currentCell?.GridPosition.y ?? 0;
                float rowSpeedMult = Mathf.Max(Manager.LevelUp?.GetRowSpeedMultiplier(row) ?? 1f, 0.01f);

                float interval = unitData.skillCooldown
                               * Manager.Buff.GaugeSpeedMultiplier
                               * Manager.Buff.FoodSpeedMultiplier
                               * (currentCell?.Model.SpeedModifier ?? 1f)
                               / rowSpeedMult;
                interval = Mathf.Max(interval, 0.05f);

                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                token.ThrowIfCancellationRequested();

                OnSkillFull();
            }
        }
        catch (OperationCanceledException) { }
    }

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
        float rowModifier   = Manager.LevelUp?.GetRowAttackMultiplier(row) ?? 1f;

        float damage = baseDamage
                     * Manager.Buff.AttackMultiplier
                     * cellModifier
                     * totemModifier
                     * rowModifier;

        float critChance = (Manager.LevelUp?.CritChance ?? 0f) + Manager.Buff.CritChanceBonus;
        if (critChance > 0f && UnityEngine.Random.value < critChance)
        {
            float critMult = (Manager.LevelUp?.CritDamageMultiplier ?? 1.5f) + Manager.Buff.CritDamageBonus;
            damage *= critMult;
        }

        return Mathf.RoundToInt(damage);
    }

    // ── 투사체 ─────────────────────────────────────────────────

    private void LaunchProjectile()
    {
        var bossArea = Manager.Boss?.CurrentBoss?.GetComponent<BossAreaTarget>();
        if (bossArea != null && Manager.Projectile != null)
            Manager.Projectile.Launch(transform.position, bossArea.GetRandomWorldPosition());
    }
}
