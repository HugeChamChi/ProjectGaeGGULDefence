using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

/// <summary>Boss_스탯 입력 표를 읽는다. 다른 탭의 수식/설계 데이터는 Import하지 않는다.</summary>
public static class BossBalanceWorkbook
{
    /// <summary>검증을 마친 한 행의 값.</summary>
    public sealed class Row
    {
        public int Number;
        public string Key;
        public string Name;
        public string PrefabKey;
        public long Hp;
        public double Defense;
        public int Lines;
        public float Exp;
    }

    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly string[] Headers = { "키", "이름", "최대 HP", "방어력", "체력줄 수", "경험치 보상", "프리팹 키" };

    /// <summary>일반 .xlsx를 읽어 전체 입력을 검증한다. 입력 셀의 수식은 거부한다.</summary>
    public static IReadOnlyList<Row> Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var workbook = Load(zip, "xl/workbook.xml");
        var sheet = workbook.Descendants(Ns + "sheet").SingleOrDefault(x => (string)x.Attribute("name") == "Boss_스탯")
            ?? throw new FormatException("Boss_스탯 탭이 없습니다.");
        var rels = Load(zip, "xl/_rels/workbook.xml.rels");
        string id = (string)sheet.Attribute(RelNs + "id");
        var rel = rels.Root.Elements().SingleOrDefault(x => (string)x.Attribute("Id") == id);
        if (rel == null || (string)rel.Attribute("TargetMode") == "External") throw new FormatException("잘못된 시트 연결입니다.");
        string target = (string)rel.Attribute("Target");
        if (string.IsNullOrEmpty(target)) throw new FormatException("시트 경로가 없습니다.");
        string part = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
        var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() :
            Load(zip, "xl/sharedStrings.xml").Root.Elements(Ns + "si").Select(Text).ToArray();
        var rows = Load(zip, part).Descendants(Ns + "sheetData").Elements(Ns + "row").ToArray();
        var header = rows.SingleOrDefault(x => (string)x.Attribute("r") == "2")
            ?? throw new FormatException("Boss_스탯 2행에 헤더가 필요합니다.");
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cell in header.Elements(Ns + "c"))
        {
            string label = Value(cell, strings);
            if (!Headers.Contains(label) && label != "#") continue;
            if (columns.ContainsKey(label)) throw new FormatException($"중복 헤더: {label}");
            columns.Add(label, Column(cell));
        }
        foreach (string label in Headers)
            if (!columns.ContainsKey(label)) throw new FormatException($"Boss_스탯: '{label}' 열이 없습니다.");

        var output = new List<Row>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            int number = int.Parse((string)row.Attribute("r"), CultureInfo.InvariantCulture);
            if (number <= 2) continue;
            var cells = row.Elements(Ns + "c").ToDictionary(Column, x => x);
            string Get(string label)
            {
                if (!cells.TryGetValue(columns[label], out var cell)) return "";
                if (cell.Element(Ns + "f") != null)
                    throw new FormatException($"Boss_스탯 {number}행 '{label}': 입력은 수식 대신 값으로 붙여넣으세요.");
                return Value(cell, strings);
            }
            if (columns.ContainsKey("#") && Get("#").Trim() == "#") continue;
            if (Headers.All(h => string.IsNullOrWhiteSpace(Get(h)))) continue;
            string key = Get("키");
            if (!BossBalanceRegistry.IsValidKey(key) || !keys.Add(key)) throw new FormatException($"{number}행: 잘못되거나 중복된 보스 키 '{key}'");
            string prefab = Get("프리팹 키");
            if (!BossBalanceRegistry.IsValidKey(prefab)) throw new FormatException($"{number}행: 잘못된 프리팹 키 '{prefab}'");
            string name = Get("이름");
            if (string.IsNullOrWhiteSpace(name)) throw new FormatException($"{number}행: 이름이 비어 있습니다.");
            long hp = Integer(Get("최대 HP"), 1, (long)CombatHealth.MaximumHp, number, "최대 HP");
            int lines = (int)Integer(Get("체력줄 수"), 1, int.MaxValue, number, "체력줄 수");
            double defense = Nonnegative(Get("방어력"), number, "방어력");
            double exp = Nonnegative(Get("경험치 보상"), number, "경험치 보상");
            if (exp > float.MaxValue) throw new FormatException($"{number}행: 경험치가 float 범위를 초과합니다.");
            output.Add(new Row { Number = number, Key = key, Name = name, PrefabKey = prefab, Hp = hp, Defense = defense, Lines = lines, Exp = (float)exp });
        }
        if (output.Count == 0) throw new FormatException("Import할 보스 행이 없습니다.");
        return output;
    }

    private static XDocument Load(ZipArchive zip, string part)
    {
        var entry = zip.GetEntry(part) ?? throw new FormatException($"XLSX 항목 없음: {part}");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16000000 });
        return XDocument.Load(reader);
    }

    private static string Column(XElement cell) => new string(((string)cell.Attribute("r") ?? "").TakeWhile(char.IsLetter).ToArray());
    private static string Text(XElement element) => string.Concat(element.Descendants(Ns + "t").Select(x => x.Value));
    private static string Value(XElement cell, string[] strings)
    {
        string type = (string)cell.Attribute("t");
        string value = cell.Element(Ns + "v")?.Value ?? "";
        if (type == "e") throw new FormatException($"셀 {cell.Attribute("r")?.Value}: Excel 오류 {value}");
        if (type == "inlineStr") return Text(cell);
        if (type != "s") return value;
        if (!int.TryParse(value, out int index) || index < 0 || index >= strings.Length) throw new FormatException("잘못된 shared string 참조입니다.");
        return strings[index];
    }

    private static long Integer(string input, long min, long max, int row, string field)
    {
        if (!decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value) || value != decimal.Truncate(value) || value < min || value > max)
            throw new FormatException($"{row}행 '{field}': {min}~{max} 범위의 정수가 필요합니다.");
        return (long)value;
    }

    private static double Nonnegative(string input, int row, string field)
    {
        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw new FormatException($"{row}행 '{field}': 0 이상의 유한한 수가 필요합니다.");
        return value;
    }
}
