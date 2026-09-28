using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

/// <summary>밸런스 엑셀의 2행 헤더 표를 값/수식 구분을 유지하여 읽는다.</summary>
public sealed class BalanceXlsxTable
{
    /// <summary>원본 셀 주소를 포함한 한 행.</summary>
    public sealed class Row
    {
        internal readonly Dictionary<string, XElement> Cells = new(StringComparer.Ordinal);
        internal string[] Strings;
        internal string Sheet;
        /// <summary>원본 Excel 행 번호.</summary>
        public int Number { get; internal set; }
        /// <summary>입력 값을 읽는다. 수식과 Excel 오류를 허용하지 않는다.</summary>
        public string Get(string header)
        {
            if (!Cells.TryGetValue(header, out var cell)) return "";
            if (cell.Element(Ns + "f") != null) throw new FormatException($"{Sheet}!{cell.Attribute("r")?.Value}: '{header}' 입력은 값으로 붙여넣으세요.");
            return ReadValue(cell, Strings);
        }
        /// <summary>셀 위치와 함께 입력 오류를 보고한다.</summary>
        public FormatException Error(string header, string message) => new($"{Sheet} {Number}행 '{header}': {message}");
    }

    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private readonly List<Row> _rows = new();
    /// <summary>헤더 이후의 비어 있지 않은 원본 행.</summary>
    public IReadOnlyList<Row> Rows => _rows;

    /// <summary>필수 헤더가 있는 표 하나를 읽는다. 계산용 열은 입력으로 읽지 않으면 유지된다.</summary>
    public static BalanceXlsxTable Read(byte[] bytes, string sheetName, params string[] required)
    {
        using var input = new MemoryStream(bytes, false);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var sheet = Load(zip, "xl/workbook.xml").Descendants(Ns + "sheet").SingleOrDefault(x => (string)x.Attribute("name") == sheetName)
            ?? throw new FormatException($"필수 탭 없음: {sheetName}");
        var rel = Load(zip, "xl/_rels/workbook.xml.rels").Root.Elements().Single(x => (string)x.Attribute("Id") == (string)sheet.Attribute(relNs + "id"));
        if ((string)rel.Attribute("TargetMode") == "External") throw new FormatException("외부 연결 시트는 지원하지 않습니다.");
        string part = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), (string)rel.Attribute("Target")).AbsolutePath.TrimStart('/');
        var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() : Load(zip, "xl/sharedStrings.xml").Root.Elements(Ns + "si").Select(Text).ToArray();
        var rows = Load(zip, part).Descendants(Ns + "sheetData").Elements(Ns + "row").ToArray();
        var header = rows.SingleOrDefault(x => (string)x.Attribute("r") == "2") ?? throw new FormatException($"{sheetName}: 2행 헤더가 없습니다.");
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cell in header.Elements(Ns + "c"))
        {
            string label = ReadValue(cell, strings);
            if (string.IsNullOrWhiteSpace(label)) continue;
            if (!names.Add(label)) throw new FormatException($"{sheetName}: 중복 헤더 {label}");
            columns.Add(Column(cell), label);
        }
        foreach (string label in required) if (!names.Contains(label)) throw new FormatException($"{sheetName}: 필수 열 '{label}' 없음");
        var result = new BalanceXlsxTable();
        foreach (var source in rows)
        {
            int number = (int)source.Attribute("r");
            if (number <= 2) continue;
            var row = new Row { Number = number, Sheet = sheetName, Strings = strings };
            foreach (var cell in source.Elements(Ns + "c"))
                if (columns.TryGetValue(Column(cell), out string label)) row.Cells.Add(label, cell);
            if (row.Cells.Values.Any(c => c.Element(Ns + "f") != null || !string.IsNullOrEmpty(ReadValue(c, strings)))) result._rows.Add(row);
        }
        return result;
    }

    private static string Column(XElement cell) => new string(((string)cell.Attribute("r")).TakeWhile(char.IsLetter).ToArray());
    private static string Text(XElement cell) => string.Concat(cell.Descendants(Ns + "t").Select(x => x.Value));
    private static string ReadValue(XElement cell, string[] strings)
    {
        string type = (string)cell.Attribute("t");
        string value = cell.Element(Ns + "v")?.Value ?? "";
        if (type == "e") throw new FormatException($"Excel 오류: {cell.Attribute("r")?.Value} = {value}");
        if (type == "inlineStr") return Text(cell);
        if (type == "s")
        {
            if (!int.TryParse(value, out int index) || index < 0 || index >= strings.Length) throw new FormatException("잘못된 문자열 참조");
            return strings[index];
        }
        return value;
    }
    private static XDocument Load(ZipArchive zip, string part)
    {
        var entry = zip.GetEntry(part) ?? throw new FormatException($"XLSX 항목 없음: {part}");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16000000 });
        return XDocument.Load(reader);
    }
}
