using System;
using System.Collections.Generic;

[Serializable]
public sealed class UpgradeProgressData
{
    public int SchemaVersion = 1;
    public List<ResearchSaveData> Trees = new();
    public string LocalImportId = "";
    // Once copied, PlayerResearchData is retained only as a legacy source for older clients.
    public bool AccountSnapshotMigrated;
    public void Validate()
    {
        if (SchemaVersion != 1 || Trees == null) throw new FormatException("Unsupported upgrade save.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in Trees)
        {
            if (tree == null || string.IsNullOrWhiteSpace(tree.TreeKey) || !keys.Add(tree.TreeKey)) throw new FormatException("Duplicate/invalid upgrade tree.");
            tree.GetLevels(tree.TreeKey);
        }
    }
}
