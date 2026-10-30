using System;
using System.Text;
using System.Security.Cryptography;
using LitJson;

namespace GGD.BackendCharts
{
    // One applied chart file is one complete configuration snapshot.
    public static class ChartSnapshotPayload
    {
        public static string Decode(string rowsJson)
        {
            var rows = JsonMapper.ToObject(rowsJson);
            if (!rows.IsArray || rows.Count == 0 || rows.Count > 4096) throw new ArgumentException("Invalid chart snapshot rows.");
            if (!rows[0].ContainsKey("Payload")) return ReadableChartRows.Decode(rows);
            var parts = new string[rows.Count];
            string release = null, digest = null, encoding = null;
            foreach (JsonData row in rows)
            {
                if (row["SchemaVersion"].ToString() != "1" || int.Parse(row["PartCount"].ToString()) != rows.Count)
                    throw new ArgumentException("Invalid chart snapshot version/count.");
                var key = row["ReleaseKey"].ToString(); var hash = row["Sha256"].ToString();
                string format = row.ContainsKey("Encoding") ? row["Encoding"].ToString() : "Base64";
                if ((format != "Base64" && format != "Json") || (encoding != null && encoding != format))
                    throw new ArgumentException("Invalid/mixed chart encoding.");
                encoding = format;
                if (string.IsNullOrWhiteSpace(key) || (release != null && release != key) || (digest != null && digest != hash))
                    throw new ArgumentException("Mixed chart snapshot releases.");
                release = key; digest = hash;
                int index = int.Parse(row["PartIndex"].ToString());
                if (index < 0 || index >= parts.Length || parts[index] != null) throw new ArgumentException("Invalid chart snapshot part.");
                parts[index] = row["Payload"].ToString();
                if (parts[index].Length == 0 || parts[index].Length > 2000) throw new ArgumentException("Invalid chart snapshot payload size.");
            }
            var bytes = encoding == "Json" ? new UTF8Encoding(false, true).GetBytes(string.Concat(parts)) : Convert.FromBase64String(string.Concat(parts));
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            if (actual != digest) throw new ArgumentException("Chart snapshot checksum mismatch.");
            return new UTF8Encoding(false, true).GetString(bytes);
        }
    }
}
