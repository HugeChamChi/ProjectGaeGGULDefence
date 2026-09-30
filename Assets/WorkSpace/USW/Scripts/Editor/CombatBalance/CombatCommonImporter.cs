using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 엑셀 CombatCommon 탭의 전투 공통 값을 검증·미리보기 후 GameConfig SO에 적용한다.
/// 연결되지 않은 키(예: defenseScale)는 미리보기에 표시만 하고 적용하지 않는다.
/// </summary>
public static class CombatCommonImporter
{
    /// <summary>프로젝트의 통합 밸런스 엑셀.</summary>
    public const string DefaultWorkbook = BossBalanceImporter.DefaultWorkbook;
    /// <summary>인게임 씬이 사용하는 전역 설정 SO.</summary>
    public const string GameConfigPath = "Assets/WorkSpace/HSD/Resources/Data/GameConfig.asset";
    private const string Sheet = "CombatCommon";
    private const string KeyHeader = "키";
    private const string ValueHeader = "값";

    /// <summary>엑셀 키 하나를 SO 필드 하나에 대응시킨다. 엑셀은 사람이 읽는 단위, SO는 코드 단위.</summary>
    private sealed class Binding
    {
        public string Key;
        public string Label;
        public string Property;
        public float ExcelToSo;
        public float Min;
        public float Max;
    }

    private static readonly Binding[] Bindings =
    {
        new() { Key = "baseCritChancePct", Label = "기본 치명타 확률", Property = nameof(GameConfig.baseCritChance), ExcelToSo = 0.01f, Min = 0f, Max = 100f },
        new() { Key = "damageVariancePct", Label = "피해 편차 (±%)", Property = nameof(GameConfig.damageVariance), ExcelToSo = 0.01f, Min = 0f, Max = 50f },
        new() { Key = "critDamageVariancePct", Label = "치명 배율 편차 (±%)", Property = nameof(GameConfig.critDamageVariance), ExcelToSo = 0.01f, Min = 0f, Max = 50f },
    };

    /// <summary>검증 결과와 적용할 값.</summary>
    public sealed class Plan
    {
        public string Path;
        public string Fingerprint;
        public string Summary;
        public string BeforeJson;
        public readonly List<(string property, float value)> Changes = new();
    }

    /// <summary>파일과 대상 SO를 검사한다. 에셋을 변경하지 않는다.</summary>
    public static Plan Prepare(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 후 Import하세요.");
        if (!string.Equals(System.IO.Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new FormatException(".xlsx 파일을 선택하세요.");
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath) ?? throw new InvalidOperationException($"GameConfig 없음: {GameConfigPath}");
        if (EditorUtility.IsDirty(config)) throw new InvalidOperationException("GameConfig의 Inspector 미저장 변경을 저장/취소한 후 다시 검증하세요.");

        byte[] bytes = File.ReadAllBytes(path);
        var table = BalanceXlsxTable.Read(bytes, Sheet, KeyHeader, ValueHeader);
        var serialized = new SerializedObject(config);
        var plan = new Plan { Path = System.IO.Path.GetFullPath(path), BeforeJson = EditorJsonUtility.ToJson(config) };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<string>();
        var ignored = new List<string>();

        foreach (var row in table.Rows)
        {
            string key = row.Get(KeyHeader).Trim();
            if (string.IsNullOrEmpty(key)) continue;
            if (!seen.Add(key)) throw row.Error(KeyHeader, $"중복 키 '{key}'");
            var binding = Bindings.FirstOrDefault(x => x.Key == key);
            if (binding == null) { ignored.Add(key); continue; }

            string text = row.Get(ValueHeader);
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float excel) || float.IsNaN(excel) || float.IsInfinity(excel))
                throw row.Error(ValueHeader, $"숫자가 아닙니다: '{text}'");
            if (excel < binding.Min || excel > binding.Max)
                throw row.Error(ValueHeader, $"{binding.Min}~{binding.Max} 범위를 벗어났습니다: {excel}");

            float value = excel * binding.ExcelToSo;
            var property = serialized.FindProperty(binding.Property) ?? throw new InvalidOperationException($"GameConfig에 필드 없음: {binding.Property}");
            if (!Mathf.Approximately(property.floatValue, value))
            {
                plan.Changes.Add((binding.Property, value));
                lines.Add($"{binding.Label} ({key}): {property.floatValue / binding.ExcelToSo:0.##} → {excel:0.##}");
            }
        }

        foreach (var binding in Bindings)
            if (!seen.Contains(binding.Key)) lines.Add($"엑셀에 '{binding.Key}' 행 없음 — {binding.Label}은 SO 값 유지");

        plan.Fingerprint = Hash(Encoding.UTF8.GetBytes(Hash(bytes) + plan.BeforeJson));
        plan.Summary = $"CombatCommon 검증 완료 / 변경 {plan.Changes.Count}개\n" +
                       (lines.Count > 0 ? string.Join("\n", lines) : "변경 없음") +
                       (ignored.Count > 0 ? "\n\n적용하지 않는 키 (연결 없음): " + string.Join(", ", ignored) : "");
        return plan;
    }

    /// <summary>미리보기와 동일한 상태에만 적용한다. 백업 후 저장하며 실패하면 원본을 복원한다.</summary>
    public static string Apply(Plan preview)
    {
        if (preview == null) throw new ArgumentNullException(nameof(preview));
        var current = Prepare(preview.Path);
        if (current.Fingerprint != preview.Fingerprint) throw new InvalidOperationException("미리보기 이후 엑셀/SO가 변경되었습니다. 다시 검증하세요.");
        if (current.Changes.Count == 0) return "변경 없음";

        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
        string backup = "outputs/combat-common-import/backups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(backup);
        File.Copy(GameConfigPath, backup + "/GameConfig.asset");
        File.WriteAllText(backup + "/changes.txt", current.Summary, Encoding.UTF8);

        Undo.RegisterCompleteObjectUndo(config, "Import combat common workbook");
        try
        {
            var data = new SerializedObject(config);
            foreach (var (property, value) in current.Changes) data.FindProperty(property).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }
        catch
        {
            EditorJsonUtility.FromJsonOverwrite(current.BeforeJson, config);
            File.Copy(backup + "/GameConfig.asset", GameConfigPath, true);
            AssetDatabase.ImportAsset(GameConfigPath);
            throw;
        }
        Debug.Log($"[Combat Common] GameConfig {current.Changes.Count}개 값 적용. 백업: {backup}");
        return backup;
    }

    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes));
    }
}
