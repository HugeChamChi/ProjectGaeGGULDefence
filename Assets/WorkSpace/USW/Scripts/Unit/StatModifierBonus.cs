using System;

public enum UnitBonusStat { AttackPercent, BasicAttackFrequencyPercent, CriticalDamagePoints }

/// <summary>Ratios use 0.05 for 5%; critical damage points use 0.5 for +50 percentage points.</summary>
[Serializable]
public sealed class StatModifierBonus : UnitLevelBonusPart
{
    public UnitBonusStat Stat;
    public float Value;
}
