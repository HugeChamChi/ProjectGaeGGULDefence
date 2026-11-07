using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Run-owned copy supplied by the account boundary. Missing units start at level 1; preview saves are never read.</summary>
public sealed class UnitLevelSnapshot
{
    public static UnitLevelSnapshot FromAccount() => new UnitLevelSnapshot(
        Player.Characters.IsReady
            ? Player.Characters.Data.Characters.Select(entry => new KeyValuePair<string, int>(entry.Id, entry.Level))
            : Enumerable.Empty<KeyValuePair<string, int>>());

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
