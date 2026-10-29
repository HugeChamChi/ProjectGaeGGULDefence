using System;
using System.Collections.Generic;
using System.Globalization;
using LitJson;
using UnityEngine;

/// <summary>A single applied file contains complete tree metadata and nodes, avoiding mixed table versions.</summary>
public static class ResearchChartParser
{
    public static Dictionary<string, ResearchTreeData> Parse(JsonData rows)
    {
        if (rows == null || !rows.IsArray || rows.Count == 0 || rows.Count > 20000)
            throw new FormatException("ResearchBalance requires 1..20000 rows.");
        var trees = new Dictionary<string, ResearchTreeData>(StringComparer.Ordinal);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        string release = null;
        try
        {
            foreach (JsonData row in rows)
            {
                if (Int(row, "SchemaVersion", 1, 1) != 1) throw new FormatException("SchemaVersion");
                string version = Text(row, "ReleaseKey", true);
                if (release != null && release != version) throw new FormatException("Mixed ReleaseKey");
                release = version;
                string key = Text(row, "TreeKey", true);
                string name = Text(row, "TreeName", true), legacy = Text(row, "LegacySaveName");
                bool show = Bool(row, "ShowLanes");
                float spacing = Number(row, "ColumnSpacing");
                var lanes = Strings(row, "LaneNames");
                if (show && lanes.Count != 3) throw new FormatException("ShowLanes requires three lane names");
                string meta = JsonMapper.ToJson(new object[] {name, legacy, show, spacing, lanes.ToArray()});
                if (!trees.TryGetValue(key, out var tree))
                {
                    tree = ScriptableObject.CreateInstance<ResearchTreeData>();
                    trees.Add(key, tree);
                    metadata.Add(key, meta);
                    tree.name = key + "_" + release;
                    tree.TreeKey = key; tree.DisplayName = name; tree.LegacySaveName = legacy;
                    tree.ShowLanes = show; tree.ColumnSpacing = spacing; tree.LaneNames = lanes.ToArray();
                    tree.IsChartDefinition = true;
                }
                else if (metadata[key] != meta) throw new FormatException("Inconsistent tree metadata: " + key);
                var node = new ResearchNodeData {
                    Id = Text(row, "NodeId", true), Stat = EnumValue<ResearchStat>(row, "Stat"),
                    IconAddress = Text(row, "IconAddress", true), Shape = EnumValue<ResearchNodeShape>(row, "Shape"),
                    MaxLevel = Int(row, "MaxLevel", 1, 1000), ValuePerLevel = Number(row, "ValuePerLevel"),
                    Row = Int(row, "Row", 0, 200), Column = Int(row, "Column", -1, 2),
                    Parents = Strings(row, "Parents"), RequireAnyParent = Bool(row, "RequireAnyParent"),
                    RequiredTotalLevel = Int(row, "RequiredTotalLevel", 0, 20000000),
                    ExclusiveGroup = Text(row, "ExclusiveGroup"), RecommendOrder = Int(row, "RecommendOrder", 0, 20000000)
                };
                if (tree.Find(node.Id) != null) throw new FormatException("Duplicate node: " + key + "/" + node.Id);
                if (float.IsInfinity(node.ValuePerLevel * node.MaxLevel)) throw new FormatException("Value overflow");
                tree.Nodes.Add(node);
            }
            foreach (var tree in trees.Values)
            {
                var positions = new HashSet<string>();
                foreach (var node in tree.Nodes)
                {
                    if (!positions.Add(node.Row + ":" + node.Column)) throw new FormatException("Duplicate node position");
                    foreach (string parent in node.Parents)
                        if (tree.Find(parent) == null || parent == node.Id) throw new FormatException("Invalid parent: " + parent);
                }
                var visiting = new HashSet<string>(); var done = new HashSet<string>();
                foreach (var node in tree.Nodes) Visit(tree, node, visiting, done);
            }
            return trees;
        }
        catch { Destroy(trees); throw; }
    }

    private static void Visit(ResearchTreeData tree, ResearchNodeData node, HashSet<string> visiting, HashSet<string> done)
    {
        if (done.Contains(node.Id)) return;
        if (!visiting.Add(node.Id)) throw new FormatException("Cyclic research parents");
        foreach (string parent in node.Parents) Visit(tree, tree.Find(parent), visiting, done);
        visiting.Remove(node.Id); done.Add(node.Id);
    }

    public static void Destroy(Dictionary<string, ResearchTreeData> trees)
    {
        foreach (var tree in trees.Values)
            if (Application.isPlaying) UnityEngine.Object.Destroy(tree);
            else UnityEngine.Object.DestroyImmediate(tree);
        trees.Clear();
    }

    private static string Text(JsonData row, string key, bool required = false)
    {
        if (!row.IsObject || !row.ContainsKey(key) || row[key] == null) throw new FormatException("Missing column: " + key);
        string value = row[key].ToString();
        if (required && string.IsNullOrWhiteSpace(value)) throw new FormatException("Empty column: " + key);
        return value;
    }
    private static int Int(JsonData row, string key, int min, int max)
    {
        if (!int.TryParse(Text(row, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < min || value > max)
            throw new FormatException("Invalid integer: " + key);
        return value;
    }
    private static float Number(JsonData row, string key)
    {
        if (!float.TryParse(Text(row, key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            throw new FormatException("Invalid number: " + key);
        return value;
    }
    private static bool Bool(JsonData row, string key)
    {
        string value = Text(row, key);
        if (value == "true" || value == "True" || value == "1") return true;
        if (value == "false" || value == "False" || value == "0") return false;
        throw new FormatException("Invalid boolean: " + key);
    }
    private static T EnumValue<T>(JsonData row, string key) where T : struct
    {
        string text = Text(row, key, true);
        if (!Enum.TryParse<T>(text, out var value) || !Enum.IsDefined(typeof(T), value) || value.ToString() != text)
            throw new FormatException("Unsupported " + key + ": " + text);
        return value;
    }
    private static List<string> Strings(JsonData row, string key)
    {
        var json = JsonMapper.ToObject(Text(row, key));
        if (!json.IsArray) throw new FormatException("Expected JSON array: " + key);
        var result = new List<string>();
        foreach (JsonData entry in json)
        {
            if (entry == null || !entry.IsString || string.IsNullOrWhiteSpace((string)entry) || result.Contains((string)entry))
                throw new FormatException("Invalid list: " + key);
            result.Add((string)entry);
        }
        return result;
    }
}
