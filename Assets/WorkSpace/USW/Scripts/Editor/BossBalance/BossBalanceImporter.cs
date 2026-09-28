using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>엑셀 입력 전체 검증, 변경 미리보기와 기존 BossData의 일괄 갱신.</summary>
public static class BossBalanceImporter
{
    /// <summary>프로젝트의 기존 통합 밸런스 엑셀.</summary>
    public const string DefaultWorkbook = "outputs/balance-so-20260926/GameBalance.xlsx";

    /// <summary>단일 SO에 적용할 값과 검증 시점의 원본.</summary>
    public sealed class Change
    {
        public BossBalanceWorkbook.Row Row;
        public BossData Target;
        public GameObject Prefab;
        public string BeforeJson;
        public string Description;
    }

    /// <summary>미리보기와 적용 사이의 파일/에셋 변경을 감지하는 계획.</summary>
    public sealed class Plan
    {
        public string Path;
        public string Fingerprint;
        public string Summary;
        public int RowCount;
        public readonly List<Change> Changes = new();
    }

    /// <summary>파일과 등록표, 대상 SO를 검사한다. 에셋을 변경하지 않는다.</summary>
    public static Plan Prepare(string path, BossBalanceRegistry registry)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 후 Import하세요.");
        if (registry == null) throw new InvalidOperationException("먼저 기존 에셋을 등록하세요.");
        if (!string.Equals(System.IO.Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new FormatException(".xlsx 파일을 선택하세요.");
        var bosses = new Dictionary<string, BossData>(StringComparer.Ordinal);
        var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        var usedTargets = new HashSet<BossData>();
        foreach (var entry in registry.Bosses)
        {
            if (entry == null || !BossBalanceRegistry.IsValidKey(entry.Key) || entry.Data == null || !AssetDatabase.Contains(entry.Data) || bosses.ContainsKey(entry.Key) || !usedTargets.Add(entry.Data))
                throw new FormatException("등록표의 보스 키/에셋에 누락 또는 중복이 있습니다.");
            bosses.Add(entry.Key, entry.Data);
        }
        foreach (var entry in registry.Prefabs)
        {
            if (entry == null || !BossBalanceRegistry.IsValidKey(entry.Key) || entry.Prefab == null || !PrefabUtility.IsPartOfPrefabAsset(entry.Prefab) || entry.Prefab.GetComponent<BossBase>() == null || prefabs.ContainsKey(entry.Key))
                throw new FormatException("등록표의 프리팹 키가 중복되었거나 BossBase 프리팹이 아닙니다.");
            prefabs.Add(entry.Key, entry.Prefab);
        }

        byte[] bytes = File.ReadAllBytes(path);
        var rows = BossBalanceWorkbook.Read(bytes);
        var plan = new Plan { Path = System.IO.Path.GetFullPath(path), RowCount = rows.Count };
        var fingerprint = new StringBuilder(Hash(bytes)).Append(EditorJsonUtility.ToJson(registry));
        foreach (var row in rows)
        {
            if (!bosses.TryGetValue(row.Key, out var target)) throw new FormatException($"{row.Number}행: 미등록 보스 키 '{row.Key}'");
            if (!prefabs.TryGetValue(row.PrefabKey, out var prefab)) throw new FormatException($"{row.Number}행: 미등록 프리팹 키 '{row.PrefabKey}'");
            if (EditorUtility.IsDirty(target)) throw new InvalidOperationException($"{row.Key}: Inspector의 미저장 변경을 저장/취소한 후 다시 검증하세요.");
            string before = EditorJsonUtility.ToJson(target);
            fingerprint.Append(AssetDatabase.GetAssetPath(target)).Append(before).Append(AssetDatabase.GetAssetPath(prefab));
            var serialized = new SerializedObject(target);
            var differences = new List<string>();
            AddDifference(differences, "이름", serialized.FindProperty("_displayName").stringValue, row.Name);
            AddDifference(differences, "HP", serialized.FindProperty("_maxHp").longValue, row.Hp);
            AddDifference(differences, "방어", serialized.FindProperty("_defense").doubleValue, row.Defense);
            AddDifference(differences, "체력줄", serialized.FindProperty("_hpLineCount").intValue, row.Lines);
            AddDifference(differences, "EXP", serialized.FindProperty("_expReward").floatValue, row.Exp);
            if (target.Prefab != prefab) differences.Add($"프리팹: {target.Prefab?.name ?? "없음"} → {prefab.name} ({row.PrefabKey})");
            if (differences.Count > 0)
                plan.Changes.Add(new Change { Row = row, Target = target, Prefab = prefab, BeforeJson = before, Description = row.Key + "\n  " + string.Join("\n  ", differences) });
        }
        plan.Fingerprint = Hash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
        plan.Summary = $"보스 {plan.RowCount}행 검증 완료 / 변경 {plan.Changes.Count}개 SO\n" + string.Join("\n\n", plan.Changes.Select(x => x.Description));
        return plan;
    }

    /// <summary>미리보기와 동일한 상태에만 적용한다. 백업 후 저장하며 실패하면 원본을 복원한다.</summary>
    public static string Apply(Plan preview, BossBalanceRegistry registry)
    {
        if (preview == null) throw new ArgumentNullException(nameof(preview));
        var current = Prepare(preview.Path, registry);
        if (current.Fingerprint != preview.Fingerprint) throw new InvalidOperationException("미리보기 이후 엑셀/등록표/SO가 변경되었습니다. 다시 검증하세요.");
        if (current.Changes.Count == 0) return "변경 없음";
        string backup = "outputs/boss-balance-import/backups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(backup);
        foreach (var change in current.Changes)
            File.Copy(AssetDatabase.GetAssetPath(change.Target), backup + "/" + change.Row.Key + ".asset");
        File.WriteAllText(backup + "/changes.txt", current.Summary, Encoding.UTF8);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Import boss balance workbook");
        Undo.RegisterCompleteObjectUndo(current.Changes.Select(x => (UnityEngine.Object)x.Target).ToArray(), "Import boss balance workbook");
        try
        {
            foreach (var change in current.Changes)
            {
                var data = new SerializedObject(change.Target);
                data.FindProperty("_displayName").stringValue = change.Row.Name;
                data.FindProperty("_maxHp").longValue = change.Row.Hp;
                data.FindProperty("_defense").doubleValue = change.Row.Defense;
                data.FindProperty("_hpLineCount").intValue = change.Row.Lines;
                data.FindProperty("_expReward").floatValue = change.Row.Exp;
                data.FindProperty("_prefab").objectReferenceValue = change.Prefab;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(change.Target);
            }
            foreach (var change in current.Changes) AssetDatabase.SaveAssetIfDirty(change.Target);
            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            foreach (var change in current.Changes)
            {
                EditorJsonUtility.FromJsonOverwrite(change.BeforeJson, change.Target);
                File.Copy(backup + "/" + change.Row.Key + ".asset", AssetDatabase.GetAssetPath(change.Target), true);
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(change.Target));
            }
            throw;
        }
        Debug.Log($"[Boss Balance] {current.Changes.Count}개 SO 적용. 백업: {backup}");
        return backup;
    }

    private static void AddDifference<T>(List<string> lines, string label, T before, T after)
    {
        if (!EqualityComparer<T>.Default.Equals(before, after)) lines.Add($"{label}: {before} → {after}");
    }

    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes));
    }
}
