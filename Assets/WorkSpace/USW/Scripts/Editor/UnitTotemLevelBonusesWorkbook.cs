using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GGD.BackendCharts;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only bridge between <c>Data/Balance/UnitTotemLevelBonuses.xlsx</c> and the bundled
/// <c>Resources/UnitTotemLevelBonuses.json</c> (user decision 2026-11-11: Excel is the source; bundled values and the
/// BACKND chart are both made from it). The same workbook is uploaded to DEV_UnitTotemLevelBonuses, then LIVE_.
/// </summary>
public static class UnitTotemLevelBonusesWorkbook
{
    public const string SourcePath = "Data/Balance/UnitTotemLevelBonuses.xlsx";
    public const string SheetName = "UnitTotemLevelBonuses";
    public const string BundledPath = "Assets/WorkSpace/USW/Resources/UnitTotemLevelBonuses.json";

    [MenuItem("Tools/Balance/UnitTotemLevelBonuses.xlsx 검사 (업로드 전)")]
    public static void CheckMenu() => Debug.Log(Check(SourcePath));

    [MenuItem("Tools/Balance/UnitTotemLevelBonuses.xlsx 값을 앱 내장 값에 반영")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath));

    /// <summary>Workbook rows as chart rows (text cells), not yet validated. Blank rows are skipped.</summary>
    public static JsonData ReadRows(string path)
    {
        var table = BalanceXlsxTable.Read(File.ReadAllBytes(path), SheetName, 1, UnitTotemLevelBonusChart.Columns);
        var rows = new JsonData(); rows.SetJsonType(JsonType.Array);
        foreach (var row in table.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Get("UnitKey"))) continue;
            var item = new JsonData();
            foreach (var column in UnitTotemLevelBonusChart.Columns) item[column] = row.Get(column) ?? "";
            rows.Add(item);
        }
        return rows;
    }

    /// <summary>Bundled JSON rows.</summary>
    public static JsonData BundledRows() => JsonMapper.ToObject(File.ReadAllText(BundledPath, Encoding.UTF8));

    public static string Check(string path)
    {
        var rows = ReadRows(path);
        var workbook = UnitTotemLevelBonusChart.Parse(rows);
        var lines = new List<string> { "UnitTotemLevelBonuses.xlsx 형식 통과: 유닛 " + workbook.Count + "종, " + rows.Count + "줄" };
        var bundled = BundledRows();
        var bundledByKey = new Dictionary<string, JsonData>();
        for (int i = 0; i < bundled.Count; i++) bundledByKey[RowKey(bundled[i])] = bundled[i];
        var seen = new HashSet<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            string key = RowKey(rows[i]);
            seen.Add(key);
            if (!bundledByKey.TryGetValue(key, out var match)) { lines.Add("앱 내장 값 없음: " + key); continue; }
            foreach (var column in new[] { "Stat", "Value", "Description" })
                if (Normal(rows[i][column].ToString(), column) != Normal(match[column].ToString(), column))
                    lines.Add("다름 " + key + " " + column + ": 엑셀 " + rows[i][column] + " / 앱 내장 " + match[column]);
        }
        foreach (var key in bundledByKey.Keys) if (!seen.Contains(key)) lines.Add("엑셀에 없음(앱 내장에만 있음): " + key);
        if (lines.Count == 1) lines.Add("앱 내장 값과 전부 같음");
        return string.Join("\n", lines);
    }

    /// <summary>Writes the whole workbook into the bundled JSON after it passed validation.</summary>
    public static string Import(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before importing level bonuses.");
        var rows = ReadRows(path);
        UnitTotemLevelBonusChart.Parse(rows);
        var builder = new StringBuilder();
        builder.Append("[\n");
        for (int i = 0; i < rows.Count; i++)
        {
            builder.Append(" {");
            for (int c = 0; c < UnitTotemLevelBonusChart.Columns.Length; c++)
            {
                string column = UnitTotemLevelBonusChart.Columns[c];
                builder.Append(c == 0 ? "" : ", ").Append(Quote(column)).Append(": ").Append(Quote(rows[i][column].ToString().Trim()));
            }
            builder.Append(i == rows.Count - 1 ? "}\n" : "},\n");
        }
        builder.Append("]\n");
        File.WriteAllText(BundledPath, builder.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(BundledPath);
        UnitTotemLevelBonusChart.Clear();
        return "UnitTotemLevelBonuses.xlsx 값을 앱 내장 값에 반영함: " + rows.Count + "줄";
    }

    private static string RowKey(JsonData row) => row["UnitKey"] + " Lv" + row["Level"] + " #" + row["PartIndex"];

    private static string Quote(string text) => JsonMapper.ToJson(text);

    // Numbers compare by value so "0.1" and "0.10" count as equal.
    private static string Normal(string text, string column)
    {
        text = text.Trim();
        if (column != "Value") return text;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value.ToString("0.######", CultureInfo.InvariantCulture) : text;
    }
}
