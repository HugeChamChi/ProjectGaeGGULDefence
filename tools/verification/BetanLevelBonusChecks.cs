using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BetanLevelBonusChecks
{
    public static string Regression() { DroneSelectionChecks.Run(); return "PASS existing drone selection regression."; }
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    private static void Near(float actual, float expected, string message)
        => Check(Math.Abs(actual - expected) < .001f, $"{message}: {actual} != {expected}");

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var random = UnityEngine.Random.state;
        try { return RunInScene(scene); }
        finally { EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Random.state = random; }
    }

    private static string RunInScene(Scene scene)
    {
        checks = 0;
        var data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/DroneUnits/UnitData_Betan.asset");
        string original = EditorJsonUtility.ToJson(data);
        var attack = new[] { 136f, 197f, 257f, 1234f };
        var coefficient = new[] { 3.6f, 7.8f, 12.6f, 18f };
        int index = 0;
        foreach (var tier in TierUtil.All)
        {
            Near(data.atk.Get(tier), attack[index], "attack");
            Near(data.GetSelfDestructCoefficient(tier), coefficient[index], "bomb coefficient");
            Near(data.skillCooldown.Get(tier), 6f, "base charge");
            Check(data.maxDroneCount.Get(tier) == index + 1, "drone capacity");
            index++;
        }
        Near(data.CombatDroneAttackCoefficient, .3f, "normal coefficient");
        foreach (int level in new[] { 1, 2, 3, 5, 6, 8, 9, 11, 12, 14, 15 })
        {
            var go = new GameObject("Betan bonus check");
            SceneManager.MoveGameObjectToScene(go, scene);
            var crit = ScriptableObject.CreateInstance<BuffData>();
            try
            {
                var betan = go.AddComponent<Drone_Betan>();
                betan.unitData = data;
                var levels = new UnitLevelSnapshot(new Dictionary<string, int> { [data.name] = level });
                betan.Init(new UnitDependencies { UnitLevels = levels });
                var bonus = betan.LevelBonuses;
                Near(bonus.GetStat(UnitBonusStat.AttackPercent), level >= 3 ? .05f : 0f, "attack unlock");
                Near(bonus.GetStat(UnitBonusStat.BasicAttackFrequencyPercent), level >= 9 ? .05f : 0f, "speed unlock");
                Near(bonus.GetStat(UnitBonusStat.CriticalDamagePoints), level >= 15 ? .5f : 0f, "critical points");
                Near(betan.GetUpgradedAtk(), 136f * (level >= 3 ? 1.05f : 1f), "runtime attack");
                Near(betan.GetCurrentAttackInterval(), 1f / (1.32f * (level >= 9 ? 1.05f : 1f)), "runtime frequency");
                int display = betan.GetCombatDroneDisplayDamage();
                Check(display == DamageCalculator.ApplyRounding(betan.GetUpgradedAtk() * .3f), "drone display coefficient");
                crit.effects.Add(new StatModifierBuffEffect { kind = StatKind.CritChance,
                    amountPerStack = new ConstantFloat { value = 1f } });
                betan.Buffs.ApplyBuff(crit);
                float critMultiplier = level >= 15 ? 2f : 1.5f;
                int droneDamage = betan.GetCombatDroneDamage(out bool droneCrit);
                Check(droneCrit && droneDamage == DamageCalculator.ApplyRounding(betan.GetUpgradedAtk() * .3f * critMultiplier), "drone critical +50pp");
                int bombDamage = betan.ComputeAttackDamageFrom(betan.GetUpgradedAtk() * 3.6f, 1f, out bool bombCrit, 1.4f);
                Check(bombCrit && bombDamage == DamageCalculator.ApplyRounding(betan.GetUpgradedAtk() * 3.6f * 1.4f * critMultiplier), "bomb damage bonus before rounding and critical");
                for (int n = 1; n <= 6; n++)
                {
                    var next = bonus.PeekCycle(UnitLevelBonusController.BasicSkillCycle);
                    Check(next.Number == n, "shared base ordinal");
                    Near(next.DamageMultiplier, level >= 6 && n % 2 == 0 ? 1.4f : 1f, "alternating damage");
                    Near(betan.GetCurrentSkillInterval(), level >= 12 && n % 2 == 0 ? 4.2f : 6f, "alternating runtime charge");
                    betan.SpawnSelfDestructDrones(2);
                    betan.SpawnFleetBomb(0);
                    Check(bonus.PeekCycle(UnitLevelBonusController.BasicSkillCycle).Number == n, "extra bombs never advance base counter");
                    betan.InvokeOnSkillFull();
                    Check(bonus.PeekCycle(UnitLevelBonusController.BasicSkillCycle).Number == n + 1, "base action advances once");
                }
                var independent = new UnitLevelBonusController(data.LevelBonuses, level);
                Check(independent.PeekCycle(UnitLevelBonusController.BasicSkillCycle).Number == 1, "new owner starts independently");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(crit); }
        }
        // A charged bomb keeps its value even while the owner advances to another cycle.
        var snapshot = new UnitLevelBonusController(data.LevelBonuses, 15);
        snapshot.CompleteCycle(UnitLevelBonusController.BasicSkillCycle);
        var second = snapshot.CompleteCycle(UnitLevelBonusController.BasicSkillCycle);
        snapshot.CompleteCycle(UnitLevelBonusController.BasicSkillCycle);
        Near(second.DamageMultiplier, 1.4f, "captured pending damage");
        Near(snapshot.PeekCycle("other_action").DamageMultiplier, 1f, "named cycles are isolated");
        var source = new Dictionary<string, int> { [data.name] = 15 };
        var immutable = new UnitLevelSnapshot(source);
        source[data.name] = 1;
        Check(immutable.GetLevel(data.name) == 15 && immutable.GetLevel("unknown") == 1, "level snapshot copy");
        Check(EditorJsonUtility.ToJson(data) == original, "combat never mutates authored SO");
        Near(Card<PeriodicBombDefinition>(1).PeriodSeconds, 15f, "periodic selection");
        Near(Card<DroneAttackSpeedDefinition>(2).BonusRatio, .1f, "attack speed selection");
        Near(Card<FleetBombDefinition>(3).PeriodSeconds, 60f, "fleet selection");
        Near(Card<ExplosionSkillRecoveryDefinition>(15).Seconds, .15f, "repair kit");
        return $"PASS {checks} checks: tiers, milestone boundaries, runtime stats, alternating cycles, extra origins, snapshots, authored data and choices.";
    }
    private static T Card<T>(int index) where T : SelectionEffectDefinition => AssetDatabase.LoadAssetAtPath<LevelUpData>(
        $"Assets/WorkSpace/USW/Data/SelectionData/DroneSelection{index}.asset").Composition.Effects.OfType<T>().Single();
}
