using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>분류 Excel만 읽어 전체 검증 후 카탈로그 하나를 갱신한다. 선택지 SO와 추첨 설정은 수정하지 않는다.</summary>
public static class DroneChoiceConceptImporter
{
    /// <summary>유일한 수동 분류 저작 원본.</summary>
    public const string SourcePath = "Data/Balance/DroneChoiceConcepts.xlsx";
    /// <summary>분류 대상 드론 풀.</summary>
    public const string PoolPath = "Assets/WorkSpace/USW/Data/SelectionData/DroneLevelUpPool.asset";
    /// <summary>생성 카탈로그의 고정 경로.</summary>
    public const string AssetPath = "Assets/WorkSpace/USW/Data/SelectionData/DroneChoiceConceptCatalog.asset";
    private static readonly string[] TagColumns = { "Common", "SelfDestruct", "Glitch", "Hacking" };
    private static readonly DroneChoiceConcept[] TagValues = { DroneChoiceConcept.Common, DroneChoiceConcept.SelfDestruct,
        DroneChoiceConcept.Glitch, DroneChoiceConcept.Hacking };

    /// <summary>명시적 Import 메뉴. 기존 GUID/풀 연결을 유지한다.</summary>
    [MenuItem("Tools/Selection/Import DroneChoiceConcepts.xlsx")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath, AssetDatabase.LoadAssetAtPath<LevelUpPoolData>(PoolPath), AssetPath));

    /// <summary>모든 검증 후에만 대상 자산을 갱신한다. 실패 시 기존 파일·메모리 내용을 복원한다.</summary>
    public static string Import(string workbookPath, LevelUpPoolData pool, string assetPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("분류 Import 전에 Play를 종료하세요.");
        if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) || !assetPath.EndsWith(".asset", StringComparison.Ordinal)
            || assetPath.Contains("..")) throw new ArgumentException("프로젝트 Assets 아래의 .asset 경로가 필요합니다.");
        var imported = Read(File.ReadAllBytes(workbookPath), pool);
        imported.name = Path.GetFileNameWithoutExtension(assetPath);
        try
        {
            var existing = AssetDatabase.LoadAssetAtPath<DroneChoiceConceptCatalog>(assetPath);
            if (existing == null && File.Exists(assetPath)) throw new InvalidOperationException("대상 경로에 다른 자산이 있습니다.");
            if (existing != null && EditorUtility.IsDirty(existing)) throw new InvalidOperationException("대상 카탈로그의 저장되지 않은 변경을 먼저 처리하세요.");
            if (!Directory.Exists(Path.GetDirectoryName(assetPath))) throw new DirectoryNotFoundException(assetPath);
            if (existing == null)
            {
                try
                {
                    AssetDatabase.CreateAsset(imported, assetPath);
                    AssetDatabase.SaveAssetIfDirty(imported);
                }
                catch
                {
                    if (AssetDatabase.GetAssetPath(imported) == assetPath) AssetDatabase.DeleteAsset(assetPath);
                    throw;
                }
                imported = null; // AssetDatabase가 수명을 소유한다.
            }
            else
            {
                string backupJson = EditorJsonUtility.ToJson(existing);
                byte[] backupBytes = File.ReadAllBytes(assetPath);
                try
                {
                    EditorUtility.CopySerialized(imported, existing);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }
                catch
                {
                    EditorJsonUtility.FromJsonOverwrite(backupJson, existing);
                    File.WriteAllBytes(assetPath, backupBytes);
                    EditorUtility.ClearDirty(existing);
                    throw;
                }
            }
            return "Drone choice concepts imported: " + assetPath;
        }
        finally { if (imported != null) UnityEngine.Object.DestroyImmediate(imported); }
    }

    /// <summary>원본 바이트에서 분리된 검증 산출물을 만든다. 호출자가 DestroyImmediate로 해제한다.</summary>
    public static DroneChoiceConceptCatalog Read(byte[] source, LevelUpPoolData pool)
    {
        if (pool == null || pool.PoolId <= 0 || pool.Cards == null || pool.Cards.Length == 0)
            throw new FormatException("유효한 대상 풀과 카드가 필요합니다.");
        var expected = new HashSet<int>();
        foreach (var card in pool.Cards)
            if (card == null || card.chooseId <= 0 || !expected.Add(card.chooseId))
                throw new FormatException("대상 풀에 빈 카드·잘못된 ID·중복 ID가 있습니다.");
        var meta = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in BalanceXlsxTable.Read(source, "Meta", "Key", "Value").Rows)
        {
            string key = row.Get("Key").Trim();
            if (!meta.TryAdd(key, row.Get("Value").Trim())) throw row.Error("Key", "중복 메타 키");
        }
        if (meta.Count != 3 || !meta.TryGetValue("SchemaVersion", out var schemaText)
            || !int.TryParse(schemaText, NumberStyles.None, CultureInfo.InvariantCulture, out int schema)
            || schema != DroneChoiceConceptCatalog.SupportedSchemaVersion
            || !meta.TryGetValue("PoolId", out var poolText)
            || !int.TryParse(poolText, NumberStyles.None, CultureInfo.InvariantCulture, out int poolId) || poolId != pool.PoolId
            || !meta.TryGetValue("DataVersion", out var version) || string.IsNullOrWhiteSpace(version))
            throw new FormatException("Meta: SchemaVersion=1, 대상 PoolId, 비어 있지 않은 DataVersion만 필요합니다.");
        var entries = new List<DroneChoiceConceptCatalog.Entry>();
        var seen = new HashSet<int>();
        foreach (var row in BalanceXlsxTable.Read(source, "Choices", "ChooseId", "Name", "Common", "SelfDestruct", "Glitch", "Hacking", "Note").Rows)
        {
            if (!int.TryParse(row.Get("ChooseId"), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || !expected.Contains(id))
                throw row.Error("ChooseId", "풀에 등록되지 않은 ID");
            if (!seen.Add(id)) throw row.Error("ChooseId", "중복 ID");
            var tags = DroneChoiceConcept.None;
            for (int i = 0; i < TagColumns.Length; i++)
            {
                string value = row.Get(TagColumns[i]);
                if (value != "0" && value != "1") throw row.Error(TagColumns[i], "0 또는 1만 허용합니다.");
                if (value == "1") tags |= TagValues[i];
            }
            if (tags == DroneChoiceConcept.None) throw row.Error("Common", "태그가 최소 하나 필요합니다.");
            if ((tags & DroneChoiceConcept.Common) != 0 && tags != DroneChoiceConcept.Common)
                throw row.Error("Common", "공용과 다른 태그를 함께 사용할 수 없습니다.");
            entries.Add(new DroneChoiceConceptCatalog.Entry { ChooseId = id, Tags = tags });
        }
        if (!seen.SetEquals(expected)) throw new FormatException("Choices: 풀 ID 집합과 완전히 일치해야 합니다. 누락: " + string.Join(",", expected.Except(seen).OrderBy(x => x)));
        var sorted = entries.OrderBy(x => x.ChooseId).ToArray();
        var result = ScriptableObject.CreateInstance<DroneChoiceConceptCatalog>();
        result.name = "DroneChoiceConceptCatalog";
        result.SetImportedData(pool.PoolId, schema, version, Sha256(source), ContentHash(pool.PoolId, sorted), sorted);
        return result;
    }

    private static string ContentHash(int poolId, IEnumerable<DroneChoiceConceptCatalog.Entry> entries)
    {
        var canonical = new StringBuilder().Append("PoolId=").Append(poolId.ToString(CultureInfo.InvariantCulture))
            .Append('\n').Append("ChooseId|Common|SelfDestruct|Glitch|Hacking\n");
        foreach (var entry in entries)
        {
            canonical.Append(entry.ChooseId.ToString(CultureInfo.InvariantCulture));
            foreach (var tag in TagValues) canonical.Append('|').Append((entry.Tags & tag) == 0 ? '0' : '1');
            canonical.Append('\n');
        }
        return Sha256(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>현재 Excel/풀/연결 SO의 메타·내용 일치를 확인한다. 자산을 갱신하지 않는다.</summary>
    [MenuItem("Tools/Selection/Validate Drone Choice Concepts")]
    public static void ValidateCurrent()
    {
        var pool = AssetDatabase.LoadAssetAtPath<LevelUpPoolData>(PoolPath);
        Validate(File.ReadAllBytes(SourcePath), pool, pool != null ? pool.ConceptCatalog : null);
    }

    /// <summary>빌드/후속 캡처에서 사용할 읽기 전용 출처·내용 검증. 런타임 효과에는 관여하지 않는다.</summary>
    public static void Validate(byte[] source, LevelUpPoolData pool, DroneChoiceConceptCatalog actual)
    {
        var expected = Read(source, pool);
        try
        {
            if (actual == null || actual.PoolId != expected.PoolId || actual.SchemaVersion != expected.SchemaVersion
                || actual.DataVersion != expected.DataVersion || actual.SourceWorkbookSha256 != expected.SourceWorkbookSha256
                || actual.ContentHash != expected.ContentHash || !actual.Entries.SequenceEqual(expected.Entries))
                throw new InvalidOperationException("분류 Excel·풀·카탈로그가 다릅니다. 명시적으로 다시 Import하세요.");
        }
        finally { UnityEngine.Object.DestroyImmediate(expected); }
    }
}
