using System;
using System.Collections.Generic;

/// <summary>Run-owned copy supplied by the account boundary. Missing units start at level 1; preview saves are never read.</summary>
public sealed class UnitLevelSnapshot
{
    private readonly Dictionary<string, int> levels;
    public UnitLevelSnapshot(IEnumerable<KeyValuePair<string, int>> source)
    {
        levels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1) throw new ArgumentException("Invalid unit level.");
            levels.Add(pair.Key, pair.Value);
        }
    }
    public int GetLevel(string unitKey) => unitKey != null && levels.TryGetValue(unitKey, out var level) ? level : 1;
}
