using UnityEngine;
using VContainer;
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
public class LevelUpManager : MonoBehaviour
{
 
    public void Init()
    {
        if (_gameDataManager != null)
        {
            if (_gameDataManager.IsLoaded)
                SyncStatsWithSheet();
            else
                _gameDataManager.OnLoaded += SyncStatsWithSheet;
        }
    }

    [Inject] private GridManager _gridManager;
    [Inject] private TotemBuffManager _totemBuffManager;
    [Inject] private CurrencyManager _currencyManager;
    [Inject] private PopulationManager _populationManager;
    
    [Inject] private UnitFactory _unitFactoryManager;
    [Inject] private UnitSpawner _spawnerManager;
    [Inject] private GameDataManager _gameDataManager;

    [SerializeField] private LevelUpData[] levelUpPool;

    public event System.Action<System.Action> OnTotemSelectionRequested;
    public event System.Action OnChieftainBuffChanged;

    public IEnumerable<int> ChosenIds => _chosenIds;
    public LevelUpData[] LevelUpPool => levelUpPool;

    private readonly HashSet<int> _chosenIds = new();

    private void SyncStatsWithSheet()
    {
        if (_gameDataManager == null || !_gameDataManager.IsLoaded) return;

        for (int i = 0; i < levelUpPool.Length; i++)
        {
            var data = levelUpPool[i];
            if (data == null) continue;

            // SO 데이터 훼손 방지를 위한 런타임 클론
            var clone = Instantiate(data);
            clone.name = data.name + "_Runtime";
            
            var row = _gameDataManager.GetLevelUpRow(clone.chooseId);
            if (row != null)
            {
                clone.ApplySheetData(row);
            }
            
            levelUpPool[i] = clone;
        }
    }

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
    public float ChieftainFoodProductionBonus { get; private set; } = 0f;

    // ── 부족별 특수 버프 ───────────────────────────────────────
    public float NinjaAtkBonus       { get; private set; } = 0f;
    public float NinjaSpeedBonus     { get; private set; } = 0f;
    public float GunnerAtkBonus      { get; private set; } = 0f;
    public float GunnerSpeedBonus    { get; private set; } = 0f;
    public float WizardAtkBonus      { get; private set; } = 0f;
    public float WizardSpeedBonus    { get; private set; } = 0f;
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
        UnitTribe.Ninja   => NinjaSpeedBonus,
        UnitTribe.Gunner  => GunnerSpeedBonus,
        UnitTribe.Wizard  => WizardSpeedBonus,
        _                 => 0f,
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
    public float SummonFixedDiscountAmount { get; private set; } = 0f;
    public float SellBonusFoodAmount     { get; private set; } = 0f;
    public bool  HasSummonDealsDamage    { get; private set; }
    public float SummonDamagePct         { get; private set; } = 0f;
    public bool  HasSellDealsDamage      { get; private set; }
    public float SellDamagePct           { get; private set; } = 0f;
    public bool  HasSellGivesRandomUnit  { get; private set; }
    public float SellGivesUnitChance     { get; private set; } = 0f;
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
        var filtered = new List<LevelUpData>();

        foreach (var data in levelUpPool)
        {
            if (data != null && !_chosenIds.Contains(data.chooseId) && IsApplicable(data, presentTribes))
                filtered.Add(data);
        }

        var tierGroups = new Dictionary<Tier, List<LevelUpData>>();
        var tierWeights = new Dictionary<Tier, float>();

        foreach (var d in filtered)
        {
            if (!tierGroups.ContainsKey(d.tier)) 
            {
                tierGroups[d.tier] = new List<LevelUpData>();
                tierWeights[d.tier] = 0f;
            }
            tierGroups[d.tier].Add(d);
            tierWeights[d.tier] += d.spawnRate;
        }

        if (tierGroups.Count == 0) return new List<LevelUpData>();

        // 1. 등급(Tier) 추첨
        float totalWeight = 0f;
        foreach (var w in tierWeights.Values) totalWeight += w;

        float roll = Random.Range(0f, totalWeight);
        float cumul = 0f;
        Tier selectedTier = Tier.Normal;

        foreach (var kvp in tierWeights)
        {
            cumul += kvp.Value;
            if (roll <= cumul)
            {
                selectedTier = kvp.Key;
                break;
            }
        }

        // 2. 선택된 등급 내에서 슬롯 뽑기
        var group = tierGroups[selectedTier];
        var result = new List<LevelUpData>();
        int pickCount = Mathf.Min(count, group.Count);

        for (int i = 0; i < pickCount; i++)
        {
            int idx = Random.Range(0, group.Count);
            result.Add(group[idx]);
            group.RemoveAt(idx);
        }

        // 등급 내 갯수가 모자라면 다른 등급에서라도 채움
        filtered.RemoveAll(x => result.Contains(x));
        while (result.Count < count && filtered.Count > 0)
        {
            int idx = Random.Range(0, filtered.Count);
            result.Add(filtered[idx]);
            filtered.RemoveAt(idx);
        }

        return result;
    }

    private HashSet<UnitTribe> GetPresentTribes()
    {
        var tribes = new HashSet<UnitTribe>();
        if (_gridManager == null) return tribes;
        foreach (var cell in _gridManager.GetOccupiedCells())
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
        _chosenIds.Add(data.chooseId);
        ApplyStatEffect(data.primaryEffect,   data.primaryValue);
        ApplyStatEffect(data.secondaryEffect, data.secondaryValue);
        ApplySpecialEffect(data);
        Debug.Log($"[LevelUp] 적용: {data.chooseName} ({data.chooseId})");
    }

    public void RemoveEffect(LevelUpData data)
    {
        if (_chosenIds.Remove(data.chooseId))
        {
            RemoveStatEffect(data.primaryEffect, data.primaryValue);
            RemoveStatEffect(data.secondaryEffect, data.secondaryValue);
            RemoveSpecialEffect(data);
            Debug.Log($"[LevelUp] 제거: {data.chooseName} ({data.chooseId})");
        }
    }

    private void RemoveStatEffect(LevelUpEffectType effectType, float value)
    {
        if (effectType == LevelUpEffectType.None || value == 0f) return;

        float v = value / 100f;
        int rows = _gridManager != null ? _gridManager.Rows : 4;

        switch (effectType)
        {
            case LevelUpEffectType.AttackPercent:
                // LevelUpAttackBuff adds to a sum. Remove requires subtracting. 
                // TotemBuffManager needs RemoveLevelUpAttackBuff, but we can do a negative add for now.
                _totemBuffManager.AddLevelUpAttackBuff(-v);
                break;
            case LevelUpEffectType.AttackSpeedPercent:
                _totemBuffManager.AddLevelUpSpeedBuff(-v);
                break;
            case LevelUpEffectType.TotemEfficiencyPercent:
                _totemBuffManager.AddTotemEfficiency(-v);
                break;
            case LevelUpEffectType.CritChancePercent:
                CritChance = Mathf.Clamp01(CritChance - v);
                break;
            case LevelUpEffectType.CritDamagePercent:
                CritDamageMultiplier -= v;
                break;
            case LevelUpEffectType.FoodProductionPercent:
                _totemBuffManager.AddFoodSpeedBuff(-v);
                break;
            case LevelUpEffectType.ProjectileSizePercent:
                _totemBuffManager.AddProjectileSizeBuff(-v);
                break;
            case LevelUpEffectType.GaugeSpeedPercent:
                _totemBuffManager.AddGaugeSpeedBuff(-v);
                break;
            case LevelUpEffectType.ExpGainPercent:
                ExpGainMultiplier /= (1f + v);
                break;
            case LevelUpEffectType.FrontRowAttackPercent:
                _rowAttackMult[0] -= v;
                if (rows > 1) _rowAttackMult[1] -= v;
                break;
            case LevelUpEffectType.BackRowAttackPercent:
                if (rows >= 2) _rowAttackMult[rows - 1] -= v;
                if (rows >= 3) _rowAttackMult[rows - 2] -= v;
                break;
            case LevelUpEffectType.FrontRowSpeedPercent:
                _rowSpeedMult[0] /= (1f + v);
                if (rows > 1) _rowSpeedMult[1] /= (1f + v);
                break;
            case LevelUpEffectType.BackRowSpeedPercent:
                if (rows >= 2) _rowSpeedMult[rows - 1] /= (1f + v);
                if (rows >= 3) _rowSpeedMult[rows - 2] /= (1f + v);
                break;
            case LevelUpEffectType.ChieftainAttackPercent:
                ChieftainAttackBonus -= v;
                break;
            case LevelUpEffectType.ChieftainFoodProductionPercent:
                ChieftainFoodProductionBonus -= v;
                OnChieftainBuffChanged?.Invoke();
                break;
        }
    }

    private void RemoveSpecialEffect(LevelUpData data)
    {
        switch (data.specialEffect)
        {
            case LevelUpSpecialEffect.None: break;

            case LevelUpSpecialEffect.BuffNinjaTribe:
                NinjaAtkBonus -= data.primaryValue / 100f;
                NinjaSpeedBonus -= data.secondaryValue / 100f;
                break;
            case LevelUpSpecialEffect.BuffGunnerTribe:
                GunnerAtkBonus -= data.primaryValue / 100f;
                GunnerSpeedBonus -= data.secondaryValue / 100f;
                break;
            case LevelUpSpecialEffect.BuffWizardTribe:
                WizardAtkBonus -= data.primaryValue / 100f;
                WizardCooldownBonus -= data.secondaryValue / 100f;
                break;
            case LevelUpSpecialEffect.AttackEveryNHits:
                BonusAttackEveryNHits.Remove((int)data.specialValue);
                break;
            case LevelUpSpecialEffect.RandomBonusAttack:
                RandomExtraAttackChance -= data.specialValue / 100f;
                break;
            case LevelUpSpecialEffect.RandomProcAttack:
                // It's hard to revert HasRandomProcAttack if multiple are added, assuming 1 for now.
                HasRandomProcAttack = false;
                RandomProcChance -= data.specialValue / 100f;
                RandomProcDamagePct = 0f;
                break;
            case LevelUpSpecialEffect.ExtraAttackEveryAttack:
                HasExtraAttackEveryAttack = false;
                break;
            case LevelUpSpecialEffect.ExtraAttackOnSkillFull:
                HasExtraAttackOnSkillFull = false;
                break;
            case LevelUpSpecialEffect.BurstOnSkillFull:
                HasBurstOnSkillFull = false;
                BurstAttackBonus = 0f;
                BurstDurationSeconds = 0f;
                break;
            case LevelUpSpecialEffect.SummonDiscount:
                SummonDiscountRate -= data.specialValue / 100f;
                break;
            case LevelUpSpecialEffect.SummonFixedDiscount:
                SummonFixedDiscountAmount -= data.specialValue;
                break;
            case LevelUpSpecialEffect.SellBonusFood:
                SellBonusFoodAmount -= data.specialValue;
                break;
            case LevelUpSpecialEffect.SummonDealsDamage:
                HasSummonDealsDamage = false;
                SummonDamagePct = 0f;
                break;
            case LevelUpSpecialEffect.SellDealsDamage:
                HasSellDealsDamage = false;
                SellDamagePct = 0f;
                break;
            case LevelUpSpecialEffect.SellGivesRandomUnit:
                HasSellGivesRandomUnit = false;
                SellGivesUnitChance = 0f;
                break;
            case LevelUpSpecialEffect.ChieftainGainOnSell:
                HasChieftainGainOnSell = false;
                ChieftainSellAtkGain -= data.primaryValue / 100f;
                ChieftainSellPopPenalty -= data.secondaryValue / 100f;
                break;
            case LevelUpSpecialEffect.MergeKeepsTribe:
                HasMergeKeepsTribe = false;
                break;
            case LevelUpSpecialEffect.AllowTotemOverlap:
                HasAllowTotemOverlap = false;
                break;
            case LevelUpSpecialEffect.WizardLightningMode:
                HasWizardLightningMode = false;
                break;
            case LevelUpSpecialEffect.WizardPhysicalMode:
                HasWizardPhysicalMode = false;
                WizardAtkBonus -= data.primaryValue / 100f;
                WizardSpeedBonus -= data.primaryValue / 100f;
                break;
            case LevelUpSpecialEffect.UnemployedFoodNegate:
                HasUnemployedFoodNegate = false;
                UnemployedSkillAtkGain = 0f;
                break;
            case LevelUpSpecialEffect.ProjectileSizeScalesAtk:
                HasProjectileSizeScalesAtk = false;
                ProjectileSizeAtkPerUnit = 0f;
                break;
        }
    }

    private void ApplyStatEffect(LevelUpEffectType effectType, float value)
    {
        if (effectType == LevelUpEffectType.None || value == 0f) return;

        float v    = value / 100f;
        int   rows = _gridManager != null ? _gridManager.Rows : 4;

        switch (effectType)
        {
            case LevelUpEffectType.AttackPercent:
                _totemBuffManager.AddLevelUpAttackBuff(v);
                break;

            case LevelUpEffectType.AttackSpeedPercent:
                // AddLevelUpSpeedBuff(양수) → SpeedMultiplier 감소 → interval 감소 → 더 빠름
                _totemBuffManager.AddLevelUpSpeedBuff(v);
                break;

            case LevelUpEffectType.TotemEfficiencyPercent:
                _totemBuffManager.AddTotemEfficiency(v);
                break;

            case LevelUpEffectType.CritChancePercent:
                CritChance = Mathf.Clamp01(CritChance + v);
                break;

            case LevelUpEffectType.CritDamagePercent:
                CritDamageMultiplier += v;
                break;

            case LevelUpEffectType.FoodProductionPercent:
                _totemBuffManager.AddFoodSpeedBuff(v);
                break;

            case LevelUpEffectType.ProjectileSizePercent:
                _totemBuffManager.AddProjectileSizeBuff(v);
                break;

            case LevelUpEffectType.GaugeSpeedPercent:
                _totemBuffManager.AddGaugeSpeedBuff(v);
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

            case LevelUpEffectType.ChieftainFoodProductionPercent:
                ChieftainFoodProductionBonus += v;
                OnChieftainBuffChanged?.Invoke();
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
                _currencyManager.AddCurrency(data.specialValue);
                break;

            case LevelUpSpecialEffect.PopulationIncrease:
                _populationManager?.AddMaxBonus((int)data.specialValue);
                break;

            case LevelUpSpecialEffect.TriggerTotemSelection:
                OnTotemSelectionRequested?.Invoke(null);
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

            case LevelUpSpecialEffect.SummonFixedDiscount:
                SummonFixedDiscountAmount += data.specialValue;
                break;

            case LevelUpSpecialEffect.SellBonusFood:
                SellBonusFoodAmount += data.specialValue;
                break;

            case LevelUpSpecialEffect.SummonDealsDamage:
                HasSummonDealsDamage = true;
                SummonDamagePct      = Mathf.Max(SummonDamagePct, data.specialValue / 100f);
                break;

            case LevelUpSpecialEffect.SellDealsDamage:
                HasSellDealsDamage = true;
                SellDamagePct      = Mathf.Max(SellDamagePct, data.specialValue / 100f);
                break;

            case LevelUpSpecialEffect.SellGivesRandomUnit:
                HasSellGivesRandomUnit = true;
                SellGivesUnitChance    += data.specialValue / 100f;
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
                WizardAtkBonus   += data.primaryValue / 100f;
                WizardSpeedBonus += data.primaryValue / 100f;
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

        var emptyCells = _gridManager?.GetEmptyCells();
        if (emptyCells == null || emptyCells.Count == 0)
        {
            Debug.LogWarning("[LevelUp] 빈 셀 없음 — 기물 획득 취소");
            return;
        }

        // 인구수 상한 검사 — 가득 차면 보상 유닛도 배치하지 않는다 (수동 소환과 동일 규칙)
        if (_populationManager != null && !_populationManager.CanAdd(1))
        {
            Debug.Log("[LevelUp] 인구수 상한 도달 — 기물 획득 취소");
            return;
        }

        var  cell = emptyCells[Random.Range(0, emptyCells.Count)];
        Tier tier = (Tier)Random.Range((int)minTier, (int)maxTier + 1);

        UnitBase unit = tribe.HasValue
            ? _unitFactoryManager.CreateRandomUnitByTribeAndTier(tribe.Value, tier)
            : _unitFactoryManager.CreateRandomUnitOfTier(tier);

        if (unit == null) return;

        _spawnerManager.PlaceUnitWithEffect(unit, cell);
    }

    // ── 투사체 크기 → 공격력 스케일 계산 ─────────────────────

    /// <summary>현재 투사체 크기 배율 기준 공격력 보너스 비율 반환</summary>
    public float GetProjectileSizeAtkBonus()
    {
        if (!HasProjectileSizeScalesAtk) return 0f;
        float sizeBonus = _totemBuffManager.ProjectileSizeMultiplier - 1f; // 0 이상
        return Mathf.Max(0f, sizeBonus / 0.1f * ProjectileSizeAtkPerUnit);
    }
}
