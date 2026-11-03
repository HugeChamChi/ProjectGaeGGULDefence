using System;
using System.Collections.Generic;

[Serializable]
public sealed class UpgradeProgressData
{
    public int SchemaVersion = 1;
    public List<ResearchSaveData> Trees = new();
    public string LocalImportId = "";
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
