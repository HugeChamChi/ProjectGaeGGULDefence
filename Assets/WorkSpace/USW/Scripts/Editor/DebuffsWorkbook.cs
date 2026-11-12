using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GGD.BackendCharts;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only bridge between the authoring workbook <c>Data/Balance/Debuffs.xlsx</c> and the bundled DebuffData assets
/// (user decision 2026-11-11: Excel is the source; bundled values and the BACKND chart are both made from it).
/// The same workbook is uploaded to DEV_Debuffs, then LIVE_Debuffs. Never opened by a player build.
/// </summary>
public static class DebuffsWorkbook
{
    public const string SourcePath = "Data/Balance/Debuffs.xlsx";
    public const string SheetName = "Debuffs";
    private const string SettingsPath = "Assets/WorkSpace/USW/Resources/DebuffSettings.asset";
    // Chart column -> DebuffData serialized field. Kind is written as the enum name in the chart.
    private static readonly (string Column, string Field)[] Fields =
    {
        ("DebuffId", "_id"), ("Key", "_key"), ("Kind", "_kind"), ("StackGroup", "_stackGroup"), ("DurationSeconds", "_duration"),
        ("MaxStacks", "_maxStacks"), ("TickIntervalSeconds", "_tickInterval"), ("SnapshotHpPercent", "_snapshotPercent"),
        ("MinTickDamage", "_minTickDamage"), ("ArmorStrength", "_armorStrength"), ("DamageTakenMultiplier", "_damageMultiplier")
    };

    [MenuItem("Tools/Balance/Debuffs.xlsx 검사 (업로드 전)")]
    public static void CheckMenu() => Debug.Log(Check(SourcePath));

    [MenuItem("Tools/Balance/Debuffs.xlsx 값을 앱 내장 값에 반영")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath));

    /// <summary>Workbook rows as chart rows (text cells), not yet validated.</summary>
    public static JsonData ReadRows(string path)
    {
        var table = BalanceXlsxTable.Read(File.ReadAllBytes(path), SheetName, 1, DebuffChart.Columns);
        var rows = new JsonData(); rows.SetJsonType(JsonType.Array);
        foreach (var row in table.Rows)
        {
            var item = new JsonData();
            foreach (var column in DebuffChart.Columns) item[column] = row.Get(column) ?? "";
            rows.Add(item);
        }
        return rows;
    }

    /// <summary>Bundled DebuffData assets as chart rows, in DebuffSettings order.</summary>
    public static JsonData BundledRows()
    {
        var rows = new JsonData(); rows.SetJsonType(JsonType.Array);
        foreach (var data in Assets())
        {
            var serialized = new SerializedObject(data);
            var item = new JsonData();
            foreach (var (column, field) in Fields) item[column] = Cell(serialized.FindProperty(field));
            rows.Add(item);
        }
        return rows;
    }

    public static IEnumerable<int> BundledIds() => Assets().Select(data => data.Id);

    public static string Check(string path)
    {
        var rows = ReadRows(path);
        var workbook = DebuffChart.Parse(rows, BundledIds());
        var lines = new List<string> { "Debuffs.xlsx 형식 통과: 디버프 " + workbook.Count + "개" };
        var bundled = BundledRows();
        for (int i = 0; i < rows.Count; i++)
        {
            string id = rows[i]["DebuffId"].ToString();
            JsonData match = null;
            for (int b = 0; b < bundled.Count; b++) if (bundled[b]["DebuffId"].ToString() == id) match = bundled[b];
            if (match == null) { lines.Add("앱 내장 값 없음: 디버프 " + id); continue; }
            foreach (var column in DebuffChart.Columns)
                if (Normal(rows[i][column].ToString(), column) != Normal(match[column].ToString(), column))
                    lines.Add("다름 " + id + " " + column + ": 엑셀 " + rows[i][column] + " / 앱 내장 " + match[column]);
        }
        if (lines.Count == 1) lines.Add("앱 내장 값과 전부 같음");
        return string.Join("\n", lines);
    }

    /// <summary>Writes workbook numbers into the bundled DebuffData assets after the whole workbook passed validation. IDs must already exist.</summary>
    public static string Import(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before importing debuffs.");
        var rows = ReadRows(path);
        DebuffChart.Parse(rows, BundledIds());
        var assets = Assets().ToDictionary(data => data.Id.ToString());
        for (int i = 0; i < rows.Count; i++)
        {
            if (!assets.TryGetValue(rows[i]["DebuffId"].ToString(), out var data))
                throw new InvalidOperationException("디버프 " + rows[i]["DebuffId"] + ": 앱에 DebuffData 에셋이 없다. 에셋(아이콘, 문구)을 먼저 만들고 DebuffSettings에 등록한다.");
            var serialized = new SerializedObject(data);
            foreach (var (column, field) in Fields)
            {
                var property = serialized.FindProperty(field);
                string text = rows[i][column].ToString();
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer: property.intValue = int.Parse(text, CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Enum: property.intValue = (int)Enum.Parse(typeof(DebuffKind), text); break;
                    case SerializedPropertyType.String: property.stringValue = text; break;
                    default: property.doubleValue = text.Length == 0 ? (column == "DamageTakenMultiplier" ? 1 : 0) : double.Parse(text, CultureInfo.InvariantCulture); break;
                }
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        }
        return "Debuffs.xlsx 값을 앱 내장 값에 반영함: 디버프 " + rows.Count + "개";
    }

    private static List<DebuffData> Assets()
    {
        var settings = AssetDatabase.LoadAssetAtPath<DebuffSettings>(SettingsPath) ?? throw new InvalidOperationException("Missing " + SettingsPath);
        var list = new SerializedObject(settings).FindProperty("_definitions");
        var result = new List<DebuffData>();
        for (int i = 0; i < list.arraySize; i++) result.Add((DebuffData)list.GetArrayElementAtIndex(i).objectReferenceValue);
        return result;
    }

    private static string Cell(SerializedProperty property) => property.propertyType switch
    {
        SerializedPropertyType.Integer => property.intValue.ToString(CultureInfo.InvariantCulture),
        SerializedPropertyType.Enum => ((DebuffKind)property.intValue).ToString(),
        SerializedPropertyType.String => property.stringValue,
        _ => property.doubleValue.ToString("0.######", CultureInfo.InvariantCulture)
    };

    // Compares numbers by value so that "" and "0" (or "1" for the multiplier) and "5" and "5.0" count as equal.
    private static string Normal(string text, string column)
    {
        if (column == "Key" || column == "Kind" || column == "StackGroup") return text;
        if (text.Length == 0) return column == "DamageTakenMultiplier" ? "1" : "0";
        return double.Parse(text, CultureInfo.InvariantCulture).ToString("0.######", CultureInfo.InvariantCulture);
    }
}
