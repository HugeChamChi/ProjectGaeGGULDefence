using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// 레벨업 선택지 풀 관리 · 효과 적용 · 특수 플래그 보관
///
/// 다른 시스템이 참조하는 프로퍼티
///   UnitBase  : GetRowAttackMultiplier, GetRowSpeedMultiplier,
///               CritChance, CritDamageMultiplier, ExpGainMultiplier,
///               GetTribeAtkBonus, GetTribeSpeedBonus, GetWizardCooldownBonus,
///               HasBurstOnSkillFull, BurstAttackBonus, BurstDurationSeconds,
///               BonusAttackEveryNHits, RandomExtraAttackChance,
///               HasRandomProcAttack, RandomProcChance, RandomProcDamagePct,
///               HasExtraAttackEveryAttack, HasExtraAttackOnSkillFull,
///               HasWizardLightningMode, HasWizardPhysicalMode,
///               HasUnemployedFoodNegate, UnemployedSkillAtkGain,
///               ProjectileSizeAtkPerUnit
///   UnitSpawner: SummonDiscountRate, SellBonusFoodAmount,
///               HasSellDealsDamage, SellDamagePct,
///               HasChieftainGainOnSell, ChieftainSellAtkGain, ChieftainSellPopPenalty
///   MergeManager: HasMergeKeepsTribe
///   TotemSpawner: HasAllowTotemOverlap
/// </summary>
public class LevelUpManager : InGameSingleton<LevelUpManager>
{
    [SerializeField] private LevelUpData[] levelUpPool;

    // ── 기본 스탯 ──────────────────────────────────────────────
    public float CritChance           { get; private set; } = 0f;
    public float CritDamageMultiplier { get; private set; } = 1.5f;
    public float ExpGainMultiplier    { get; private set; } = 1f;

    // ── 줄별 배율 ─────────────────────────────────────────────
    private readonly float[] _rowAttackMult = { 1f, 1f, 1f, 1f };
    private readonly float[] _rowSpeedMult  = { 1f, 1f, 1f, 1f };

    public float GetRowAttackMultiplier(int row)
        => (row >= 0 && row < _rowAttackMult.Length) ? _rowAttackMult[row] : 1f;

    public float GetRowSpeedMultiplier(int row)
        => (row >= 0 && row < _rowSpeedMult.Length) ? _rowSpeedMult[row] : 1f;

    // ── 족장 공격 보너스 ───────────────────────────────────────
    public float ChieftainAttackBonus { get; private set; } = 0f;

    // ── 부족별 특수 버프 ───────────────────────────────────────
    public float NinjaAtkBonus       { get; private set; } = 0f;
    public float NinjaSpeedBonus     { get; private set; } = 0f;
    public float GunnerAtkBonus      { get; private set; } = 0f;
    public float GunnerSpeedBonus    { get; private set; } = 0f;
    public float WizardAtkBonus      { get; private set; } = 0f;
    public float WizardCooldownBonus { get; private set; } = 0f; // 양수=쿨타임 감소(빠름)

    public float GetTribeAtkBonus(UnitTribe tribe) => tribe switch
    {
        UnitTribe.Ninja   => NinjaAtkBonus,
        UnitTribe.Gunner  => GunnerAtkBonus,
        UnitTribe.Wizard  => WizardAtkBonus,
        _                 => 0f,
    };

    public float GetTribeSpeedBonus(UnitTribe tribe) => tribe switch
    {
        UnitTribe.Ninja  => NinjaSpeedBonus,
        UnitTribe.Gunner => GunnerSpeedBonus,
        _                => 0f,
    };

    public float GetWizardCooldownBonus() => WizardCooldownBonus;

    // ── 공격 관련 특수 플래그 (UnitBase 참조) ─────────────────
    public readonly List<int> BonusAttackEveryNHits = new();
    public float RandomExtraAttackChance { get; private set; } = 0f;
    public bool  HasRandomProcAttack     { get; private set; }
    public float RandomProcChance        { get; private set; } = 0f;
    public float RandomProcDamagePct     { get; private set; } = 0f;
    public bool  HasExtraAttackEveryAttack { get; private set; }
    public bool  HasExtraAttackOnSkillFull { get; private set; }
    public bool  HasBurstOnSkillFull       { get; private set; }
    public float BurstAttackBonus          { get; private set; } = 0f;
    public float BurstDurationSeconds      { get; private set; } = 0f;

    // ── 유닛 특성 변경 플래그 (UnitBase 참조) ─────────────────
    public bool  HasWizardLightningMode  { get; private set; }
    public bool  HasWizardPhysicalMode   { get; private set; }
    public bool  HasUnemployedFoodNegate { get; private set; }
    public float UnemployedSkillAtkGain  { get; private set; } = 1f;

    // ── 투사체 크기 → 공격력 스케일 (UnitBase 참조) ───────────
    public bool  HasProjectileSizeScalesAtk { get; private set; }
    public float ProjectileSizeAtkPerUnit   { get; private set; } = 0f; // 크기 10%당 공격력 N%

    // ── 소환 / 판매 플래그 (UnitSpawner 참조) ─────────────────
    public float SummonDiscountRate      { get; private set; } = 0f;
    public float SellBonusFoodAmount     { get; private set; } = 0f;
    public bool  HasSellDealsDamage      { get; private set; }
    public float SellDamagePct           { get; private set; } = 0f;
    public bool  HasChieftainGainOnSell  { get; private set; }
    public float ChieftainSellAtkGain    { get; private set; } = 0f;
    public float ChieftainSellPopPenalty { get; private set; } = 0f;

    // ── 합성 / 토템 플래그 ─────────────────────────────────────
    public bool HasMergeKeepsTribe  { get; private set; }
    public bool HasAllowTotemOverlap { get; private set; }

    // ══════════════════════════════════════════════════════════
    // 랜덤 선택지 3장 뽑기 (가중치 + 부족 필터)
    // ══════════════════════════════════════════════════════════

    public List<LevelUpData> GetRandomChoices(int count = 3)
    {
        if (levelUpPool == null || levelUpPool.Length == 0)
        {
            Debug.LogError("LevelUpManager: levelUpPool이 비어있습니다.");
            return new List<LevelUpData>();
        }

        var presentTribes = GetPresentTribes();
        var filtered = new List<LevelUpData>(levelUpPool.Length);

        foreach (var data in levelUpPool)
        {
            if (data != null && IsApplicable(data, presentTribes))
                filtered.Add(data);
        }

        var result = new List<LevelUpData>(count);
        count = Mathf.Min(count, filtered.Count);

        for (int i = 0; i < count; i++)
        {
            float total = 0f;
            foreach (var d in filtered) total += d.spawnRate;

            float roll   = Random.Range(0f, total);
            float cumul  = 0f;
            bool  picked = false;

            for (int j = 0; j < filtered.Count; j++)
            {
                cumul += filtered[j].spawnRate;
                if (roll <= cumul)
                {
                    result.Add(filtered[j]);
                    filtered.RemoveAt(j);
                    picked = true;
                    break;
                }
            }

            if (!picked && filtered.Count > 0)
            {
                result.Add(filtered[filtered.Count - 1]);
                filtered.RemoveAt(filtered.Count - 1);
            }
        }

        return result;
    }

    private HashSet<UnitTribe> GetPresentTribes()
    {
        var tribes = new HashSet<UnitTribe>();
        if (Manager.Grid == null) return tribes;
        foreach (var cell in Manager.Grid.GetOccupiedCells())
        {
            if (cell.OccupyingUnit?.unitData != null)
                tribes.Add(cell.OccupyingUnit.unitData.unitTribe);
        }
        return tribes;
    }

    private static bool IsApplicable(LevelUpData data, HashSet<UnitTribe> presentTribes)
    {
        if (data.applicableTribes == null || data.applicableTribes.Length == 0) return true;
        foreach (var t in data.applicableTribes)
            if (presentTribes.Contains(t)) return true;
        return false;
    }

    // ══════════════════════════════════════════════════════════
    // 효과 적용
    // ══════════════════════════════════════════════════════════

    public void ApplyEffect(LevelUpData data)
    {
        ApplyStatEffect(data.primaryEffect,   data.primaryValue);
        ApplyStatEffect(data.secondaryEffect, data.secondaryValue);
        ApplySpecialEffect(data);
        Debug.Log($"[LevelUp] 적용: {data.chooseName} ({data.chooseId})");
    }

    private void ApplyStatEffect(LevelUpEffectType effectType, float value)
    {
        if (effectType == LevelUpEffectType.None || value == 0f) return;

        float v    = value / 100f;
        int   rows = Manager.Grid != null ? Manager.Grid.Rows : 4;

        switch (effectType)
        {
            case LevelUpEffectType.AttackPercent:
                Manager.Buff.AddLevelUpAttackBuff(v);
                break;

            case LevelUpEffectType.AttackSpeedPercent:
                // AddLevelUpSpeedBuff(양수) → SpeedMultiplier 감소 → interval 감소 → 더 빠름
                Manager.Buff.AddLevelUpSpeedBuff(v);
                break;

            case LevelUpEffectType.TotemEfficiencyPercent:
                Manager.Buff.AddTotemEfficiency(v);
                break;

            case LevelUpEffectType.CritChancePercent:
                CritChance = Mathf.Clamp01(CritChance + v);
                break;

            case LevelUpEffectType.CritDamagePercent:
                CritDamageMultiplier += v;
                break;

            case LevelUpEffectType.FoodProductionPercent:
                Manager.Buff.AddFoodSpeedBuff(v);
                break;

            case LevelUpEffectType.ProjectileSizePercent:
                Manager.Buff.AddProjectileSizeBuff(v);
                break;

            case LevelUpEffectType.GaugeSpeedPercent:
                Manager.Buff.AddGaugeSpeedBuff(v);
                break;

            case LevelUpEffectType.ExpGainPercent:
                ExpGainMultiplier *= (1f + v);
                break;

            case LevelUpEffectType.FrontRowAttackPercent:
                _rowAttackMult[0] += v;
                if (rows > 1) _rowAttackMult[1] += v;
                break;

            case LevelUpEffectType.BackRowAttackPercent:
                if (rows >= 2) _rowAttackMult[rows - 1] += v;
                if (rows >= 3) _rowAttackMult[rows - 2] += v;
                break;

            case LevelUpEffectType.FrontRowSpeedPercent:
                _rowSpeedMult[0] *= (1f + v);
                if (rows > 1) _rowSpeedMult[1] *= (1f + v);
                break;

            case LevelUpEffectType.BackRowSpeedPercent:
                if (rows >= 2) _rowSpeedMult[rows - 1] *= (1f + v);
                if (rows >= 3) _rowSpeedMult[rows - 2] *= (1f + v);
                break;

            case LevelUpEffectType.ChieftainAttackPercent:
                ChieftainAttackBonus += v;
                break;
        }
    }

    private void ApplySpecialEffect(LevelUpData data)
    {
        switch (data.specialEffect)
        {
            case LevelUpSpecialEffect.None: break;

            // ── 즉시 효과 ──────────────────────────────────────
            case LevelUpSpecialEffect.GiveFoodAmount:
                Manager.Currency.AddCurrency(data.specialValue);
                break;

            case LevelUpSpecialEffect.PopulationIncrease:
                Manager.Population?.AddMaxBonus((int)data.specialValue);
                break;

            case LevelUpSpecialEffect.TriggerTotemSelection:
                Manager.TotemSelect?.Show(null);
                break;

            case LevelUpSpecialEffect.GainRandomUnit:
                SpawnUnitAsync(null, Tier.Normal, Tier.Rare).Forget();
                break;

            case LevelUpSpecialEffect.GainGunnerUnit:
                SpawnUnitAsync(UnitTribe.Gunner, Tier.Rare, Tier.Epic).Forget();
                break;

            case LevelUpSpecialEffect.GainNinjaUnit:
                SpawnUnitAsync(UnitTribe.Ninja, Tier.Rare, Tier.Epic).Forget();
                break;

            case LevelUpSpecialEffect.GainWizardUnit:
                SpawnUnitAsync(UnitTribe.Wizard, Tier.Rare, Tier.Epic).Forget();
                break;

            case LevelUpSpecialEffect.GainUnemployedUnit:
                SpawnUnitAsync(UnitTribe.UnEmployed, Tier.Rare, Tier.Epic).Forget();
                break;

            case LevelUpSpecialEffect.BuffNinjaTribe:
                NinjaAtkBonus   += data.primaryValue  / 100f;
                NinjaSpeedBonus += data.secondaryValue / 100f;
                break;

            case LevelUpSpecialEffect.BuffGunnerTribe:
                GunnerAtkBonus   += data.primaryValue  / 100f;
                GunnerSpeedBonus += data.secondaryValue / 100f;
                break;

            case LevelUpSpecialEffect.BuffWizardTribe:
                WizardAtkBonus      += data.primaryValue  / 100f;
                WizardCooldownBonus += data.secondaryValue / 100f;
                break;

            // ── 공격 패시브 ────────────────────────────────────
            case LevelUpSpecialEffect.AttackEveryNHits:
                BonusAttackEveryNHits.Add((int)data.specialValue);
                break;

            case LevelUpSpecialEffect.RandomBonusAttack:
                RandomExtraAttackChance += data.specialValue / 100f;
                break;

            case LevelUpSpecialEffect.RandomProcAttack:
                HasRandomProcAttack = true;
                RandomProcChance   += data.specialValue / 100f;
                RandomProcDamagePct = data.primaryValue / 100f; // 마지막 설정값 사용
                break;

            case LevelUpSpecialEffect.ExtraAttackEveryAttack:
                HasExtraAttackEveryAttack = true;
                break;

            case LevelUpSpecialEffect.ExtraAttackOnSkillFull:
                HasExtraAttackOnSkillFull = true;
                break;

            case LevelUpSpecialEffect.BurstOnSkillFull:
                HasBurstOnSkillFull = true;
                BurstAttackBonus    = Mathf.Max(BurstAttackBonus, data.primaryValue  / 100f);
                BurstDurationSeconds = Mathf.Max(BurstDurationSeconds, data.specialValue);
                break;

            // ── 소환 / 판매 패시브 ─────────────────────────────
            case LevelUpSpecialEffect.SummonDiscount:
                SummonDiscountRate += data.specialValue / 100f;
                break;

            case LevelUpSpecialEffect.SellBonusFood:
                SellBonusFoodAmount += data.specialValue;
                break;

            case LevelUpSpecialEffect.SellDealsDamage:
                HasSellDealsDamage = true;
                SellDamagePct      = Mathf.Max(SellDamagePct, data.specialValue / 100f);
                break;

            case LevelUpSpecialEffect.ChieftainGainOnSell:
                HasChieftainGainOnSell  = true;
                ChieftainSellAtkGain   += data.primaryValue  / 100f;
                ChieftainSellPopPenalty += data.secondaryValue / 100f;
                break;

            // ── 합성 / 토템 패시브 ─────────────────────────────
            case LevelUpSpecialEffect.MergeKeepsTribe:
                HasMergeKeepsTribe = true;
                break;

            case LevelUpSpecialEffect.AllowTotemOverlap:
                HasAllowTotemOverlap = true;
                break;

            // ── 유닛 행동 변경 ─────────────────────────────────
            case LevelUpSpecialEffect.WizardLightningMode:
                HasWizardLightningMode = true;
                break;

            case LevelUpSpecialEffect.WizardPhysicalMode:
                HasWizardPhysicalMode = true;
                Manager.Buff.AddLevelUpAttackBuff(data.primaryValue / 100f);
                Manager.Buff.AddLevelUpSpeedBuff(data.primaryValue  / 100f);
                break;

            case LevelUpSpecialEffect.UnemployedFoodNegate:
                HasUnemployedFoodNegate = true;
                UnemployedSkillAtkGain  = data.primaryValue; // +N 공격력/스킬
                break;

            case LevelUpSpecialEffect.ProjectileSizeScalesAtk:
                HasProjectileSizeScalesAtk = true;
                ProjectileSizeAtkPerUnit   = data.primaryValue / 100f;
                break;
        }
    }

    // ── 유닛 즉시 스폰 (GainUnit 계열) ────────────────────────

    private async UniTaskVoid SpawnUnitAsync(UnitTribe? tribe, Tier minTier, Tier maxTier)
    {
        await UniTask.Yield(this.GetCancellationTokenOnDestroy());

        var emptyCells = Manager.Grid?.GetEmptyCells();
        if (emptyCells == null || emptyCells.Count == 0)
        {
            Debug.LogWarning("[LevelUp] 빈 셀 없음 — 기물 획득 취소");
            return;
        }

        var  cell = emptyCells[Random.Range(0, emptyCells.Count)];
        Tier tier = (Tier)Random.Range((int)minTier, (int)maxTier + 1);

        UnitBase unit = tribe.HasValue
            ? Manager.UnitFactory.CreateRandomUnitByTribeAndTier(tribe.Value, tier)
            : Manager.UnitFactory.CreateRandomUnitOfTier(tier);

        if (unit == null) return;

        cell.TryPlaceUnit(unit);
        unit.transform.SetParent(cell.transform, false);

        var rt = unit.GetComponent<UnityEngine.RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
            rt.anchoredPosition = UnityEngine.Vector2.zero;
        }

        var drag = unit.GetComponent<DragHandler>();
        if (drag != null) drag.SetOriginCell(cell);

        unit.OnPlaced(Manager.Currency, Manager.Boss.CurrentBoss, cell);
    }

    // ── 투사체 크기 → 공격력 스케일 계산 ─────────────────────

    /// <summary>현재 투사체 크기 배율 기준 공격력 보너스 비율 반환</summary>
    public float GetProjectileSizeAtkBonus()
    {
        if (!HasProjectileSizeScalesAtk) return 0f;
        float sizeBonus = Manager.Buff.ProjectileSizeMultiplier - 1f; // 0 이상
        return Mathf.Max(0f, sizeBonus / 0.1f * ProjectileSizeAtkPerUnit);
    }
}
