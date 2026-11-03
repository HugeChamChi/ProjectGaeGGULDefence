using System;
using System.Collections.Generic;

/// <summary>Per-unit, immutable unlocked values and independent action counters. No Unity lifecycle subscriptions.</summary>
public sealed class UnitLevelBonusController
{
    public const string BasicSkillCycle = "basic_skill";
    private readonly Dictionary<UnitBonusStat, float> stats = new();
    private readonly List<EveryNthCycleBonus> cycles = new();
    private readonly Dictionary<string, long> completed = new();
    public int Level { get; }

    public UnitLevelBonusController(IEnumerable<UnitLevelBonusDefinition> definitions, int level)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        Level = level;
        if (definitions == null) return;
        int foodMultiplierLevel = -1;
        foreach (var definition in definitions)
        {
            if (definition == null || definition.Level < 1) throw new ArgumentException("Invalid milestone.");
            if (definition.Level > level) continue;
            foreach (var part in definition.Parts)
            {
                if (part is StatModifierBonus stat)
                {
                    if (!Enum.IsDefined(typeof(UnitBonusStat), stat.Stat) || !Finite(stat.Value) || stat.Value < 0f)
                        throw new ArgumentException("Invalid stat bonus.");
                    if (stat.Stat == UnitBonusStat.FoodFifthMultiplier)
                    {
                        if (definition.Level >= foodMultiplierLevel)
                        { stats[stat.Stat] = stat.Value; foodMultiplierLevel = definition.Level; }
                    }
                    else stats[stat.Stat] = GetStat(stat.Stat) + stat.Value;
                }
                else if (part is EveryNthCycleBonus cycle)
                {
                    if (string.IsNullOrWhiteSpace(cycle.CycleId) || cycle.Every < 1 || !Finite(cycle.DamageBonusRatio)
                        || cycle.DamageBonusRatio < 0f || !Finite(cycle.ChargeTimeMultiplier) || cycle.ChargeTimeMultiplier <= 0f)
                        throw new ArgumentException("Invalid cycle bonus.");
                    cycles.Add(new EveryNthCycleBonus { CycleId = cycle.CycleId, Every = cycle.Every,
                        DamageBonusRatio = cycle.DamageBonusRatio, ChargeTimeMultiplier = cycle.ChargeTimeMultiplier });
                }
                else throw new ArgumentException("Unsupported level bonus part.");
            }
        }
    }

    public float GetStat(UnitBonusStat stat) => stats.TryGetValue(stat, out var value) ? value : 0f;
    public CycleBonus PeekCycle(string cycleId)
    {
        long number = (completed.TryGetValue(cycleId, out var count) ? count : 0) + 1;
        float damage = 0f, charge = 1f;
        foreach (var part in cycles)
            if (part.CycleId == cycleId && number % part.Every == 0)
            { damage += part.DamageBonusRatio; charge *= part.ChargeTimeMultiplier; }
        return new CycleBonus(number, 1f + damage, charge);
    }
    /// <summary>Called once when the base action commits, never by extra actions or their impacts.</summary>
    public CycleBonus CompleteCycle(string cycleId)
    {
        var result = PeekCycle(cycleId);
        completed[cycleId] = result.Number;
        return result;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public readonly struct CycleBonus
    {
        public readonly long Number;
        public readonly float DamageMultiplier, ChargeTimeMultiplier;
        public CycleBonus(long number, float damageMultiplier, float chargeTimeMultiplier)
        { Number = number; DamageMultiplier = damageMultiplier; ChargeTimeMultiplier = chargeTimeMultiplier; }
    }
}
