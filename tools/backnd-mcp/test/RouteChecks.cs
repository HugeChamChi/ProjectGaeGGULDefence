using System;

public static class RouteChecks
{
    public static void Main(string[] args)
    {
        string expected = args[0];
        if (UnitBalanceChartRoute.Environment != expected) throw new Exception("Environment mismatch");
        foreach (string table in new[] { "Units", "BaseStats", "UpgradeStats" })
            if (UnitBalanceChartRoute.GetChartName(table) != expected + "_" + table)
                throw new Exception("Chart name mismatch");
        foreach (string invalid in new[] { "DEV_Units", "LIVE_Units", "units", "LevelBonuses", "", null })
        {
            try { UnitBalanceChartRoute.GetChartName(invalid); }
            catch (ArgumentException) { continue; }
            throw new Exception("Invalid chart accepted");
        }
        Console.WriteLine("PASS " + expected + ": route names and invalid tables");
    }
}
