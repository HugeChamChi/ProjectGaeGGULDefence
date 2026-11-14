using System;
using System.Collections.Generic;
using System.Linq;
using GGD.BackendCharts;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit-mode checks for the in-game level table (TODO VC8-LT): bundled rows match the confirmed table that used to live
/// in PrototypeLevelBonuses.cs (c051b018), the workbook matches the bundled rows, and malformed charts are rejected.
/// </summary>
public static class UnitTotemLevelBonusChecks
{
    [MenuItem("Tools/Balance/Checks/UnitTotemLevelBonuses 검사")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        var failures = new List<string>();
        int pass = 0;
        void Check(bool ok, string name) { if (ok) pass++; else failures.Add("FAIL " + name); }

        UnitTotemLevelBonusChart.Clear();
        try
        {
            var table = UnitTotemLevelBonusChart.Bundled();
            Check(table.Count == 5, "five units");
            foreach (var key in UnitTotemLevelBonusChart.UnitKeys)
                Check(table[key].Select(d => d.Level).SequenceEqual(new[] { 3, 6, 9, 12, 15 }), key + " milestones 3/6/9/12/15");

            float S(string key, int level, UnitBonusStat stat) => new UnitLevelBonusController(table[key], level).GetStat(stat);
            bool E(float actual, float expected) => Math.Abs(actual - expected) < 1e-5f;

            Check(E(S("Disigman", 15, UnitBonusStat.BasicAttackFrequencyPercent), .2f), "Disigman attack speed +10%+10%");
            Check(E(S("Disigman", 11, UnitBonusStat.SkillDamagePercent), .4f), "Disigman skill damage +20%+20%");
            Check(E(S("Disigman", 15, UnitBonusStat.ChargeAttackReductionFlat), 1), "Disigman charge -1");
            Check(E(S("Betan", 3, UnitBonusStat.AttackPercent), .1f), "Betan attack +10%");
            Check(E(S("Betan", 15, UnitBonusStat.DroneCountFlat), 4), "Betan drones +4");
            Check(E(S("Betan", 12, UnitBonusStat.BombDamagePercent), .25f), "Betan bomb +10%+15%");
            Check(E(S("Betan", 9, UnitBonusStat.SkillCooldownPercent), .15f), "Betan cooldown -15%");
            Check(E(S("Betan", 14, UnitBonusStat.ThirdSkillExtraBomb), 0), "Betan no extra bomb before 15");
            Check(E(S("Betan", 15, UnitBonusStat.ThirdSkillExtraBomb), 1) && E(S("Betan", 15, UnitBonusStat.BombCycleEvery), 3), "Betan Lv15 every 3rd skill +1 bomb");
            // Old code: before 6 none, 6-8 one per 10, 9-14 two per 10, 15 one per 3.
            (int, int) D(int level) => ((int)S("Deltan", level, UnitBonusStat.ExtraProjectileCount), (int)S("Deltan", level, UnitBonusStat.ExtraProjectileEvery));
            Check(D(5).Item1 == 0, "Deltan no extra projectile before 6");
            Check(D(6) == (1, 10) && D(8) == (1, 10), "Deltan 6-8: 1 per 10");
            Check(D(9) == (2, 10) && D(14) == (2, 10), "Deltan 9-14: 2 per 10");
            Check(D(15) == (1, 3), "Deltan 15: 1 per 3");
            Check(E(S("Deltan", 3, UnitBonusStat.BasicAttackFrequencyPercent), .05f), "Deltan attack speed +5%");
            Check(E(S("Deltan", 12, UnitBonusStat.HackingBasicDamagePerStack), .005f), "Deltan 0.5% per stack");
            Check(E(S("Deltan", 15, UnitBonusStat.HackingCapacityFlat), 15), "Deltan capacity +15");
            Check(E(S("Gamman", 15, UnitBonusStat.SkillDamagePercent), .3f), "Gamman detonation damage +10%+20%");
            Check(E(S("Gamman", 15, UnitBonusStat.SkillCooldownPercent), .3f), "Gamman cooldown -10%-20%");
            Check(E(S("Gamman", 15, UnitBonusStat.HackingRefundPercent), .2f), "Gamman refund 20%");
            Check(E(S("Zeltan", 15, UnitBonusStat.FoodPercent), .3f), "Zeltan food +10%+20%");
            Check(E(S("Zeltan", 15, UnitBonusStat.FoodSpeedPercent), .3f), "Zeltan food speed +10%+20%");
            Check(E(S("Zeltan", 14, UnitBonusStat.FoodBurstChance), 0), "Zeltan no burst before 15");
            Check(E(S("Zeltan", 15, UnitBonusStat.FoodBurstChance), .5f) && E(S("Zeltan", 15, UnitBonusStat.FoodBurstIntervalSeconds), 10)
                && E(S("Zeltan", 15, UnitBonusStat.FoodBurstDurationSeconds), 5) && E(S("Zeltan", 15, UnitBonusStat.FoodBurstMultiplier), 2),
                "Zeltan Lv15 50% every 10s, 5s, x2");
            Check(table["Betan"][0].Description == "공격력 +10%, 전투 드론 +1기", "description text kept");
            Check(!ReferenceEquals(UnitTotemLevelBonusChart.Bundled()["Betan"][0], new List<UnitLevelBonusDefinition>()), "bundled table cached");

            string workbook = UnitTotemLevelBonusesWorkbook.Check(UnitTotemLevelBonusesWorkbook.SourcePath);
            Check(workbook.Contains("전부 같음"), "xlsx equals bundled: " + workbook.Replace("\n", " | "));

            bool Rejects(string name, Func<JsonData, JsonData> mutate)
            {
                var rows = UnitTotemLevelBonusesWorkbook.BundledRows();
                try { UnitTotemLevelBonusChart.Parse(mutate(rows)); return false; }
                catch (FormatException) { return true; }
            }
            JsonData First(JsonData rows, string stat) { foreach (JsonData row in rows) if (row["Stat"].ToString() == stat) return row; return null; }
            Check(Rejects("missing unit", rows =>
            {
                var next = new JsonData(); next.SetJsonType(JsonType.Array);
                foreach (JsonData row in rows) if (row["UnitKey"].ToString() != "Zeltan") next.Add(row);
                return next;
            }), "rejects missing unit");
            Check(Rejects("unknown stat", rows => { rows[0]["Stat"] = "Nope"; return rows; }), "rejects unknown stat");
            Check(Rejects("numeric stat", rows => { rows[0]["Stat"] = "3"; return rows; }), "rejects numeric stat");
            Check(Rejects("negative", rows => { rows[0]["Value"] = "-1"; return rows; }), "rejects negative value");
            Check(Rejects("fraction", rows => { First(rows, "DroneCountFlat")["Value"] = "1.5"; return rows; }), "rejects fractional drone count");
            Check(Rejects("cooldown", rows => { First(rows, "SkillCooldownPercent")["Value"] = "1"; return rows; }), "rejects cooldown 100%");
            Check(Rejects("gap", rows => { First(rows, "DroneCountFlat")["PartIndex"] = "2"; return rows; }), "rejects PartIndex gap");
            Check(Rejects("description", rows => { rows[0]["Description"] = ""; return rows; }), "rejects empty description");
            Check(Rejects("period", rows => { First(rows, "ExtraProjectileEvery")["Value"] = "0"; return rows; }), "rejects period 0");
            Check(Rejects("unknown unit", rows => { rows[0]["UnitKey"] = "Frog"; return rows; }), "rejects unknown unit");
            Check(Rejects("column", rows => { ((System.Collections.IDictionary)rows[0]).Remove("Value"); return rows; }), "rejects missing column");
        }
        catch (Exception error) { failures.Add("ERROR " + error.GetType().Name + ": " + error.Message); }
        finally { UnitTotemLevelBonusChart.Clear(); }

        return "[UnitTotemLevelBonusChecks] PASS " + pass + " FAIL " + failures.Count + (failures.Count > 0 ? "\n" + string.Join("\n", failures) : "");
    }
}
