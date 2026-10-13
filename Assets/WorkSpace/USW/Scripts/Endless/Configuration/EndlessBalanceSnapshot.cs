using System;
using System.Globalization;
using LitJson;

/// <summary>Validated immutable NORMAL balance, captured once per run without editing authored assets.</summary>
public sealed class EndlessBalanceSnapshot
{
    /// <summary>Starting countdown in seconds.</summary>
    public float StartSeconds { get; }
    /// <summary>HP growth percentage; 13 means multiplication by 1.13.</summary>
    public double HpGrowthPercent { get; }
    /// <summary>Breather boss time reward in seconds.</summary>
    public float BreatherSeconds { get; }
    /// <summary>Hard boss time reward in seconds.</summary>
    public float HardSeconds { get; }
    private EndlessBalanceSnapshot(float start, double hp, float breather, float hard)
    { StartSeconds = start; HpGrowthPercent = hp; BreatherSeconds = breather; HardSeconds = hard; }

    /// <summary>Accepts exactly one version-1 NORMAL row; malformed payloads reject the entire override.</summary>
    public static bool TryParse(JsonData rows, out EndlessBalanceSnapshot snapshot)
    {
        snapshot = null;
        try
        {
            if (rows == null || !rows.IsArray || rows.Count != 1) return false;
            var row = rows[0];
            if (row["Mode"].ToString() != "NORMAL" || row["SchemaVersion"].ToString() != "1") return false;
            if (!Read(row, "StartSeconds", out double start) || start <= 0 || start > float.MaxValue
                || !Read(row, "HpGrowthPercent", out double hp)
                || !Read(row, "BreatherSeconds", out double breather) || breather > float.MaxValue
                || !Read(row, "HardSeconds", out double hard) || hard > float.MaxValue) return false;
            snapshot = new EndlessBalanceSnapshot((float)start, hp, (float)breather, (float)hard);
            return snapshot.StartSeconds > 0;
        }
        catch (Exception) { return false; }
    }

    private static bool Read(JsonData row, string key, out double value)
    {
        value = 0;
        return row[key] != null && double.TryParse(row[key].ToString(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out value) && value >= 0 && !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
