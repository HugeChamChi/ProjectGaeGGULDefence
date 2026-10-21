using System;
using System.Globalization;

/// <summary>Immutable per-run role configuration; contains no Unity object references.</summary>
public readonly struct EndlessBossProgressionSnapshot
{
    /// <summary>Round-one breather HP.</summary>
    public decimal BreatherBaseHp { get; }
    /// <summary>First round receiving one HP growth step.</summary>
    public int FirstGrowthRound { get; }
    /// <summary>Rounds between HP growth steps.</summary>
    public int IntervalRounds { get; }
    /// <summary>Hard encounter used to anchor the difficulty ratio.</summary>
    public int HardReferenceRound { get; }
    /// <summary>Breather encounter used to anchor the difficulty ratio.</summary>
    public int BreatherReferenceRound { get; }
    /// <summary>H(HardReferenceRound) / E(BreatherReferenceRound).</summary>
    public double HardReferenceMultiplier { get; }
    /// <summary>HP represented by one visual bar line.</summary>
    public decimal HpPerLine { get; }
    /// <summary>Base breather reduction before debuffs, as a percentage.</summary>
    public double BreatherReductionPercent { get; }
    /// <summary>Base hard reduction before debuffs, as a percentage.</summary>
    public double HardReductionPercent { get; }
    /// <summary>Frozen K from the actual combat defense settings.</summary>
    public double DefenseScale { get; }
    /// <summary>False means the growth input remains provisional.</summary>
    public bool GrowthRateFinalized { get; }

    /// <summary>Constructs explicit authoring or test inputs; callers must validate before use.</summary>
    public EndlessBossProgressionSnapshot(decimal baseHp, int first, int interval, int hardRound,
        int breatherRound, double hardMultiplier, decimal hpPerLine, double breatherReduction,
        double hardReduction, double defenseScale, bool growthFinalized)
    {
        BreatherBaseHp = baseHp; FirstGrowthRound = first; IntervalRounds = interval;
        HardReferenceRound = hardRound; BreatherReferenceRound = breatherRound;
        HardReferenceMultiplier = hardMultiplier; HpPerLine = hpPerLine;
        BreatherReductionPercent = breatherReduction; HardReductionPercent = hardReduction;
        DefenseScale = defenseScale; GrowthRateFinalized = growthFinalized;
    }

    /// <summary>Rejects missing or non-finite role settings rather than falling back to boss-specific HP.</summary>
    public bool TryValidate(out string error)
    {
        error = "Invalid endless boss progression: HP, growth timing, reference rounds, line size or defense settings.";
        if (BreatherBaseHp <= 0 || BreatherBaseHp > CombatHealth.MaximumHp || FirstGrowthRound < 2
            || IntervalRounds < 1 || HardReferenceRound < 1 || BreatherReferenceRound < HardReferenceRound
            || HpPerLine <= 0 || !Finite(HardReferenceMultiplier) || HardReferenceMultiplier < 1
            || !Reduction(BreatherReductionPercent) || !Reduction(HardReductionPercent)
            || !Finite(DefenseScale) || DefenseScale <= 0) return false;
        error = "";
        return true;
    }

    /// <summary>Number of steps at an actual one-based round, independent of shuffle order.</summary>
    public int GrowthSteps(int round) => round < FirstGrowthRound ? 0 : 1 + (round - FirstGrowthRound) / IntervalRounds;

    /// <summary>Inverts the existing logarithmic defense formula at neutral armor and damage modifiers.</summary>
    public double CalculateDefense(EndlessBossRole role)
    {
        double reduction = (role == EndlessBossRole.Hard ? HardReductionPercent : BreatherReductionPercent) / 100d;
        return DefenseScale * (Math.Exp(reduction / (1d - reduction)) - 1d);
    }

    /// <summary>Stable diagnostics for balance exports and per-run configuration hashes.</summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture,
        "role_hp={0};role_first={1};role_interval={2};hard_ref={3};breather_ref={4};hard_ratio={5:R};hp_per_line={6};breather_reduction={7:R};hard_reduction={8:R};defense_scale={9:R};growth_final={10}",
        BreatherBaseHp, FirstGrowthRound, IntervalRounds, HardReferenceRound, BreatherReferenceRound,
        HardReferenceMultiplier, HpPerLine, BreatherReductionPercent, HardReductionPercent, DefenseScale, GrowthRateFinalized);

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Reduction(double value) => Finite(value) && value >= 0 && value < 100;
}
