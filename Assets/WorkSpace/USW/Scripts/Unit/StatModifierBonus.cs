using System;

public enum UnitBonusStat { AttackPercent, BasicAttackFrequencyPercent, CriticalDamagePoints, AttackFlat, BasicAttackFrequencyFlat, CriticalChancePoints, SkillCooldownFlat, SkillCooldownPercent, FoodFlat, FoodPercent, DroneCountFlat, ChargeAttackReductionFlat, HackingCapacityFlat, HackingPostSpeedPercent, HackingPostDuration, HackingStackDamagePercent, RecoveryNextBasicPercent, TimeRecoveryPercent, BasicDamagePercent, FoodSpeedPercent, BossFoodSeconds, FoodFifthMultiplier, ChiefCooldownPercent, ChiefStartsReady, ChiefGlobalAttackPercent, ChiefSpeedPercent, ChiefSpeedDuration, ChiefDamageTakenPercent, ChiefDamageTakenDuration, HackingProducedNormal, HackingProducedRare, HackingProducedEpic, HackingProducedLegend, HackingConsumedNormal, HackingConsumedRare, HackingConsumedEpic, HackingConsumedLegend }

/// <summary>Ratios use 0.05 for 5%; critical damage points use 0.5 for +50 percentage points.</summary>
[Serializable]
public sealed class StatModifierBonus : UnitLevelBonusPart
{
    public UnitBonusStat Stat;
    public float Value;
}
