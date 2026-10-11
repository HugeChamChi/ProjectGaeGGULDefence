using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>활성 카드의 불변 계산 결과. 생성 후 쓰기 API나 가변 효과 정의를 노출하지 않는다.</summary>
public sealed class SelectionEffectSnapshot : ISelectionCombatReader, IDroneEffectReader, ISelectionEconomyReader, IChiefSelectionReader
{
    /// <summary>AttackBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float AttackBonus { get; private set; } = 0f;
    /// <summary>AttackSpeedBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float AttackSpeedBonus { get; private set; } = 0f;
    /// <summary>TotemEfficiencyBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float TotemEfficiencyBonus { get; private set; } = 0f;
    /// <summary>CritChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float CritChance { get; private set; } = 0f;
    /// <summary>CritDamageMultiplier의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float CritDamageMultiplier { get; private set; } = 1.5f;
    /// <summary>FoodSpeedBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FoodSpeedBonus { get; private set; } = 0f;
    /// <summary>ProjectileSizeBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ProjectileSizeBonus { get; private set; } = 0f;
    /// <summary>GaugeSpeedBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float GaugeSpeedBonus { get; private set; } = 0f;
    /// <summary>ExpGainMultiplier의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ExpGainMultiplier { get; private set; } = 1f;
    /// <summary>FrontAttackBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FrontAttackBonus { get; private set; } = 0f;
    /// <summary>BackAttackBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BackAttackBonus { get; private set; } = 0f;
    /// <summary>FrontSpeedMultiplier의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FrontSpeedMultiplier { get; private set; } = 1f;
    /// <summary>BackSpeedMultiplier의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BackSpeedMultiplier { get; private set; } = 1f;
    /// <summary>RandomExtraAttackChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float RandomExtraAttackChance { get; private set; } = 0f;
    /// <summary>HasRandomProcAttack의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasRandomProcAttack { get; private set; } = false;
    /// <summary>RandomProcChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float RandomProcChance { get; private set; } = 0f;
    /// <summary>RandomProcDamagePct의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float RandomProcDamagePct { get; private set; } = 0f;
    /// <summary>HasExtraAttackEveryAttack의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasExtraAttackEveryAttack { get; private set; } = false;
    /// <summary>HasExtraAttackOnSkillFull의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasExtraAttackOnSkillFull { get; private set; } = false;
    /// <summary>HasBurstOnSkillFull의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasBurstOnSkillFull { get; private set; } = false;
    /// <summary>BurstAttackBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BurstAttackBonus { get; private set; } = 0f;
    /// <summary>BurstDurationSeconds의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BurstDurationSeconds { get; private set; } = 0f;
    /// <summary>SummonDiscountRate의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SummonDiscountRate { get; private set; } = 0f;
    /// <summary>SummonFixedDiscountAmount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SummonFixedDiscountAmount { get; private set; } = 0f;
    /// <summary>SellBonusFoodAmount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SellBonusFoodAmount { get; private set; } = 0f;
    /// <summary>HasSummonDealsDamage의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasSummonDealsDamage { get; private set; } = false;
    /// <summary>SummonDamagePct의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SummonDamagePct { get; private set; } = 0f;
    /// <summary>HasSellDealsDamage의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasSellDealsDamage { get; private set; } = false;
    /// <summary>SellDamagePct의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SellDamagePct { get; private set; } = 0f;
    /// <summary>HasSellGivesRandomUnit의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasSellGivesRandomUnit { get; private set; } = false;
    /// <summary>SellGivesUnitChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SellGivesUnitChance { get; private set; } = 0f;
    /// <summary>HasMergeKeepsTribe의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasMergeKeepsTribe { get; private set; } = false;
    /// <summary>HasAllowTotemOverlap의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasAllowTotemOverlap { get; private set; } = false;
    /// <summary>HasProjectileSizeScalesAtk의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasProjectileSizeScalesAtk { get; private set; } = false;
    /// <summary>ProjectileSizeAtkPerUnit의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ProjectileSizeAtkPerUnit { get; private set; } = 0f;
    /// <summary>PeriodicBombInterval의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float PeriodicBombInterval { get; private set; } = 0f;
    /// <summary>PeriodicBombCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int PeriodicBombCount { get; private set; } = 0;
    /// <summary>FleetBombInterval의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FleetBombInterval { get; private set; } = 0f;
    /// <summary>FleetBombCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int FleetBombCount { get; private set; } = 0;
    /// <summary>DroneAttackSpeedBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float DroneAttackSpeedBonus { get; private set; } = 0f;
    /// <summary>StackSkillReduction의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float StackSkillReduction { get; private set; } = 0f;
    /// <summary>MaximumStackSkillReduction의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float MaximumStackSkillReduction { get; private set; } = 0f;
    /// <summary>HasEmptyStackDamage의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasEmptyStackDamage { get; private set; } = false;
    /// <summary>HasFoodPayoutAnimation의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasFoodPayoutAnimation { get; private set; } = false;
    /// <summary>FoodPayoutInterval의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FoodPayoutInterval { get; private set; } = 1f;
    /// <summary>FoodProductionBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FoodProductionBonus { get; private set; } = 0f;
    /// <summary>FleetFoodProductionBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FleetFoodProductionBonus { get; private set; } = 0f;
    /// <summary>HackingProductionFlat의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int HackingProductionFlat { get; private set; } = 0;
    /// <summary>HackingProductionBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float HackingProductionBonus { get; private set; } = 0f;
    /// <summary>ManaRequirementReduction의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ManaRequirementReduction { get; private set; } = 0f;
    /// <summary>마나 감소 기능의 활성 여부.</summary>
    public bool HasManaRequirementReduction { get; private set; }
    /// <summary>ChiefCooldownReduction의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ChiefCooldownReduction { get; private set; } = 0f;
    /// <summary>ChiefVolleyDelay의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ChiefVolleyDelay { get; private set; } = 0f;
    /// <summary>ChiefVolleyDamageRatio의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ChiefVolleyDamageRatio { get; private set; } = 0f;
    /// <summary>ChiefVolleyCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int ChiefVolleyCount { get; private set; } = 0;
    /// <summary>RallyDamageBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float RallyDamageBonus { get; private set; } = 0f;
    /// <summary>집결 피해 배율 정의의 활성 여부. 기존 0배 저작도 보존한다.</summary>
    public bool HasRallyDamage { get; private set; }
    /// <summary>ExplosionSkillRecoverySeconds의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ExplosionSkillRecoverySeconds { get; private set; } = 0f;
    /// <summary>MergeSupportChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float MergeSupportChance { get; private set; } = 0f;
    /// <summary>ExtraCombatDroneCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int ExtraCombatDroneCount { get; private set; } = 0;
    /// <summary>OverflowManaPerStack의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int OverflowManaPerStack { get; private set; } = 0;
    /// <summary>HasBombHacking의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasBombHacking { get; private set; } = false;
    /// <summary>HackingStacksPerBomb의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int HackingStacksPerBomb { get; private set; } = 0;
    /// <summary>HackingCarryoverRatio의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float HackingCarryoverRatio { get; private set; } = 0f;
    /// <summary>BonusManaChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BonusManaChance { get; private set; } = 0f;
    /// <summary>BonusManaCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int BonusManaCount { get; private set; } = 0;
    private readonly List<int> _hitPeriods = new();
    private readonly Dictionary<int,float> _upgradeDiscounts = new();
    private readonly HashSet<Type> _singleConfigurations = new();
    /// <summary>공격 횟수 트리거의 읽기 전용 목록.</summary>
    public IReadOnlyList<int> BonusAttackEveryNHits { get; }
    /// <summary>출처별 강화 할인. 환급 이력은 포함하지 않는다.</summary>
    public IReadOnlyDictionary<int,float> UpgradeDiscounts { get; }
    internal SelectionEffectSnapshot(IEnumerable<SelectionCardSnapshot> cards, float baseCritChance)
    {
        Finite(baseCritChance); CritChance=Mathf.Clamp01(baseCritChance);
        BonusAttackEveryNHits=_hitPeriods.AsReadOnly();
        UpgradeDiscounts=new ReadOnlyDictionary<int,float>(_upgradeDiscounts);
        var ids=new HashSet<int>(); var groups=new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            if (card == null || !ids.Add(card.CardId)) throw new ArgumentException("Null or duplicate active card.");
            foreach (var group in card.ExclusiveGroups) if (!groups.Add(group)) throw new ArgumentException("Exclusive selection group conflict: " + group);
            foreach (var effect in card.CopyDefinition().Effects)
            {
                switch (effect)
                {
                    case StatModifierDefinition d: ApplyStat(d); break;
                    case UpgradeDiscountDefinition d:
                        if (_upgradeDiscounts.ContainsKey(card.CardId)) throw new ArgumentException("Multiple upgrade discounts on one card.");
                        _upgradeDiscounts.Add(card.CardId,d.Ratio); break;
                    case PeriodicBombDefinition d: Once(effect); PeriodicBombInterval=d.PeriodSeconds; PeriodicBombCount=d.BombCount; break;
                    case DroneAttackSpeedDefinition d: DroneAttackSpeedBonus+=d.BonusRatio; break;
                    case FleetBombDefinition d: Once(effect); FleetBombInterval=d.PeriodSeconds; FleetBombCount=d.BombCount; break;
                    case StackSkillFrequencyDefinition d: Once(effect); StackSkillReduction=d.ReductionPerStack; MaximumStackSkillReduction=d.MaximumReduction; break;
                    case EmptyStackDamageDefinition d: HasEmptyStackDamage=true; break;
                    case FoodPayoutDefinition d: Once(effect); HasFoodPayoutAnimation=true; FoodPayoutInterval=d.PeriodSeconds; FoodProductionBonus+=d.BonusRatio; break;
                    case HackingProductionFlatDefinition d: HackingProductionFlat=checked(HackingProductionFlat+d.StackCount); break;
                    case HackingProductionRollDefinition d: if (d.MinimumRatio != d.MaximumRatio) throw new ArgumentException("Production roll must be resolved by Prepare."); HackingProductionBonus+=d.MinimumRatio; break;
                    case ManaRequirementDefinition d: Once(effect); HasManaRequirementReduction=true; ManaRequirementReduction=d.ReductionRatio; break;
                    case ChiefCooldownDefinition d: ChiefCooldownReduction+=d.ReductionRatio; break;
                    case ChiefVolleyDefinition d: Once(effect); ChiefVolleyDelay=d.ShotDelaySeconds; ChiefVolleyDamageRatio=d.DamageRatio; ChiefVolleyCount=d.ShotCount; break;
                    case FoodProductionDefinition d: FoodProductionBonus+=d.BonusRatio; break;
                    case FleetFoodProductionDefinition d: FleetFoodProductionBonus+=d.BonusRatio; break;
                    case RallyDamageDefinition d: HasRallyDamage=true; RallyDamageBonus+=d.BonusRatio; break;
                    case ExplosionSkillRecoveryDefinition d: ExplosionSkillRecoverySeconds+=d.Seconds; break;
                    case MergeSupportDefinition d: MergeSupportChance+=d.Chance; break;
                    case CombatDroneCapacityDefinition d: ExtraCombatDroneCount=checked(ExtraCombatDroneCount+d.Count); break;
                    case OverflowManaDefinition d: Once(effect); OverflowManaPerStack=d.ManaPerStack; break;
                    case BombHackingDefinition d: Once(effect); HasBombHacking=true; HackingStacksPerBomb=d.StacksPerBomb; break;
                    case HackingCarryoverDefinition d: Once(effect); HackingCarryoverRatio=d.Ratio; break;
                    case BonusManaDefinition d: Once(effect); BonusManaChance=d.Chance; BonusManaCount=d.ManaCount; break;
                    case EveryHitsAttackDefinition d: _hitPeriods.Add(d.HitCount); break;
                    case RandomExtraAttackDefinition d: RandomExtraAttackChance+=d.Chance; break;
                    case RandomProcAttackDefinition d: HasRandomProcAttack=true; RandomProcChance+=d.Chance; RandomProcDamagePct=d.DamageRatio; break;
                    case ExtraAttackEveryAttackDefinition d: HasExtraAttackEveryAttack=true; break;
                    case ExtraAttackOnSkillDefinition d: HasExtraAttackOnSkillFull=true; break;
                    case SkillBurstDefinition d: HasBurstOnSkillFull=true; BurstAttackBonus=Mathf.Max(BurstAttackBonus,d.AttackBonus); BurstDurationSeconds=Mathf.Max(BurstDurationSeconds,d.DurationSeconds); break;
                    case SummonDiscountDefinition d: SummonDiscountRate+=d.Ratio; break;
                    case SummonFixedDiscountDefinition d: SummonFixedDiscountAmount+=d.Amount; break;
                    case SellFoodBonusDefinition d: SellBonusFoodAmount+=d.Amount; break;
                    case SummonDamageDefinition d: HasSummonDealsDamage=true; SummonDamagePct+=d.DamageRatio; break;
                    case SellDamageDefinition d: HasSellDealsDamage=true; SellDamagePct+=d.DamageRatio; break;
                    case SellRandomUnitDefinition d: HasSellGivesRandomUnit=true; SellGivesUnitChance+=d.Chance; break;
                    case TotemOverlapDefinition d: HasAllowTotemOverlap=true; break;
                    case MergeKeepsTribeDefinition d: HasMergeKeepsTribe=true; break;
                    case ProjectileAttackScaleDefinition d: HasProjectileSizeScalesAtk=true; ProjectileSizeAtkPerUnit=d.AttackPerSizeStep; break;
                    default: throw new ArgumentException("Unsupported effect projection.");
                }
            }
        }
        Finite(AttackBonus);
        Finite(AttackSpeedBonus);
        Finite(TotemEfficiencyBonus);
        Finite(CritChance);
        Finite(CritDamageMultiplier);
        Finite(FoodSpeedBonus);
        Finite(ProjectileSizeBonus);
        Finite(GaugeSpeedBonus);
        Finite(ExpGainMultiplier);
        Finite(FrontAttackBonus);
        Finite(BackAttackBonus);
        Finite(FrontSpeedMultiplier);
        Finite(BackSpeedMultiplier);
        Finite(RandomExtraAttackChance);
        Finite(RandomProcChance);
        Finite(RandomProcDamagePct);
        Finite(BurstAttackBonus);
        Finite(BurstDurationSeconds);
        Finite(SummonDiscountRate);
        Finite(SummonFixedDiscountAmount);
        Finite(SellBonusFoodAmount);
        Finite(SummonDamagePct);
        Finite(SellDamagePct);
        Finite(SellGivesUnitChance);
        Finite(ProjectileSizeAtkPerUnit);
        Finite(PeriodicBombInterval);
        Finite(FleetBombInterval);
        Finite(DroneAttackSpeedBonus);
        Finite(StackSkillReduction);
        Finite(MaximumStackSkillReduction);
        Finite(FoodPayoutInterval);
        Finite(FoodProductionBonus);
        Finite(FleetFoodProductionBonus);
        Finite(HackingProductionBonus);
        Finite(ManaRequirementReduction);
        Finite(ChiefCooldownReduction);
        Finite(ChiefVolleyDelay);
        Finite(ChiefVolleyDamageRatio);
        Finite(RallyDamageBonus);
        Finite(ExplosionSkillRecoverySeconds);
        Finite(MergeSupportChance);
        Finite(HackingCarryoverRatio);
        Finite(BonusManaChance);
        CritChance=Mathf.Clamp01(CritChance);
    }
    /// <summary>전방/후방 공격 가산을 기존 행 규칙으로 평가한다.</summary>
    public float GetRowAttackMultiplier(int row, int rows)
    {
        if (row<0 || row>=rows) return 1f;
        return 1f + (row<2 ? FrontAttackBonus:0f) + (rows>=2 && row==rows-1 || rows>=3 && row==rows-2 ? BackAttackBonus:0f);
    }
    /// <summary>전방/후방 승산 보정을 기존 행 규칙으로 평가한다.</summary>
    public float GetRowSpeedMultiplier(int row, int rows)
    {
        if (row<0 || row>=rows) return 1f;
        return (row<2 ? FrontSpeedMultiplier:1f) * (rows>=2 && row==rows-1 || rows>=3 && row==rows-2 ? BackSpeedMultiplier:1f);
    }
    private void ApplyStat(StatModifierDefinition d)
    {
        switch(d.Stat)
        {
            case SelectionStat.Attack: AttackBonus+=d.Ratio; break;
            case SelectionStat.AttackSpeed: AttackSpeedBonus+=d.Ratio; break;
            case SelectionStat.TotemEfficiency: TotemEfficiencyBonus+=d.Ratio; break;
            case SelectionStat.CritChance: CritChance+=d.Ratio; break;
            case SelectionStat.CritDamage: CritDamageMultiplier+=d.Ratio; break;
            case SelectionStat.FoodSpeed: FoodSpeedBonus+=d.Ratio; break;
            case SelectionStat.ProjectileSize: ProjectileSizeBonus+=d.Ratio; break;
            case SelectionStat.GaugeSpeed: GaugeSpeedBonus+=d.Ratio; break;
            case SelectionStat.Experience: ExpGainMultiplier*=1f+d.Ratio; break;
            case SelectionStat.FrontRowAttack: FrontAttackBonus+=d.Ratio; break;
            case SelectionStat.BackRowAttack: BackAttackBonus+=d.Ratio; break;
            case SelectionStat.FrontRowSpeed: FrontSpeedMultiplier*=1f+d.Ratio; break;
            case SelectionStat.BackRowSpeed: BackSpeedMultiplier*=1f+d.Ratio; break;
            default: throw new ArgumentException("Unknown stat projection.");
        }
    }
    private void Once(SelectionEffectDefinition definition) { if (!_singleConfigurations.Add(definition.GetType())) throw new ArgumentException("Conflicting non-additive feature configuration: " + definition.GetType().Name); }
    private static void Finite(float value) { if(float.IsNaN(value)||float.IsInfinity(value)) throw new ArgumentException("Selection aggregation is not finite."); }
}
