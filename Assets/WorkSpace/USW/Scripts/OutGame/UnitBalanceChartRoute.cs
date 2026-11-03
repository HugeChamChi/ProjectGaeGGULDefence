using System;

/// <summary>Unit balance chart names for the future loader. Does not fetch or apply data.</summary>
public static class UnitBalanceChartRoute
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const string Environment = "DEV";
#else
    public const string Environment = "LIVE";
#endif

    // No runtime override or fallback to another environment.
    public static string GetChartName(string table)
    {
        switch (table)
        {
            case "Units":
            case "BaseStats":
            case "UpgradeStats":
                return Environment + "_" + table;
            default:
                throw new ArgumentException("Unsupported unit balance table: " + table, nameof(table));
        }
    }
}
