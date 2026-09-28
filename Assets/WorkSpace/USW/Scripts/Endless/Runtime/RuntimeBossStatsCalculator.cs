using System;

/// <summary>Pure growth calculation; never mutates authored assets.</summary>
public static class RuntimeBossStatsCalculator
{
    /// <summary>Fixed-slot preview only. Live shuffled runs pass their selected definition to the BossData overload.</summary>
    public static bool TryCalculate(EndlessModeData mode, int round, out RuntimeBossStats stats, out string error)
    {
        stats = default;
        error = "Invalid endless round or growth configuration.";
        if (mode == null || round < 1 || mode.CycleLength < 1 || mode.Growth == null || mode.Slots == null) return false;
        int position = (round - 1) % mode.CycleLength + 1;
        BossData source = null;
        foreach (var slot in mode.Slots)
            if (slot != null && slot.Position == position) { source = slot.Boss; break; }
        if (source == null) { error = $"Missing boss at position {position}."; return false; }

        return TryCalculate(source, mode.Growth, round, out stats, out error);
    }

    /// <summary>Applies round growth to an explicitly selected fixed or shuffled boss definition.</summary>
    public static bool TryCalculate(BossData source, EndlessGrowthSettings growth, int round, out RuntimeBossStats stats, out string error)
    {
        stats = default;
        error = "Invalid selected boss or growth configuration.";
        if (source == null || round < 1 || growth == null || !growth.IsConfigured
            || growth.FirstGrowthRound < 1 || growth.IntervalRounds < 1
            || !NonnegativeFinite(growth.HpPercentPerStep) || !NonnegativeFinite(growth.DefenseAddedPerStep)
            || !NonnegativeFinite(growth.ExpPercentPerStep)) return false;

        int steps = round < growth.FirstGrowthRound ? 0 : 1 + (round - growth.FirstGrowthRound) / growth.IntervalRounds;
        double hpFactor = Math.Pow(1d + growth.HpPercentPerStep / 100d, steps);
        double defense = source.Defense + growth.DefenseAddedPerStep * steps;
        double exp = source.ExpReward * Math.Pow(1d + growth.ExpPercentPerStep / 100d, steps);
        if (!NonnegativeFinite(hpFactor) || hpFactor > (double)decimal.MaxValue
            || !NonnegativeFinite(defense) || !NonnegativeFinite(exp) || exp > float.MaxValue)
        { error = $"Round {round}: boss growth exceeds numeric range."; return false; }

        decimal hp;
        try { hp = checked(source.MaxHp * (decimal)hpFactor); }
        catch (OverflowException) { error = $"Round {round}: HP growth exceeds decimal range."; return false; }
        stats = new RuntimeBossStats(source, hp, defense, (float)exp, source.HpLineCount);
        return TryValidate(stats, out error);
    }

    /// <summary>Checks actual HP precision and independently authored total EXP before spawning.</summary>
    public static bool TryValidate(RuntimeBossStats stats, out string error)
    {
        error = "Invalid runtime boss: prefab, HP, defense, EXP or HP-line count.";
        if (stats.Source == null || stats.Source.Prefab == null || stats.MaxHp <= 0m
            || stats.MaxHp > CombatHealth.MaximumHp || !NonnegativeFinite(stats.Defense)
            || !NonnegativeFinite(stats.ExpReward) || stats.HpLineCount < 1) return false;
        decimal actualHp = CombatHealth.ToUnits(stats.MaxHp) / (decimal)CombatHealth.Scale;
        if (actualHp <= 0m) { error = "Boss HP is below CombatHealth precision."; return false; }
        float multiplier = stats.ExpReward / (float)actualHp;
        if (!NonnegativeFinite(multiplier) || (stats.ExpReward > 0f && multiplier == 0f))
        { error = "Boss EXP per actual HP exceeds float precision/range."; return false; }
        error = "";
        return true;
    }

    private static bool NonnegativeFinite(double value) => value >= 0d && !double.IsInfinity(value) && !double.IsNaN(value);
}
