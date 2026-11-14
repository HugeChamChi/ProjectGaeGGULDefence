using System;
using System.Collections.Generic;

/// <summary>출처 전체를 교체하며, 이미 캡처한 계산 입력은 변경하지 않는다.</summary>
public sealed class StatModifierRegistry
{
    private readonly Dictionary<string, StatModifier[]> _sources = new Dictionary<string, StatModifier[]>();
    private readonly List<string> _order = new List<string>();
    private StatModifierSnapshot _snapshot;

    public void Clear() { _sources.Clear(); _order.Clear(); _snapshot = null; }

    public void ReplaceSource(string source, IEnumerable<StatModifier> modifiers)
    {
        if (string.IsNullOrEmpty(source)) throw new ArgumentException("A stable source key is required.", nameof(source));
        if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
        // Materialize before mutation so a failed enumeration preserves the previous source.
        var replacement = new List<StatModifier>();
        foreach (var modifier in modifiers) replacement.Add(modifier);
        ReplaceSource(source, replacement.ToArray());
    }

    public void ReplaceSource(string source, StatModifier[] modifiers)
    {
        if (string.IsNullOrEmpty(source)) throw new ArgumentException("A stable source key is required.", nameof(source));
        if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
        if (_sources.TryGetValue(source, out var previous) && Same(previous, modifiers)) return;
        if (previous == null) _order.Add(source);
        _sources[source] = (StatModifier[])modifiers.Clone();
        _snapshot = null;
    }

    private static bool Same(StatModifier[] a, StatModifier[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i].EffectId != b[i].EffectId || a[i].Stage != b[i].Stage || a[i].Operation != b[i].Operation || !a[i].Value.Equals(b[i].Value)) return false;
        return true;
    }

    public void RemoveSource(string source)
    {
        if (!_sources.Remove(source)) return;
        _order.Remove(source);
        _snapshot = null;
    }

    public StatModifierSnapshot Capture()
    {
        if (_snapshot != null) return _snapshot;
        var entries = new List<StatModifierEntry>();
        foreach (var source in _order)
            foreach (var modifier in _sources[source])
                entries.Add(new StatModifierEntry(source, modifier));
        return _snapshot = new StatModifierSnapshot(entries.ToArray());
    }
}

public enum ModifierOperation { Flat, AdditiveRatio, IndependentMultiplier }

// Stages are calculation boundaries, never provider-defined priorities.
public enum StatStage
{
    AccountAttackScale, LevelAttackFlat, LevelAttackRatio, AlphanAttackRatio,
    UpgradeAttackScale, DamageFlat, DamageRatio, BetanAttackReduction,
    CellDamageScale, TotemAttackScale, RowAttackScale, ProjectileAttackScale,
    BurstAttackScale, CallerDamageScale, GlitchAttackRatio, ResearchAttackRatio,
    CriticalChance, CriticalDamage,
    FrequencyFlat, UpgradeFrequencyScale, LevelFrequencyRatio, UnitFrequencyRatio,
    AlphanFrequencyRatio, AcceleratedFrequencyRatio, SpeedRatio, CellSpeedScale,
    TotemSpeedScale, RowSpeedScale, ResearchFrequencyRatio, PatternFrequencyScale,
    SkillCooldownFlatReduction, SkillCooldownRatioReduction, UnitSkillCooldownScale,
    GaugeSpeedRatio, ResearchSkillCooldownRatioReduction, SacrificeAttackRatio, BasicDamageRatio, RegularBasicScale, SelectionCriticalChance,
    SelectionCriticalDamage, LevelCriticalChance, ResearchCriticalChance, LevelCriticalDamage, ResearchCriticalDamage
}

public readonly struct StatModifier
{
    public readonly string EffectId;
    public readonly StatStage Stage;
    public readonly ModifierOperation Operation;
    public readonly float Value;

    public StatModifier(string effectId, StatStage stage, ModifierOperation operation, float value)
    {
        EffectId = effectId;
        Stage = stage;
        Operation = operation;
        Value = value;
    }
}

public readonly struct StatModifierEntry
{
    public readonly string Source;
    public readonly StatModifier Modifier;
    public StatModifierEntry(string source, StatModifier modifier) { Source = source; Modifier = modifier; }
}

public sealed class StatModifierSnapshot
{
    public IReadOnlyList<StatModifierEntry> Entries { get; }
    private static readonly int StageCount = Enum.GetValues(typeof(StatStage)).Length;
    private readonly float[] _sums = new float[StageCount];
    private readonly float[] _products = new float[StageCount];
    private readonly bool[] _present = new bool[StageCount];
    internal StatModifierSnapshot(StatModifierEntry[] entries)
    {
        Entries = Array.AsReadOnly(entries);
        for (int i = 0; i < StageCount; i++) _products[i] = 1f;
        foreach (var entry in entries)
        {
            var m = entry.Modifier;
            int index = (int)m.Stage;
            _present[index] = true;
            if (m.Operation == ModifierOperation.IndependentMultiplier) _products[index] *= m.Value;
            else _sums[index] += m.Value;
        }
    }
    public float Value(StatStage stage, float fallback = 0f)
    {
        int i = (int)stage;
        return !_present[i] ? fallback : fallback == 1f ? (1f + _sums[i]) * _products[i] : _sums[i] * _products[i];
    }
    public static StatModifierSnapshot Combine(StatModifierSnapshot first, StatModifierSnapshot second)
    {
        if (second.Entries.Count == 0) return first;
        var entries = new List<StatModifierEntry>(first.Entries);
        entries.AddRange(second.Entries);
        return new StatModifierSnapshot(entries.ToArray());
    }
}
