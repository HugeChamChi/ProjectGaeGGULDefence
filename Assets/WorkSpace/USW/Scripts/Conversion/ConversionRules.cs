using GGD.BackendCharts;
using UnityEngine;

/// <summary>Stones granted for one boss kill and whether it counts as a fast kill (toast wording).</summary>
public readonly struct ConversionKillReward
{
    public readonly int Stones;
    public readonly bool Fast;
    public ConversionKillReward(int stones, bool fast) { Stones = stones; Fast = fast; }
}

/// <summary>
/// Conversion stone numbers (user design 2026-11-14). Values come from the RunBalance chart and fall back to the
/// bundled GameConfig fields in the tutorial and direct Editor play.
/// Hard boss: within 20 s +2 (fast), within 40 s +1, later 0. Breather boss: within 20 s +1 (fast), later 0.
/// Cost: tier number (Normal 1, Rare 2, Epic 3, Legend 4) times the per-tier value.
/// </summary>
public static class ConversionRules
{
    /// <summary>Reward for a defeated boss of <paramref name="role"/> killed in <paramref name="combatSeconds"/>.</summary>
    public static ConversionKillReward ForKill(GameConfig config, EndlessBossRole role, double combatSeconds)
    {
        if (config == null) return default;
        switch (role)
        {
            case EndlessBossRole.Hard:
                if (combatSeconds <= Read(RunBalance.ConversionHardFastSeconds, config.conversionHardFastSeconds))
                    return new ConversionKillReward(Count(RunBalance.ConversionHardFastStones, config.conversionHardFastStones), true);
                if (combatSeconds <= Read(RunBalance.ConversionHardSeconds, config.conversionHardSeconds))
                    return new ConversionKillReward(Count(RunBalance.ConversionHardStones, config.conversionHardStones), false);
                break;
            case EndlessBossRole.Breather:
                if (combatSeconds <= Read(RunBalance.ConversionBreatherFastSeconds, config.conversionBreatherFastSeconds))
                    return new ConversionKillReward(Count(RunBalance.ConversionBreatherFastStones, config.conversionBreatherFastStones), true);
                break;
        }
        return default;
    }

    /// <summary>Stones needed to convert a unit of <paramref name="tier"/>; -1 for a non-combat tier.</summary>
    public static int Cost(GameConfig config, Tier tier)
    {
        if (config == null || tier < Tier.Normal || tier > Tier.Legend) return -1;
        return ((int)tier + 1) * Count(RunBalance.ConversionCostPerTier, config.conversionCostPerTier);
    }

    private static float Read(string key, float bundled) => RunBalance.Number(key, bundled);
    private static int Count(string key, float bundled) => Mathf.Max(0, Mathf.RoundToInt(RunBalance.Number(key, bundled)));
}
