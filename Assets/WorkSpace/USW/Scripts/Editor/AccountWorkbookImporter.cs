using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit Editor-only XLSX import. Supported formula templates are recalculated from current inputs, never trusted caches.</summary>
public static class AccountWorkbookImporter
{
    /// <summary>Maintained authoring source; never opened by a player build.</summary>
    public const string SourcePath = "Data/Balance/AccountProgression.xlsx";
    /// <summary>Stable imported build asset.</summary>
    public const string AssetPath = "Assets/WorkSpace/USW/Resources/AccountProgressionConfig.asset";
    private const string Settings = "안내_계정설정";
    private const string Levels = "계정레벨";
    private const string Rules = "전투경험치";
    private const string Examples = "획득량_예시";

    /// <summary>Imports only after the entire workbook has passed validation. Failure leaves the existing asset untouched.</summary>
    [MenuItem("Tools/Balance/Import AccountProgression.xlsx")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath));

    /// <summary>Validates and updates the stable SO/GUID; the workbook is never modified.</summary>
    public static string Import(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before importing account progression.");
        var imported = Read(path, out var warnings);
        try
        {
            var existing = AssetDatabase.LoadAssetAtPath<AccountProgressionConfig>(AssetPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<AccountProgressionConfig>();
                AssetDatabase.CreateAsset(existing, AssetPath);
            }
            EditorUtility.CopySerialized(imported, existing);
            existing.name = "AccountProgressionConfig";
            EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing);
            if (warnings.Count > 0) Debug.LogWarning("[Account import] " + string.Join("\n", warnings));
            return $"Account import OK: {existing.MaxLevel} levels. SHA256 {existing.SourceSha256}. "
                + (warnings.Count == 0 ? "Caches agree (or absent); calculated from inputs." : string.Join("\n", warnings));
        }
        finally { UnityEngine.Object.DestroyImmediate(imported); }
    }

    /// <summary>Reads a transient validated configuration. Caller owns/destroys it. Unknown formulas and bad inputs name their cell.</summary>
    public static AccountProgressionConfig Read(string path, out List<string> warnings)
    {
        byte[] source = File.ReadAllBytes(path);
        var book = new Workbook(source); warnings = book.Warnings;
        int max = book.Integer(Settings, "B5", 2, 10000);
        int initial = book.Integer(Settings, "B6", 1, max);
        int initialExp = book.Integer(Settings, "B7", 0);
        int first = book.Integer(Settings, "B8", 1);
        int step = book.Integer(Settings, "B13", 0);
        int bossExp = book.Integer(Settings, "B14", 0);
        book.RequireText(Settings, "B9", "이월");
        book.RequireText(Settings, "B10", "추가 경험치 미적립");

        var requirements = new int[max];
        decimal cumulative = 0;
        for (int level = 1; level <= max; level++)
        {
            int row = level + 4;
            if (book.Integer(Levels, "A" + row, 1) != level) throw book.Error(Levels, "A" + row, "levels must be contiguous from 1");
            string formula = $"IF(A{row}>='{Settings}'!$B$5,\"\",'{Settings}'!$B$8+(A{row}-1)*'{Settings}'!$B$13)";
            decimal? calculated = level == max ? null : first + (decimal)(level - 1) * step;
            var value = book.Number(Levels, "B" + row, formula, calculated, level == max);
            if (level == max)
            {
                if (value != null) throw book.Error(Levels, "B" + row, "max level must have a blank next requirement");
            }
            else requirements[level - 1] = book.AsInteger(Levels, "B" + row, value, 1);
            int prev = row - 1;
            string cumulativeFormula = level == 1 ? $"IF(A{row}=\"\",\"\",IF(A{row}=1,0,\"\"))"
                : $"IF(A{row}=\"\",\"\",IF(AND(ISNUMBER(A{prev}),A{row}=A{prev}+1,ISNUMBER(B{prev}),B{prev}>0,ISNUMBER(C{prev})),C{prev}+B{prev},\"\"))";
            if (book.Number(Levels, "C" + row, cumulativeFormula, cumulative) != cumulative)
                throw book.Error(Levels, "C" + row, "cumulative EXP disagrees with level requirements");
            cumulative += requirements[level - 1];
        }
        if (book.HasLevelAfter(max + 4)) throw book.Error(Levels, "A" + (max + 5), "extra level rows beyond configured maximum");

        bool win = false, lose = false;
        int winExp = 0, loseExp = 0;
        string[] ids = { "NORMAL_WIN", "NORMAL_LOSE", "NORMAL_QUIT", "TUTORIAL" };
        for (int i = 0; i < ids.Length; i++)
        {
            int row = 5 + i;
            book.RequireText(Rules, "A" + row, ids[i]);
            book.RequireText(Rules, "B" + row, i == 3 ? "TUTORIAL" : "NORMAL");
            book.RequireText(Rules, "C" + row, new[] { "승리", "패배", "중도 종료", "튜토리얼 종료" }[i]);
            book.RequireText(Rules, "E" + row, "보스별 최대 HP 감소 비율");
            string policy = book.Text(Rules, "D" + row);
            if (policy != "지급" && policy != "미지급") throw book.Error(Rules, "D" + row, "expected 지급 or 미지급");
            if (i >= 2 && policy != "미지급") throw book.Error(Rules, "D" + row, "quit/tutorial rewards are not supported by the current settlement contract");
            if (book.Integer(Rules, "F" + row, 0) != 0 || book.Number(Rules, "H" + row) != 1)
                throw book.Error(Rules, "F" + row, "HP-ratio policy requires base 0 and ratio cap 1");
            int amount = book.AsInteger(Rules, "G" + row, book.Number(Rules, "G" + row, $"'{Settings}'!$B$14", bossExp), 0);
            if (i == 0) { win = policy == "지급"; winExp = amount; }
            if (i == 1) { lose = policy == "지급"; loseExp = amount; }
        }
        if (book.HasRuleAfter(8)) throw book.Error(Rules, "A9", "additional reward rule rows are not supported; no rules were ignored");
        for (int row = 5; row <= 8; row++)
        {
            int kills = book.Integer(Examples, "B" + row, 0);
            decimal ratio = book.Number(Examples, "C" + row).Value;
            decimal expected = decimal.Floor((kills + Math.Min(1, Math.Max(0, ratio))) * bossExp);
            string formula = $"ROUNDDOWN((B{row}+MIN(1,MAX(0,C{row})))*'{Settings}'!$B$14,0)";
            if (book.Number(Examples, "D" + row, formula, expected) != expected)
                throw book.Error(Examples, "D" + row, "example disagrees with current inputs");
        }
        var config = ScriptableObject.CreateInstance<AccountProgressionConfig>();
        try
        {
            config.RequiredExperience = requirements; config.InitialLevel = initial; config.InitialExperience = initialExp;
            config.GrantOnWin = win; config.GrantOnLose = lose; config.WinBossExperience = winExp; config.LoseBossExperience = loseExp;
            using var hash = SHA256.Create();
            config.SourceSha256 = BitConverter.ToString(hash.ComputeHash(source)).Replace("-", "").ToLowerInvariant();
            config.Validate(); return config;
        }
        catch { UnityEngine.Object.DestroyImmediate(config); throw; }
    }

    private sealed class Workbook
    {
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private readonly Dictionary<string, Dictionary<string, XElement>> _sheets = new();
        private readonly string[] _strings;
        public readonly List<string> Warnings = new();

        public Workbook(byte[] source)
        {
            using var archive = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read);
            XDocument Xml(string name)
            {
                var entry = archive.GetEntry(name) ?? throw new InvalidDataException("Missing XLSX part: " + name);
                using var stream = entry.Open(); return XDocument.Load(stream);
            }
            var shared = archive.GetEntry("xl/sharedStrings.xml");
            if (shared != null)
            {
                using var stream = shared.Open();
                _strings = XDocument.Load(stream).Descendants(Ns + "si").Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value))).ToArray();
            }
            else _strings = Array.Empty<string>();
            var relationships = Xml("xl/_rels/workbook.xml.rels").Root.Elements()
                .ToDictionary(e => (string)e.Attribute("Id"), e => (string)e.Attribute("Target"));
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            foreach (var sheet in Xml("xl/workbook.xml").Descendants(Ns + "sheet"))
            {
                string target = relationships[(string)sheet.Attribute(rel + "id")];
                string entry = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
                _sheets.Add((string)sheet.Attribute("name"), Xml(entry).Descendants(Ns + "c")
                    .ToDictionary(c => (string)c.Attribute("r"), c => c));
            }
        }

        public Exception Error(string sheet, string address, string message) => new InvalidDataException($"{sheet}!{address}: {message}");
        private XElement Cell(string sheet, string address)
        {
            if (!_sheets.TryGetValue(sheet, out var cells)) throw Error(sheet, address, "missing sheet");
            cells.TryGetValue(address, out var cell);
            if ((string)cell?.Attribute("t") == "e") throw Error(sheet, address, "Excel error " + cell.Element(Ns + "v")?.Value);
            return cell;
        }
        private string Raw(XElement cell)
        {
            if ((string)cell?.Attribute("t") == "inlineStr") return string.Concat(cell.Descendants(Ns + "t").Select(t => t.Value));
            string value = cell?.Element(Ns + "v")?.Value ?? "";
            if ((string)cell?.Attribute("t") == "s") return _strings[int.Parse(value, CultureInfo.InvariantCulture)];
            return value;
        }
        public string Text(string sheet, string address)
        {
            var cell = Cell(sheet, address);
            if (cell?.Element(Ns + "f") != null) throw Error(sheet, address, "formula not supported in this input");
            return Raw(cell);
        }
        public void RequireText(string sheet, string address, string expected)
        { if (Text(sheet, address) != expected) throw Error(sheet, address, "expected " + expected); }
        private static string Normalize(string value) => Regex.Replace(value.TrimStart('=').Replace("$", ""), @"\s+", "");
        public decimal? Number(string sheet, string address, string supportedFormula = null, decimal? calculated = null, bool allowBlank = false)
        {
            var cell = Cell(sheet, address);
            string formula = cell?.Element(Ns + "f")?.Value;
            string raw = Raw(cell);
            if (formula != null)
            {
                if (supportedFormula == null || Normalize(formula) != Normalize(supportedFormula))
                    throw Error(sheet, address, "unsupported formula; use a numeric override or the documented template: " + formula);
                if (raw.Length > 0 && (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal cache) || calculated != cache))
                    Warnings.Add($"{sheet}!{address}: stale cache '{raw}', recalculated '{calculated}'.");
                return calculated;
            }
            if (raw.Length == 0 && allowBlank) return null;
            if ((string)cell?.Attribute("t") is "s" or "inlineStr" or "str" or "b"
                || !decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value))
                throw Error(sheet, address, "expected a numeric cell, found '" + raw + "'");
            return value;
        }
        public int Integer(string sheet, string address, int min, int max = int.MaxValue) => AsInteger(sheet, address, Number(sheet, address), min, max);
        public int AsInteger(string sheet, string address, decimal? value, int min, int max = int.MaxValue)
        {
            if (!value.HasValue || decimal.Truncate(value.Value) != value || value < min || value > max)
                throw Error(sheet, address, $"expected integer in [{min}, {max}]");
            return (int)value.Value;
        }
        public bool HasLevelAfter(int lastRow) => _sheets[Levels].Any(pair => Regex.IsMatch(pair.Key, @"^A\d+$")
            && int.Parse(pair.Key.Substring(1), CultureInfo.InvariantCulture) > lastRow
            && (Raw(pair.Value).Length > 0 || pair.Value.Element(Ns + "f") != null));
        public bool HasRuleAfter(int lastRow) => _sheets[Rules].Any(pair => Regex.IsMatch(pair.Key, @"^[B-H]\d+$")
            && int.Parse(pair.Key.Substring(1), CultureInfo.InvariantCulture) > lastRow
            && (Raw(pair.Value).Length > 0 || pair.Value.Element(Ns + "f") != null));
    }
}
