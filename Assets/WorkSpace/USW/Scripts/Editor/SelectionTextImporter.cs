using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택지·용어 문구 Excel을 읽어 전체 검증 후 문구 SO 세 종류(드론 선택지 카드, TermGlossary, DescriptionTermCatalog)의
/// 글자 필드만 갱신한다. 효과·수치·영상·수치 출처·색은 수정하지 않는다.
/// </summary>
public static class SelectionTextImporter
{
    /// <summary>문구의 유일한 저작 원본.</summary>
    public const string SourcePath = "Data/Text/SelectionDescriptions.xlsx";
    /// <summary>카드 문구 대상 드론 풀.</summary>
    public const string PoolPath = DroneChoiceConceptImporter.PoolPath;
    /// <summary>선택지 카드 용어 사전.</summary>
    public const string GlossaryPath = "Assets/WorkSpace/USW/Data/UI/TermGlossary.asset";
    /// <summary>설명 모드 용어 카탈로그.</summary>
    public const string CatalogPath = "Assets/WorkSpace/USW/Resources/DescriptionTermCatalog.asset";
    /// <summary>지원하는 Excel 형식.</summary>
    public const int SupportedSchemaVersion = 1;

    private static readonly Regex Token = new Regex(@"\{[^{}]+\}");

    /// <summary>검증을 통과한 변경 한 건.</summary>
    public sealed class Change
    {
        /// <summary>대상 SO.</summary>
        public UnityEngine.Object Target;
        /// <summary>보고용 이름 (예: 9105.description).</summary>
        public string Label;
        /// <summary>적용 동작.</summary>
        public Action Apply;
    }

    /// <summary>명시적 Import 메뉴.</summary>
    [MenuItem("Tools/Selection/Import SelectionDescriptions.xlsx")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath));

    /// <summary>현재 SO가 Excel과 같은지 확인한다. 자산을 갱신하지 않는다.</summary>
    [MenuItem("Tools/Selection/Validate SelectionDescriptions.xlsx")]
    public static void ValidateMenu()
    {
        var changes = Plan(File.ReadAllBytes(SourcePath), LoadPool(), LoadGlossary(), LoadCatalog());
        if (changes.Count > 0)
            throw new InvalidOperationException("Excel과 다른 문구 " + changes.Count + "건: " + string.Join(", ", changes.Select(c => c.Label)));
        Debug.Log("SelectionDescriptions.xlsx와 문구 SO가 일치합니다.");
    }

    /// <summary>모든 검증 후에만 대상 자산을 갱신한다. 실패 시 모든 대상의 파일·메모리 내용을 복원한다.</summary>
    public static string Import(string workbookPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("문구 Import 전에 Play를 종료하세요.");
        var changes = Plan(File.ReadAllBytes(workbookPath), LoadPool(), LoadGlossary(), LoadCatalog());
        if (changes.Count == 0) return "SelectionDescriptions: 변경 없음";
        var targets = changes.Select(c => c.Target).Distinct().ToArray();
        foreach (var target in targets)
            if (EditorUtility.IsDirty(target)) throw new InvalidOperationException("저장되지 않은 변경이 있는 자산: " + AssetDatabase.GetAssetPath(target));
        var backups = targets.Select(t => (Target: t, Path: AssetDatabase.GetAssetPath(t), Json: EditorJsonUtility.ToJson(t),
            Bytes: File.ReadAllBytes(AssetDatabase.GetAssetPath(t)))).ToArray();
        try
        {
            foreach (var change in changes) change.Apply();
            foreach (var target in targets) EditorUtility.SetDirty(target);
            foreach (var target in targets) AssetDatabase.SaveAssetIfDirty(target);
        }
        catch
        {
            foreach (var backup in backups)
            {
                EditorJsonUtility.FromJsonOverwrite(backup.Json, backup.Target);
                File.WriteAllBytes(backup.Path, backup.Bytes);
                EditorUtility.ClearDirty(backup.Target);
            }
            throw;
        }
        return "SelectionDescriptions imported (" + changes.Count + "건): " + string.Join(", ", changes.Select(c => c.Label));
    }

    /// <summary>원본 전체를 검증하고 실제로 달라지는 필드만 변경 목록으로 만든다. 자산을 수정하지 않는다.</summary>
    public static List<Change> Plan(byte[] source, LevelUpPoolData pool, TermGlossary glossary, DescriptionTermCatalog catalog)
    {
        if (pool == null || pool.Cards == null || pool.Cards.Length == 0) throw new FormatException("대상 드론 풀이 없습니다.");
        if (glossary == null || catalog == null) throw new FormatException("TermGlossary 또는 DescriptionTermCatalog가 없습니다.");
        ReadMeta(source);
        var changes = new List<Change>();
        var termIds = PlanGlossary(source, glossary, changes);
        PlanChoices(source, pool, termIds, changes);
        PlanCatalog(source, catalog, changes);
        return changes;
    }

    private static void ReadMeta(byte[] source)
    {
        var meta = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in BalanceXlsxTable.Read(source, "Meta", "Key", "Value").Rows)
            if (!meta.TryAdd(row.Get("Key").Trim(), row.Get("Value").Trim())) throw row.Error("Key", "중복 메타 키");
        if (!meta.TryGetValue("SchemaVersion", out var schemaText)
            || !int.TryParse(schemaText, NumberStyles.None, CultureInfo.InvariantCulture, out int schema) || schema != SupportedSchemaVersion
            || !meta.TryGetValue("DataVersion", out var version) || string.IsNullOrWhiteSpace(version))
            throw new FormatException("Meta: SchemaVersion=1과 비어 있지 않은 DataVersion이 필요합니다.");
    }

    private static HashSet<string> PlanGlossary(byte[] source, TermGlossary glossary, List<Change> changes)
    {
        var entries = glossary.Entries.Where(e => e != null).ToDictionary(e => e.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var aliasOwner = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in BalanceXlsxTable.Read(source, "용어", "Id", "DisplayName", "Aliases", "Kind", "BriefBody", "DetailedBody", "Body", "ClipCaption").Rows)
        {
            string id = row.Get("Id").Trim();
            if (!entries.TryGetValue(id, out var entry)) throw row.Error("Id", "TermGlossary에 없는 Id (Id 추가·변경은 SO에서 먼저)");
            if (!seen.Add(id)) throw row.Error("Id", "중복 Id");
            string[] aliases = SplitList(row.Get("Aliases"));
            if (aliases.Length == 0) throw row.Error("Aliases", "표기가 최소 하나 필요합니다.");
            foreach (string alias in aliases)
                if (!aliasOwner.TryAdd(alias, id)) throw row.Error("Aliases", $"'{alias}'가 {aliasOwner[alias]}와 겹칩니다.");
            string name = Required(row, "DisplayName"), kind = Required(row, "Kind"), body = Required(row, "Body");
            string brief = Text(row, "BriefBody"), detailed = Text(row, "DetailedBody"), caption = Text(row, "ClipCaption");
            string known = entry.Body + entry.BriefBody + entry.DetailedBody;
            CheckTokens(row, "BriefBody", brief, known);
            CheckTokens(row, "DetailedBody", detailed, known);
            CheckTokens(row, "Body", body, known);
            Field(changes, glossary, id + ".DisplayName", entry.DisplayName, name, () => entry.DisplayName = name);
            if (!(entry.Aliases ?? Array.Empty<string>()).SequenceEqual(aliases)) changes.Add(new Change { Target = glossary, Label = id + ".Aliases", Apply = () => entry.Aliases = aliases });
            Field(changes, glossary, id + ".Kind", entry.Kind, kind, () => entry.Kind = kind);
            Field(changes, glossary, id + ".BriefBody", entry.BriefBody, brief, () => entry.BriefBody = brief);
            Field(changes, glossary, id + ".DetailedBody", entry.DetailedBody, detailed, () => entry.DetailedBody = detailed);
            Field(changes, glossary, id + ".Body", entry.Body, body, () => entry.Body = body);
            Field(changes, glossary, id + ".ClipCaption", entry.ClipCaption, caption, () => entry.ClipCaption = caption);
        }
        if (!seen.SetEquals(entries.Keys)) throw new FormatException("용어: TermGlossary Id와 완전히 일치해야 합니다. 누락: " + string.Join(",", entries.Keys.Except(seen)));
        return seen;
    }

    private static void PlanChoices(byte[] source, LevelUpPoolData pool, HashSet<string> termIds, List<Change> changes)
    {
        var cards = new Dictionary<int, LevelUpData>();
        foreach (var card in pool.Cards)
            if (card == null || !cards.TryAdd(card.chooseId, card)) throw new FormatException("드론 풀에 빈 카드나 중복 ID가 있습니다.");
        var seen = new HashSet<int>();
        foreach (var row in BalanceXlsxTable.Read(source, "선택지", "ChooseId", "Name", "SimpleDescription", "Description", "Terms").Rows)
        {
            if (!int.TryParse(row.Get("ChooseId").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || !cards.TryGetValue(id, out var card))
                throw row.Error("ChooseId", "드론 풀에 없는 ID");
            if (!seen.Add(id)) throw row.Error("ChooseId", "중복 ID");
            string name = Required(row, "Name"), simple = Required(row, "SimpleDescription"), detail = Required(row, "Description");
            string known = card.description + card.simpleDescription;
            CheckTokens(row, "SimpleDescription", simple, known);
            CheckTokens(row, "Description", detail, known);
            string[] terms = SplitList(row.Get("Terms"));
            foreach (string term in terms) if (!termIds.Contains(term)) throw row.Error("Terms", $"'용어' 탭에 없는 Id: {term}");
            string label = id.ToString(CultureInfo.InvariantCulture);
            Field(changes, card, label + ".chooseName", card.chooseName, name, () => card.chooseName = name);
            Field(changes, card, label + ".simpleDescription", card.simpleDescription, simple, () => card.simpleDescription = simple);
            Field(changes, card, label + ".description", card.description, detail, () => card.description = detail);
            if (!(card.DescriptionTerms ?? Array.Empty<string>()).SequenceEqual(terms))
                changes.Add(new Change { Target = card, Label = label + ".DescriptionTerms", Apply = () => card.DescriptionTerms = terms });
        }
        if (!seen.SetEquals(cards.Keys)) throw new FormatException("선택지: 드론 풀 ID와 완전히 일치해야 합니다. 누락: " + string.Join(",", cards.Keys.Except(seen).OrderBy(x => x)));
    }

    private static void PlanCatalog(byte[] source, DescriptionTermCatalog catalog, List<Change> changes)
    {
        var entries = catalog.Entries.Where(e => e != null).ToDictionary(e => e.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in BalanceXlsxTable.Read(source, "설명용어", "Id", "DisplayName", "Body").Rows)
        {
            string id = row.Get("Id").Trim();
            if (!entries.TryGetValue(id, out var entry)) throw row.Error("Id", "DescriptionTermCatalog에 없는 Id");
            if (!seen.Add(id)) throw row.Error("Id", "중복 Id");
            string name = Required(row, "DisplayName"), body = Required(row, "Body");
            Field(changes, catalog, "설명용어." + id + ".DisplayName", entry.DisplayName, name, () => entry.DisplayName = name);
            Field(changes, catalog, "설명용어." + id + ".Body", entry.Body, body, () => entry.Body = body);
        }
        if (!seen.SetEquals(entries.Keys)) throw new FormatException("설명용어: DescriptionTermCatalog Id와 완전히 일치해야 합니다. 누락: " + string.Join(",", entries.Keys.Except(seen)));
    }

    private static void Field(List<Change> changes, UnityEngine.Object target, string label, string current, string next, Action apply)
    {
        if (!string.Equals(current ?? string.Empty, next, StringComparison.Ordinal)) changes.Add(new Change { Target = target, Label = label, Apply = apply });
    }

    /// <summary>수치가 들어가는 {자리}는 해당 항목이 이미 쓰던 것만 허용한다. 효과 정의가 모르는 자리를 막는다.</summary>
    private static void CheckTokens(BalanceXlsxTable.Row row, string header, string text, string known)
    {
        foreach (Match match in Token.Matches(text))
            if (known == null || !known.Contains(match.Value)) throw row.Error(header, $"이 항목에서 쓰지 않던 수치 자리 {match.Value}");
    }

    private static string Text(BalanceXlsxTable.Row row, string header) => row.Get(header).Replace("\r\n", "\n").Replace('\r', '\n');

    private static string Required(BalanceXlsxTable.Row row, string header)
    {
        string value = Text(row, header);
        if (string.IsNullOrWhiteSpace(value)) throw row.Error(header, "비어 있으면 안 됩니다.");
        return value;
    }

    private static string[] SplitList(string value) =>
        value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

    private static LevelUpPoolData LoadPool() => AssetDatabase.LoadAssetAtPath<LevelUpPoolData>(PoolPath);
    private static TermGlossary LoadGlossary() => AssetDatabase.LoadAssetAtPath<TermGlossary>(GlossaryPath);
    private static DescriptionTermCatalog LoadCatalog() => AssetDatabase.LoadAssetAtPath<DescriptionTermCatalog>(CatalogPath);
}
