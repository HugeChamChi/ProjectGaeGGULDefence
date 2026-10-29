using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LitJson;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class ResearchChartAuthoring
{
    private const string ResourcePath = "Assets/WorkSpace/USW/Resources/ResearchBalance.json";

    public static string ApplyBundled(string relativeFile)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before applying the bundled chart");
        string root=Path.GetFullPath(".")+Path.DirectorySeparatorChar;
        string file=Path.GetFullPath(relativeFile);
        if (!file.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("File must be inside the project");
        string json=File.ReadAllText(file);
        var trees=ResearchChartParser.Parse(JsonMapper.ToObject(json));
        ResearchChartParser.Destroy(trees);
        File.WriteAllText(ResourcePath,json);
        AssetDatabase.ImportAsset(ResourcePath);
        return ResourcePath;
    }

    // Explicit one-time migration. Does not replace existing snapshots or change an existing remote toggle.
    public static string Prepare()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing research assets");
        if (File.Exists(ResourcePath)) throw new InvalidOperationException("Research snapshot already exists; use an explicit new export");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings missing");
        var rows = new List<Dictionary<string, object>>();
        var addresses = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ResearchTreeData", new[] { "Assets/WorkSpace/USW/Data/OutgameUpgrade" }).OrderBy(x => x))
        {
            var tree = AssetDatabase.LoadAssetAtPath<ResearchTreeData>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var node in tree.Nodes)
            {
                if (node.Icon == null) throw new InvalidOperationException("Missing icon: " + tree.TreeKey + "/" + node.Id);
                string iconPath = AssetDatabase.GetAssetPath(node.Icon);
                string iconGuid = AssetDatabase.AssetPathToGUID(iconPath);
                var entry = settings.FindAssetEntry(iconGuid, true);
                if (entry == null)
                {
                    var group = settings.FindGroup("ResearchIcons");
                    if (group == null)
                    {
                        group = settings.CreateGroup("ResearchIcons", false, false, false, null,
                            typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                        var schema = group.GetSchema<BundledAssetGroupSchema>();
                        schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                        schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                    }
                    entry = settings.CreateOrMoveEntry(iconGuid, group);
                    entry.address = "Research/Icons/" + iconGuid;
                }
                string address = entry.address + "[" + node.Icon.name + "]";
                addresses.Add(address);
                rows.Add(new Dictionary<string, object> {
                    {"SchemaVersion",1}, {"ReleaseKey","research-initial-20261025"},
                    {"TreeKey",tree.TreeKey}, {"TreeName",tree.DisplayName}, {"LegacySaveName",tree.LegacySaveName ?? ""},
                    {"ShowLanes",tree.ShowLanes ? "true" : "false"}, {"ColumnSpacing",tree.ColumnSpacing},
                    {"LaneNames",JsonMapper.ToJson((tree.LaneNames ?? new string[0]).Where(x => !string.IsNullOrEmpty(x)).ToArray())},
                    {"NodeId",node.Id}, {"Stat",node.Stat.ToString()}, {"IconAddress",address}, {"Shape",node.Shape.ToString()},
                    {"MaxLevel",node.MaxLevel}, {"ValuePerLevel",node.ValuePerLevel}, {"Row",node.Row}, {"Column",node.Column},
                    {"Parents",JsonMapper.ToJson(node.Parents)}, {"RequireAnyParent",node.RequireAnyParent ? "true" : "false"},
                    {"RequiredTotalLevel",node.RequiredTotalLevel}, {"ExclusiveGroup",node.ExclusiveGroup ?? ""}, {"RecommendOrder",node.RecommendOrder}
                });
            }
        }
        string json = JsonMapper.ToJson(rows);
        var parsed = ResearchChartParser.Parse(JsonMapper.ToObject(json));
        int treeCount = parsed.Count;
        ResearchChartParser.Destroy(parsed);
        File.WriteAllText(ResourcePath, json);
        AssetDatabase.ImportAsset(ResourcePath);
        const string configPath = "Assets/WorkSpace/USW/Resources/ResearchChartConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<ResearchChartConfig>(configPath);
        if (config == null) { config = ScriptableObject.CreateInstance<ResearchChartConfig>(); AssetDatabase.CreateAsset(config, configPath); }
        config.BundledChart = AssetDatabase.LoadAssetAtPath<TextAsset>(ResourcePath);
        EditorUtility.SetDirty(config); EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return JsonMapper.ToJson(new { trees = treeCount, nodes = rows.Count, icons = addresses.Count, useRemote = config.UseRemote, source = ResourcePath });
    }
}
