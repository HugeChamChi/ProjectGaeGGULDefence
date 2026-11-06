using System;
using System.Collections.Generic;

/// <summary>씬의 단일 런 상태를 계속 따라가는 읽기 전용 DI 공급자.</summary>
public sealed class SelectionEffectReader : ISelectionCombatReader, IDroneEffectReader, ISelectionEconomyReader, IChiefSelectionReader, ISelectionEffectChanges, IGlitchEffectReader, IOverdriveEffectReader
{
    /// <summary>감망 운반 자폭 프리팹.</summary>
    public SelfDestructDrone GammanBombPrefab => _state.Current.GammanBombPrefab;
    /// <summary>BasicSkillExtraBombs 선택지 기여.</summary>
    public int BasicSkillExtraBombs => _state.Current.BasicSkillExtraBombs;
    /// <summary>BombRearmHits 선택지 기여.</summary>
    public int BombRearmHits => _state.Current.BombRearmHits;
    /// <summary>BombRearmExtraBombs 선택지 기여.</summary>
    public int BombRearmExtraBombs => _state.Current.BombRearmExtraBombs;
    /// <summary>BombFuseStacks 선택지 기여.</summary>
    public int BombFuseStacks => _state.Current.BombFuseStacks;
    /// <summary>BombFuseBonusPerStack 선택지 기여.</summary>
    public float BombFuseBonusPerStack => _state.Current.BombFuseBonusPerStack;
    /// <summary>HasGammanBomb 선택지 기여.</summary>
    public bool HasGammanBomb => _state.Current.HasGammanBomb;
    /// <summary>BetanCycleMultiplier 선택지 기여.</summary>
    public float BetanCycleMultiplier => _state.Current.BetanCycleMultiplier;
    /// <summary>HasCounterBomb 선택지 기여.</summary>
    public bool HasCounterBomb => _state.Current.HasCounterBomb;
    /// <summary>BombDamageMultiplier 선택지 기여.</summary>
    public float BombDamageMultiplier => _state.Current.BombDamageMultiplier;
    /// <summary>BombWarheadCycleMultiplier 선택지 기여.</summary>
    public float BombWarheadCycleMultiplier => _state.Current.BombWarheadCycleMultiplier;
    /// <summary>BombArmorReduction 선택지 기여.</summary>
    public float BombArmorReduction => _state.Current.BombArmorReduction;
    /// <summary>BombArmorDuration 선택지 기여.</summary>
    public float BombArmorDuration => _state.Current.BombArmorDuration;
    /// <summary>BombExecutionThreshold 선택지 기여.</summary>
    public float BombExecutionThreshold => _state.Current.BombExecutionThreshold;
    /// <summary>BombExecutionBonus 선택지 기여.</summary>
    public float BombExecutionBonus => _state.Current.BombExecutionBonus;
    /// <summary>BombSalvageHits 선택지 기여.</summary>
    public int BombSalvageHits => _state.Current.BombSalvageHits;
    /// <summary>BombSalvageFood 선택지 기여.</summary>
    public float BombSalvageFood => _state.Current.BombSalvageFood;

    /// <inheritdoc />
    public float ExtraTimeDrainRatio => _state.Current.ExtraTimeDrainRatio;
    /// <inheritdoc />
    public float AcceleratedAttackSpeedBonus => _state.Current.AcceleratedAttackSpeedBonus;
    /// <inheritdoc />
    public float SacrificeCostSeconds => _state.Current.SacrificeCostSeconds;
    /// <inheritdoc />
    public float SacrificeAttackPerPayment => _state.Current.SacrificeAttackPerPayment;
    /// <inheritdoc />
    public float TimeVaultSeconds => _state.Current.TimeVaultSeconds;
    /// <inheritdoc />
    public float OverheatCostSeconds => _state.Current.OverheatCostSeconds;
    /// <inheritdoc />
    public float OverheatDamageWeight => _state.Current.OverheatDamageWeight;
    /// <inheritdoc />
    public int OverheatOverflowPerStack => _state.Current.OverheatOverflowPerStack;
    /// <inheritdoc />
    public float SacrificeAttackBonus => _state.SacrificeAttackBonus;
    private readonly LevelUpRunState _state;
    /// <summary>모든 읽기 계약은 같은 상태 인스턴스를 공유한다.</summary>
    public SelectionEffectReader(LevelUpRunState state) => _state = state ?? throw new ArgumentNullException(nameof(state));
    /// <summary>HackingOverflowAttackCoefficient 선택지 기여.</summary>
    public float HackingOverflowAttackCoefficient => _state.Current.HackingOverflowAttackCoefficient;
    /// <summary>HackingConsumptionMultiplier 선택지 기여.</summary>
    public float HackingConsumptionMultiplier => _state.Current.HackingConsumptionMultiplier;
    /// <summary>HackingStackDamageMultiplier 선택지 기여.</summary>
    public float HackingStackDamageMultiplier => _state.Current.HackingStackDamageMultiplier;
    /// <summary>HackingCapacityBonus 선택지 기여.</summary>
    public int HackingCapacityBonus => _state.Current.HackingCapacityBonus;
    /// <summary>BetanAttackReductionRatio 선택지 기여.</summary>
    public float BetanAttackReductionRatio => _state.Current.BetanAttackReductionRatio;
    /// <inheritdoc />
    public event Action OnEffectsChanged;
    /// <inheritdoc />
    public float AttackBonus => _state.Current.AttackBonus;
    /// <inheritdoc />
    public float AttackSpeedBonus => _state.Current.AttackSpeedBonus;
    /// <inheritdoc />
    public float TotemEfficiencyBonus => _state.Current.TotemEfficiencyBonus;
    /// <inheritdoc />
    public float CritChance => _state.Current.CritChance;
    /// <inheritdoc />
    public float CritDamageMultiplier => _state.Current.CritDamageMultiplier;
    /// <inheritdoc />
    public float FoodSpeedBonus => _state.Current.FoodSpeedBonus;
    /// <inheritdoc />
    public float ProjectileSizeBonus => _state.Current.ProjectileSizeBonus;
    /// <inheritdoc />
    public float GaugeSpeedBonus => _state.Current.GaugeSpeedBonus;
    /// <inheritdoc />
    public float ExpGainMultiplier => _state.Current.ExpGainMultiplier;
    /// <inheritdoc />
    public float FrontAttackBonus => _state.Current.FrontAttackBonus;
    /// <inheritdoc />
    public float BackAttackBonus => _state.Current.BackAttackBonus;
    /// <inheritdoc />
    public float FrontSpeedMultiplier => _state.Current.FrontSpeedMultiplier;
    /// <inheritdoc />
    public float BackSpeedMultiplier => _state.Current.BackSpeedMultiplier;
    /// <inheritdoc />
    public float RandomExtraAttackChance => _state.Current.RandomExtraAttackChance;
    /// <inheritdoc />
    public bool HasRandomProcAttack => _state.Current.HasRandomProcAttack;
    /// <inheritdoc />
    public float RandomProcChance => _state.Current.RandomProcChance;
    /// <inheritdoc />
    public float RandomProcDamagePct => _state.Current.RandomProcDamagePct;
    /// <inheritdoc />
    public bool HasExtraAttackEveryAttack => _state.Current.HasExtraAttackEveryAttack;
    /// <inheritdoc />
    public bool HasExtraAttackOnSkillFull => _state.Current.HasExtraAttackOnSkillFull;
    /// <inheritdoc />
    public bool HasBurstOnSkillFull => _state.Current.HasBurstOnSkillFull;
    /// <inheritdoc />
    public float BurstAttackBonus => _state.Current.BurstAttackBonus;
    /// <inheritdoc />
    public float BurstDurationSeconds => _state.Current.BurstDurationSeconds;
    /// <inheritdoc />
    public float SummonDiscountRate => _state.Current.SummonDiscountRate;
    /// <inheritdoc />
    public float SummonFixedDiscountAmount => _state.Current.SummonFixedDiscountAmount;
    /// <inheritdoc />
    public float SellBonusFoodAmount => _state.Current.SellBonusFoodAmount;
    /// <inheritdoc />
    public bool HasSummonDealsDamage => _state.Current.HasSummonDealsDamage;
    /// <inheritdoc />
    public float SummonDamagePct => _state.Current.SummonDamagePct;
    /// <inheritdoc />
    public bool HasSellDealsDamage => _state.Current.HasSellDealsDamage;
    /// <inheritdoc />
    public float SellDamagePct => _state.Current.SellDamagePct;
    /// <inheritdoc />
    public bool HasSellGivesRandomUnit => _state.Current.HasSellGivesRandomUnit;
    /// <inheritdoc />
    public float SellGivesUnitChance => _state.Current.SellGivesUnitChance;
    /// <inheritdoc />
    public bool HasMergeKeepsTribe => _state.Current.HasMergeKeepsTribe;
    /// <inheritdoc />
    public bool HasAllowTotemOverlap => _state.Current.HasAllowTotemOverlap;
    /// <inheritdoc />
    public bool HasProjectileSizeScalesAtk => _state.Current.HasProjectileSizeScalesAtk;
    /// <inheritdoc />
    public float ProjectileSizeAtkPerUnit => _state.Current.ProjectileSizeAtkPerUnit;
    /// <inheritdoc />
    public float PeriodicBombInterval => _state.Current.PeriodicBombInterval;
    /// <inheritdoc />
    public int PeriodicBombCount => _state.Current.PeriodicBombCount;
    /// <inheritdoc />
    public float FleetBombInterval => _state.Current.FleetBombInterval;
    /// <inheritdoc />
    public int FleetBombCount => _state.Current.FleetBombCount;
    /// <inheritdoc />
    public float DroneAttackSpeedBonus => _state.Current.DroneAttackSpeedBonus;
    /// <inheritdoc />
    public float StackDamageBonusPerDrone => _state.Current.StackDamageBonusPerDrone;
    /// <inheritdoc />
    public float MaximumStackDamageBonus => _state.Current.MaximumStackDamageBonus;
    /// <inheritdoc />
    public bool HasEmptyStackDamage => _state.Current.HasEmptyStackDamage;
    /// <inheritdoc />
    public bool HasBatchedFoodPayout => _state.Current.HasBatchedFoodPayout;
    /// <inheritdoc />
    public float FoodPayoutInterval => _state.Current.FoodPayoutInterval;
    /// <inheritdoc />
    public float FoodProductionBonus => _state.Current.FoodProductionBonus;
    /// <inheritdoc />
    public float WaveFoodDividendRatio => _state.Current.WaveFoodDividendRatio;
    /// <inheritdoc />
    public int HackingProductionFlat => _state.Current.HackingProductionFlat;
    /// <inheritdoc />
    public float HackingProductionBonus => _state.Current.HackingProductionBonus;
    /// <inheritdoc />
    public int HackingCompressionRequiredAttacks => _state.Current.HackingCompressionRequiredAttacks;
    /// <inheritdoc />
    public int HackingCompressionMultiplier => _state.Current.HackingCompressionMultiplier;
    /// <inheritdoc />
    public float ManaRequirementReduction => _state.Current.ManaRequirementReduction;
    /// <inheritdoc />
    public bool HasManaRequirementReduction => _state.Current.HasManaRequirementReduction;
    /// <inheritdoc />
    public float HackingProductionProcChance => _state.Current.HackingProductionProcChance;
    /// <inheritdoc />
    public int HackingProductionProcMultiplier => _state.Current.HackingProductionProcMultiplier;
    /// <inheritdoc />
    public bool HasGammanResidue => _state.Current.HasGammanResidue;
    /// <inheritdoc />
    public float GammanResidueRatio => _state.Current.GammanResidueRatio;
    /// <inheritdoc />
    public float GammanResidueDuration => _state.Current.GammanResidueDuration;
    /// <inheritdoc />
    public int GammanResidueTicks => _state.Current.GammanResidueTicks;
    /// <inheritdoc />
    public float ChiefCooldownReduction => _state.Current.ChiefCooldownReduction;
    /// <inheritdoc />
    public int ChiefChargeCapacity => _state.Current.ChiefChargeCapacity;
    /// <inheritdoc />
    public float ChiefHackingDamagePerStack => _state.Current.ChiefHackingDamagePerStack;
    /// <inheritdoc />
    public float ChiefVolleyDelay => _state.Current.ChiefVolleyDelay;
    /// <inheritdoc />
    public float ChiefVolleyDamageRatio => _state.Current.ChiefVolleyDamageRatio;
    /// <inheritdoc />
    public int ChiefVolleyCount => _state.Current.ChiefVolleyCount;
    /// <inheritdoc />
    public float RallyDamageBonus => _state.Current.RallyDamageBonus;
    /// <inheritdoc />
    public bool HasRallyDamage => _state.Current.HasRallyDamage;
    /// <inheritdoc />
    public float ExplosionSkillRecoverySeconds => _state.Current.ExplosionSkillRecoverySeconds;
    /// <inheritdoc />
    public float MergeSupportChance => _state.Current.MergeSupportChance;
    /// <inheritdoc />
    public int ExtraCombatDroneCount => _state.Current.ExtraCombatDroneCount;
    /// <inheritdoc />
    public int OverflowManaPerCast => _state.Current.OverflowManaPerCast;
    /// <inheritdoc />
    public bool HasBombHacking => _state.Current.HasBombHacking;
    /// <inheritdoc />
    public int HackingStacksPerBomb => _state.Current.HackingStacksPerBomb;
    /// <inheritdoc />
    public float HackingCarryoverRatio => _state.Current.HackingCarryoverRatio;
    /// <inheritdoc />
    public float BonusManaChance => _state.Current.BonusManaChance;
    /// <inheritdoc />
    public int BonusManaCount => _state.Current.BonusManaCount;
    /// <inheritdoc />
    public IReadOnlyList<int> BonusAttackEveryNHits => _state.Current.BonusAttackEveryNHits;
    /// <inheritdoc />
    public IReadOnlyDictionary<int,float> UpgradeDiscounts => _state.Current.UpgradeDiscounts;
    /// <inheritdoc />
    public float GetRowAttackMultiplier(int row, int rows) => _state.Current.GetRowAttackMultiplier(row, rows);
    /// <inheritdoc />
    public float GetRowSpeedMultiplier(int row, int rows) => _state.Current.GetRowSpeedMultiplier(row, rows);
    /// <inheritdoc />
    public bool HasOverloadMerge => _state.Current.HasOverloadMerge;
    /// <inheritdoc />
    public float OverloadMergeCostSeconds => _state.Current.OverloadMergeCostSeconds;
    /// <inheritdoc />
    public float OverloadMergeUpgradeChance => _state.Current.OverloadMergeUpgradeChance;
    /// <inheritdoc />
    public int RecoveryHackingStacks => _state.Current.RecoveryHackingStacks;
    /// <inheritdoc />
    public DroneHackingData RecoveryHackingData => _state.Current.RecoveryHackingData;
    /// <inheritdoc />
    public SelfDestructDrone GlitchBombPrefab => _state.Current.GlitchBombPrefab;
    /// <inheritdoc />
    public float GlitchAttackBonus => _state.GlitchAttackBonus;
    internal void Publish()
    {
        if (OnEffectsChanged == null) return;
        foreach (Action handler in OnEffectsChanged.GetInvocationList())
            try { handler(); } catch (Exception error) { UnityEngine.Debug.LogException(error); }
    }
}
