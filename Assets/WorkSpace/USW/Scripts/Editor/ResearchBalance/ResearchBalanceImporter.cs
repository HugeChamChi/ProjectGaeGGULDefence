using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Account_영구강화의 숫자만 검증하고 SO로 반영한다. 구조/참고/비용은 읽지 않는다.</summary>
public static class ResearchBalanceImporter
{
    /// <summary>기존 밸런스 통합 문서.</summary>
    public const string DefaultWorkbook = "outputs/balance-so-20260926/GameBalance.xlsx";
    /// <summary>강화 수치 탭.</summary>
    public const string SheetName = "Account_영구강화";
    /// <summary>기본 트리 에셋 위치.</summary>
    public const string DataFolder = "Assets/WorkSpace/USW/Data/OutgameUpgrade";
    /// <summary>스탯별 단위 원본.</summary>
    public static ResearchViewSettings DefaultSettings => AssetDatabase.LoadAssetAtPath<ResearchViewSettings>(DataFolder + "/UpgradeViewSettings.asset");

    /// <summary>검증된 숫자 행. 키로 찾아 위치 변경의 영향을 받지 않는다.</summary>
    public sealed class NodeValues
    {
        internal ResearchNodeData Target;
        internal int MaxLevel, RequiredTotalLevel, RecommendOrder;
        internal float Value;
        internal string Description;
    }
    /// <summary>트리 하나의 변경과 복원 스냅샷.</summary>
    public sealed class Change
    {
        internal ResearchTreeData Target;
        internal string BeforeJson;
        internal readonly List<NodeValues> Rows = new();
    }
    /// <summary>원본 워크북/SO/단위를 지문으로 고정한 미리보기.</summary>
    public sealed class Plan
    {
        internal string Path, Fingerprint;
        internal ResearchTreeData[] Trees;
        internal ResearchViewSettings Settings;
        internal readonly List<Change> Changes = new();
        /// <summary>검증한 데이터 행 수.</summary>
        public int RowCount { get; internal set; }
        /// <summary>변경될 SO 수.</summary>
        public int ChangeCount => Changes.Count;
        /// <summary>적용 전 사용자가 검토할 수치 차이.</summary>
        public string Summary { get; internal set; }
    }

    /// <summary>키/숫자/단위/전체 트리 행을 검증하고 변경 내역을 만든다. 에셋을 쓰지 않는다.</summary>
    public static Plan Prepare(string path, ResearchTreeData[] trees = null, ResearchViewSettings settings = null)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 뒤 Import하세요.");
        trees ??= AssetDatabase.FindAssets("t:ResearchTreeData", new[] { DataFolder }).Select(g =>
            AssetDatabase.LoadAssetAtPath<ResearchTreeData>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(t => t.TreeKey, StringComparer.Ordinal).ToArray();
        settings ??= DefaultSettings;
        if (settings == null || trees.Length == 0) throw new InvalidOperationException("트리 또는 스탯 표시 설정이 없습니다.");
        var stats = new Dictionary<ResearchStat, bool>();
        foreach (var stat in settings.Stats)
        {
            if (stat == null || stats.ContainsKey(stat.Stat)) throw new InvalidOperationException("스탯 단위 설정이 중복/누락되었습니다.");
            stats.Add(stat.Stat, stat.IsPercent);
        }
        byte[] bytes = File.ReadAllBytes(path);
        var table = BalanceXlsxTable.Read(bytes, SheetName, "TreeKey", "NodeId", "MaxLevel", "ValuePerLevelPct", "ValuePerLevelFlat", "RequiredTotalLevel", "RecommendOrder");
        var byKey = new Dictionary<string, ResearchTreeData>(StringComparer.Ordinal);
        var nodes = new Dictionary<string, ResearchNodeData>(StringComparer.Ordinal);
        var fingerprint = new StringBuilder(Hash(bytes)).Append(EditorJsonUtility.ToJson(settings));
        foreach (var tree in trees)
        {
            if (tree == null || !AssetDatabase.Contains(tree) || !BossBalanceRegistry.IsValidKey(tree.TreeKey) || byKey.ContainsKey(tree.TreeKey))
                throw new InvalidOperationException("저장된 트리와 고유한 TreeKey가 필요합니다.");
            if (EditorUtility.IsDirty(tree)) throw new InvalidOperationException(tree.name + ": SO를 저장한 뒤 다시 검증하세요.");
            byKey.Add(tree.TreeKey, tree);
            fingerprint.Append(AssetDatabase.GetAssetPath(tree)).Append(EditorJsonUtility.ToJson(tree));
            foreach (var node in tree.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || node.Id.Contains("\n") || !stats.ContainsKey(node.Stat))
                    throw new InvalidOperationException(tree.TreeKey + ": 노드/Id/스탯 단위 설정이 잘못되었습니다.");
                string key = tree.TreeKey + "\n" + node.Id;
                if (!nodes.TryAdd(key, node)) throw new InvalidOperationException("중복 SO 노드 키: " + key);
            }
        }
        var plan = new Plan { Path = path, Trees = trees.ToArray(), Settings = settings };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            string treeKey = row.Get("TreeKey"), id = row.Get("NodeId"), key = treeKey + "\n" + id;
            if (!nodes.TryGetValue(key, out var node)) throw row.Error("TreeKey/NodeId", "없는 키: " + treeKey + "/" + id);
            if (!seen.Add(key)) throw row.Error("TreeKey/NodeId", "중복 키: " + treeKey + "/" + id);
            int max = Integer(row, "MaxLevel", 1), required = Integer(row, "RequiredTotalLevel", 0), recommend = Integer(row, "RecommendOrder", 0);
            bool pct = stats[node.Stat];
            string field = pct ? "ValuePerLevelPct" : "ValuePerLevelFlat";
            if (!string.IsNullOrEmpty(row.Get(pct ? "ValuePerLevelFlat" : "ValuePerLevelPct")))
                throw row.Error(field, "스탯 단위에 맞는 값 열 하나만 입력하세요.");
            float value;
            if (pct) value = Integer(row, field, 0) / 100f;
            else if (!float.TryParse(row.Get(field), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw row.Error(field, "0 이상의 유한한 숫자가 필요합니다.");
            var differences = new List<string>();
            Difference(differences, "MaxLevel", node.MaxLevel, max);
            Difference(differences, "ValuePerLevel", node.ValuePerLevel, value);
            Difference(differences, "RequiredTotalLevel", node.RequiredTotalLevel, required);
            Difference(differences, "RecommendOrder", node.RecommendOrder, recommend);
            plan.RowCount++;
            if (differences.Count == 0) continue;
            var tree = byKey[treeKey];
            var change = plan.Changes.FirstOrDefault(c => c.Target == tree);
            if (change == null) plan.Changes.Add(change = new Change { Target = tree, BeforeJson = EditorJsonUtility.ToJson(tree) });
            change.Rows.Add(new NodeValues { Target = node, MaxLevel = max, RequiredTotalLevel = required,
                RecommendOrder = recommend, Value = value, Description = treeKey + "/" + id + ": " + string.Join(", ", differences) });
        }
        if (seen.Count != nodes.Count) throw new FormatException("SO의 일부 노드 행이 없습니다: " + string.Join(", ", nodes.Keys.Except(seen).Select(k => k.Replace('\n', '/'))));
        plan.Fingerprint = Hash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
        plan.Summary = $"{plan.RowCount}행 검증 / 변경 {plan.ChangeCount}개 SO\n" + string.Join("\n", plan.Changes.SelectMany(c => c.Rows).Select(r => r.Description));
        return plan;
    }

    /// <summary>미리보기와 동일한 원본만 백업 후 적용한다. 실패하면 메모리와 파일을 복원한다.</summary>
    public static string Apply(Plan preview) => ApplyCore(preview, tree => AssetDatabase.SaveAssetIfDirty(tree));

    internal static string ApplyCore(Plan preview, Action<ResearchTreeData> saveAsset)
    {
        if (preview == null) throw new ArgumentNullException(nameof(preview));
        var current = Prepare(preview.Path, preview.Trees, preview.Settings);
        if (current.Fingerprint != preview.Fingerprint) throw new InvalidOperationException("미리보기 이후 엑셀/SO/단위가 바뀌었습니다. 다시 검증하세요.");
        if (current.ChangeCount == 0) return "변경 없음";
        string backup = "outputs/research-import/backups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(backup);
        foreach (var change in current.Changes) File.Copy(AssetDatabase.GetAssetPath(change.Target), backup + "/" + change.Target.TreeKey + ".asset");
        File.WriteAllText(backup + "/changes.txt", current.Summary, Encoding.UTF8);
        File.WriteAllLines(backup + "/paths.txt", current.Changes.Select(c => c.Target.TreeKey + "=" + AssetDatabase.GetAssetPath(c.Target)));
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Import research balance workbook");
        Undo.RegisterCompleteObjectUndo(current.Changes.Select(c => (UnityEngine.Object)c.Target).ToArray(), "Import research balance workbook");
        try
        {
            foreach (var change in current.Changes)
            {
                foreach (var row in change.Rows)
                {
                    row.Target.MaxLevel = row.MaxLevel;
                    row.Target.ValuePerLevel = row.Value;
                    row.Target.RequiredTotalLevel = row.RequiredTotalLevel;
                    row.Target.RecommendOrder = row.RecommendOrder;
                }
                EditorUtility.SetDirty(change.Target);
                saveAsset(change.Target);
            }
            Undo.CollapseUndoOperations(group);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            foreach (var change in current.Changes)
            {
                EditorJsonUtility.FromJsonOverwrite(change.BeforeJson, change.Target);
                string path = AssetDatabase.GetAssetPath(change.Target);
                File.Copy(backup + "/" + change.Target.TreeKey + ".asset", path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            throw;
        }
        return backup;
    }
    private static int Integer(BalanceXlsxTable.Row row, string name, int min)
    {
        if (!int.TryParse(row.Get(name), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) || value < min)
            throw row.Error(name, min + " 이상의 정수가 필요합니다.");
        return value;
    }
    private static void Difference<T>(List<string> lines, string name, T before, T after)
    { if (!EqualityComparer<T>.Default.Equals(before, after)) lines.Add(name + ": " + before + " → " + after); }
    private static string Hash(byte[] bytes)
    { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(bytes)); }
}
