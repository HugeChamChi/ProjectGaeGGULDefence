using System;
using System.Collections.Generic;
using System.Linq;
using GGD.BackendCharts;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit-mode checks for the in-game level table (TODO VC8-LT, LV-REDESIGN 2026-11-16): bundled rows match the confirmed
/// table, the workbook matches the bundled rows, and malformed charts are rejected.
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

            // 2026-11-16 LV-REDESIGN table (outputs/level-effects-redesign-20261116/DESIGN.md).
            Check(E(S("Disigman", 15, UnitBonusStat.BasicAttackFrequencyPercent), .2f), "Disigman attack speed +10% at 3 and 9");
            Check(E(S("Disigman", 5, UnitBonusStat.SkillRepeatEvery), 0) && E(S("Disigman", 6, UnitBonusStat.SkillRepeatEvery), 3), "Disigman 6: every 3rd skill repeats");
            Check(E(S("Disigman", 11, UnitBonusStat.ChargeAttackReductionFlat), 0) && E(S("Disigman", 12, UnitBonusStat.ChargeAttackReductionFlat), 1), "Disigman 12: charge -1");
            Check(E(S("Disigman", 15, UnitBonusStat.AttackPercent), .3f) && E(S("Disigman", 15, UnitBonusStat.SkillDamagePercent), 0), "Disigman 15: attack +30%, no skill damage rows");
            Check(E(S("Betan", 15, UnitBonusStat.AttackPercent), .3f), "Betan attack +10% (3) +20% (15)");
            Check(E(S("Betan", 15, UnitBonusStat.DroneCountFlat), 0), "Betan drones follow the arm table, no level-effect drones (11-15 decision)");
            Check(E(S("Betan", 15, UnitBonusStat.BombDamagePercent), .1f), "Betan bomb +10% only at 6");
            Check(E(S("Betan", 9, UnitBonusStat.SkillCooldownPercent), .15f), "Betan cooldown -15%");
            Check(E(S("Betan", 11, UnitBonusStat.ThirdSkillExtraBomb), 0), "Betan no extra bomb before 12");
            Check(E(S("Betan", 12, UnitBonusStat.ThirdSkillExtraBomb), 1) && E(S("Betan", 12, UnitBonusStat.BombCycleEvery), 3), "Betan 12: every 3rd skill +1 bomb");
            Check(E(S("Deltan", 15, UnitBonusStat.ExtraProjectileCount), 0), "Deltan has no extra projectiles (11-16 decision)");
            Check(E(S("Deltan", 15, UnitBonusStat.BasicAttackFrequencyPercent), .15f), "Deltan attack speed +5% (3) +10% (15)");
            Check(E(S("Deltan", 5, UnitBonusStat.HackingCapacityFlat), 0) && E(S("Deltan", 6, UnitBonusStat.HackingCapacityFlat), 5), "Deltan 6: capacity +5");
            Check(E(S("Deltan", 9, UnitBonusStat.AttackPercent), .15f), "Deltan 9: attack +15%");
            Check(E(S("Deltan", 11, UnitBonusStat.HackingBasicDamagePerStack), 0) && E(S("Deltan", 12, UnitBonusStat.HackingBasicDamagePerStack), .005f), "Deltan 12: 0.5% per stack");
            Check(E(S("Gamman", 15, UnitBonusStat.SkillDamagePercent), .3f), "Gamman detonation damage +10%+20%");
            Check(E(S("Gamman", 15, UnitBonusStat.SkillCooldownPercent), .3f), "Gamman cooldown -10%-20%");
            Check(E(S("Gamman", 11, UnitBonusStat.HackingSaveFlat), 0) && E(S("Gamman", 12, UnitBonusStat.HackingSaveFlat), 3), "Gamman 12: 3 stacks saved");
            Check(E(S("Gamman", 15, UnitBonusStat.HackingRefundPercent), 0), "Gamman no percent refund");
            Check(E(S("Zeltan", 15, UnitBonusStat.FoodPercent), .3f), "Zeltan food +10% (3) +20% (15)");
            Check(E(S("Zeltan", 15, UnitBonusStat.FoodSpeedPercent), .1f), "Zeltan food speed +10% at 9");
            Check(E(S("Zeltan", 5, UnitBonusStat.BossFoodSeconds), 0) && E(S("Zeltan", 6, UnitBonusStat.BossFoodSeconds), 5), "Zeltan 6: boss kill 5s food");
            Check(E(S("Zeltan", 11, UnitBonusStat.FoodBurstChance), 0), "Zeltan no burst before 12");
            Check(E(S("Zeltan", 12, UnitBonusStat.FoodBurstChance), .5f) && E(S("Zeltan", 12, UnitBonusStat.FoodBurstIntervalSeconds), 10)
                && E(S("Zeltan", 12, UnitBonusStat.FoodBurstDurationSeconds), 5) && E(S("Zeltan", 12, UnitBonusStat.FoodBurstMultiplier), 2),
                "Zeltan 12: 50% every 10s, 5s, x2");
            Check(table["Betan"][0].Description == "공격력 +10%", "description text kept");
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
            Check(Rejects("fraction", rows => { First(rows, "HackingCapacityFlat")["Value"] = "1.5"; return rows; }), "rejects fractional whole-number stat");
            Check(Rejects("cooldown", rows => { First(rows, "SkillCooldownPercent")["Value"] = "1"; return rows; }), "rejects cooldown 100%");
            Check(Rejects("gap", rows => { First(rows, "BombCycleEvery")["PartIndex"] = "2"; return rows; }), "rejects PartIndex gap");
            Check(Rejects("description", rows => { rows[0]["Description"] = ""; return rows; }), "rejects empty description");
            Check(Rejects("period", rows => { First(rows, "SkillRepeatEvery")["Value"] = "0"; return rows; }), "rejects period 0");
            Check(Rejects("unknown unit", rows => { rows[0]["UnitKey"] = "Frog"; return rows; }), "rejects unknown unit");
            Check(Rejects("column", rows => { ((System.Collections.IDictionary)rows[0]).Remove("Value"); return rows; }), "rejects missing column");
        }
        catch (Exception error) { failures.Add("ERROR " + error.GetType().Name + ": " + error.Message); }
        finally { UnitTotemLevelBonusChart.Clear(); }

        return "[UnitTotemLevelBonusChecks] PASS " + pass + " FAIL " + failures.Count + (failures.Count > 0 ? "\n" + string.Join("\n", failures) : "");
    }
}
