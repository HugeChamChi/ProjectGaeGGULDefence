using System.Collections.Generic;
using UnityEngine;

public readonly struct StatEvaluationStep
{
    public readonly string Stage;
    public readonly float Before, After;
    public StatEvaluationStep(string stage, float before, float after) { Stage = stage; Before = before; After = after; }
}

public sealed class StatEvaluation
{
    public int Value { get; }
    public bool Critical { get; }
    public StatModifierSnapshot Modifiers { get; }
    public IReadOnlyList<StatEvaluationStep> Steps { get; }
    public StatEvaluation(int value, bool critical, StatModifierSnapshot modifiers, List<StatEvaluationStep> steps)
    {
        Value = value;
        Critical = critical;
        Modifiers = modifiers;
        Steps = steps.AsReadOnly();
    }
}

/// <summary>서비스나 유닛을 조회하지 않는 수치 계산 경로. trace는 요청한 경우에만 할당한다.</summary>
public static class UnitStatCalculator
{
    public static float Value(StatModifierSnapshot snapshot, StatStage stage, float fallback = 0f) => snapshot.Value(stage, fallback);

    private static float Record(List<StatEvaluationStep> trace, string stage, float before, float after)
    {
        trace?.Add(new StatEvaluationStep(stage, before, after));
        return after;
    }

    public static float Attack(float basis, StatModifierSnapshot s, bool upgrade, List<StatEvaluationStep> trace = null)
    {
        float v = (basis * Value(s, StatStage.AccountAttackScale, 1) + Value(s, StatStage.LevelAttackFlat)) * AttackRatio(s);
        trace?.Add(new StatEvaluationStep("A1–A3 (original expression)", basis, v));
        if (upgrade) v = Record(trace, "UpgradeAttackScale", v, v * Value(s, StatStage.UpgradeAttackScale, 1));
        return v;
    }

    private static float AttackRatio(StatModifierSnapshot s) =>
        (1f + Value(s, StatStage.LevelAttackRatio)) * (1f + Value(s, StatStage.AlphanAttackRatio));

    public static float BasicMultiplier(StatModifierSnapshot s) =>
        (1f + Value(s, StatStage.BasicDamageRatio)) * Value(s, StatStage.RegularBasicScale, 1);

    public static int Damage(float basis, StatModifierSnapshot s, bool roll, bool attackBased,
        float damageVariance, float critVariance, double runScale, bool applyRunScale, out bool critical,
        List<StatEvaluationStep> trace = null)
    {
        critical = false;
        float v = (basis + Value(s, StatStage.DamageFlat)) * (1f + Value(s, StatStage.DamageRatio))
            * (1f - Value(s, StatStage.BetanAttackReduction)) * Value(s, StatStage.CellDamageScale, 1)
            * Value(s, StatStage.TotemAttackScale, 1) * Value(s, StatStage.RowAttackScale, 1)
            * Value(s, StatStage.ProjectileAttackScale, 1) * Value(s, StatStage.BurstAttackScale, 1)
            * Value(s, StatStage.CallerDamageScale, 1);
        trace?.Add(new StatEvaluationStep("D1–D7 (original expression)", basis, v));
        if (roll) v = Record(trace, "DamageSpread", v, v * DamageCalculator.Spread(damageVariance));
        float chance = Mathf.Clamp01(Value(s, StatStage.SelectionCriticalChance) + Value(s, StatStage.CriticalChance)
            + Value(s, StatStage.ResearchCriticalChance) + Value(s, StatStage.LevelCriticalChance));
        if (roll && Random.value < chance)
        {
            float cell = Value(s, StatStage.CriticalDamage) + Value(s, StatStage.LevelCriticalDamage);
            v = Record(trace, "Critical", v, v * ((Value(s, StatStage.SelectionCriticalDamage) + cell
                + Value(s, StatStage.ResearchCriticalDamage)) * DamageCalculator.Spread(critVariance)));
            critical = true;
        }
        if (attackBased)
        {
            v = Record(trace, "GlitchAttackRatio", v, v * (1f + Value(s, StatStage.GlitchAttackRatio) + Value(s, StatStage.SacrificeAttackRatio)));
            v = Record(trace, "ResearchAttackRatio", v, v * (1f + Value(s, StatStage.ResearchAttackRatio)));
            if (applyRunScale) v = Record(trace, "RunScale", v, RunStatMath.ScaleAttack(v, runScale));
        }
        return (int)Record(trace, "RoundAndMinimum", v, Mathf.Max(roll ? 1 : 0, DamageCalculator.ApplyRounding(v)));
    }

    public static float AttackInterval(float frequency, StatModifierSnapshot s, bool upgrade, double runScale,
        List<StatEvaluationStep> trace = null, float maximumSpeedMultiplier = float.PositiveInfinity)
    {
        float f = frequency + Value(s, StatStage.FrequencyFlat);
        if (upgrade) f *= Mathf.Max(Value(s, StatStage.UpgradeFrequencyScale, 1), .01f);
        f *= 1f + Value(s, StatStage.LevelFrequencyRatio);
        f *= 1f + Value(s, StatStage.UnitFrequencyRatio);
        f *= 1f + Value(s, StatStage.AlphanFrequencyRatio);
        f *= 1f + Value(s, StatStage.AcceleratedFrequencyRatio);
        float baseInterval = 1f / Mathf.Max(f, .01f);
        float speed = 1f / Mathf.Max(.1f, 1f + Value(s, StatStage.SpeedRatio));
        float cellSpeed = Value(s, StatStage.CellSpeedScale, 1);
        float interval = baseInterval * speed * Mathf.Min(1f, cellSpeed)
            * Value(s, StatStage.TotemSpeedScale, 1) / Mathf.Max(Value(s, StatStage.RowSpeedScale, 1), .01f);
        interval /= 1f + Value(s, StatStage.ResearchFrequencyRatio);
        float baseline = Mathf.Max(interval, .05f);
        float minimum = frequency > 0f ? 1f / (frequency * maximumSpeedMultiplier) : 0f;
        float result = Mathf.Max(RunStatMath.ScaleAttackInterval(baseline, runScale), minimum)
            * Mathf.Max(1f, cellSpeed) / Mathf.Max(.01f, Value(s, StatStage.PatternFrequencyScale, 1));
        trace?.Add(new StatEvaluationStep("AttackFrequency", frequency, f));
        trace?.Add(new StatEvaluationStep("AttackIntervalBeforeMinimum", baseInterval, interval));
        trace?.Add(new StatEvaluationStep("MinimumInterval", interval, baseline));
        trace?.Add(new StatEvaluationStep("RunAndPatternFrequency", baseline, result));
        return result;
    }

    public static float SkillInterval(float cooldown, StatModifierSnapshot s, List<StatEvaluationStep> trace = null)
    {
        float gauge = 1f / Mathf.Max(.1f, 1f + Value(s, StatStage.GaugeSpeedRatio));
        float v = Mathf.Max(0f, cooldown - Value(s, StatStage.SkillCooldownFlatReduction))
            * Mathf.Max(0f, 1f - Value(s, StatStage.SkillCooldownRatioReduction))
            * Value(s, StatStage.UnitSkillCooldownScale, 1)
            * gauge
            * Value(s, StatStage.CellSpeedScale, 1)
            / Mathf.Max(Value(s, StatStage.RowSpeedScale, 1), .01f);
        v *= Mathf.Max(0f, 1f - Value(s, StatStage.ResearchSkillCooldownRatioReduction));
        trace?.Add(new StatEvaluationStep("SkillCooldownBeforeMinimum", cooldown, v));
        return Record(trace, "MinimumSkillInterval", v, Mathf.Max(v, .05f));
    }
}
