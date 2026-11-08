using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LitJson;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ResearchSkillDamageChecks
{
    private const string Output = "outputs/research-skill-damage-20261108/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Results = new List<string>();
    private static void Check(bool condition, string name)
    {
        Results.Add((condition ? "PASS " : "FAIL ") + name);
        File.WriteAllLines(Output + "unity-checks.txt", Results);
        if (!condition) throw new Exception(name);
    }

    public static string Run()
    {
        Results.Clear();
        Directory.CreateDirectory(Output);
        string json = File.ReadAllText("Assets/WorkSpace/USW/Resources/ResearchBalance.json");
        var trees = ResearchChartParser.Parse(JsonMapper.ToObject(json));
        var legacy = ResearchChartParser.Parse(JsonMapper.ToObject(json.Replace("SkillDamage", "BurnDamage")));
        var root = new GameObject("ResearchSkillDamageCheck");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<UnitData>();
        var skill = ScriptableObject.CreateInstance<SkillData>();
        var basic = ScriptableObject.CreateInstance<SkillData>();
        try
        {
            Check((int)ResearchStat.SkillDamage == 8, "Serialized stat ID remains 8");
            Check(trees.Values.Sum(t => t.Nodes.Count(n => n.Stat == ResearchStat.SkillDamage)) == 4, "All four chart nodes use SkillDamage");
            Check(legacy.Values.Sum(t => t.Nodes.Count(n => n.Stat == ResearchStat.SkillDamage)) == 4, "Previous uploaded chart remains readable");
            var node = trees["BASIC"].Find("6-2");
            var progress = new ResearchProgress(trees["BASIC"], new Dictionary<string, int> { { "6-2", 3 } });
            Check(progress.GetLevel(node) == 3 && Math.Abs(progress.GetTotal(ResearchStat.SkillDamage) - .012f) < .00001f,
                "Existing level 3 becomes 1.2 percent skill damage with no reset");
            Check(node.MaxLevel == 5 && Math.Abs(node.ValuePerLevel - .004f) < .00001f, "BASIC retains 0.4 percent per level and max level 5");
            var settings = AssetDatabase.LoadAssetAtPath<ResearchViewSettings>("Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeViewSettings.asset");
            Check(settings.GetDisplayName(trees["BASIC"], node) == "스킬 피해 증가", "Runtime chart UI displays skill damage");

            var research = new ResearchRunBonuses(null, null, null, null);
            var values = (float[])typeof(ResearchRunBonuses).GetField("_values", Private).GetValue(research);
            values[(int)ResearchStat.SkillDamage] = .5f;
            var deps = new UnitDependencies { Research = research };
            var unit = root.AddComponent<FrogWarrior>();
            var stats = root.AddComponent<UnitStatsModifier>();
            data.atk = new PerTierFloat { normal = 100 };
            data.skillData = skill; data.basicAttackData = basic; unit.unitData = data;
            stats.Init(unit, deps);
            typeof(UnitBase).GetField("_deps", Private).SetValue(unit, deps);
            typeof(UnitBase).GetField("_stats", Private).SetValue(unit, stats);
            var fixedSkill = new FixedDamage { power = new ConstantFloat { value = 100 } };
            var atkSkill = new AtkCoefficientDamage();
            var fixedBasic = new FixedDamage { power = new ConstantFloat { value = 100 } };
            var atkBasic = new AtkCoefficientDamage();
            skill.hitEffects = new List<IEffect> { fixedSkill, atkSkill };
            basic.hitEffects = new List<IEffect> { fixedBasic, atkBasic };
            var boss = root.AddComponent<BossNormal>();
            var debuffSettings = Resources.Load<DebuffSettings>("DebuffSettings");
            boss.ConfigureDebuffs(new DebuffCatalog(debuffSettings), debuffSettings, null, 0, null);
            boss.Init(10000m);
            Action<IEffect, int, string> hit = (effect, expected, label) =>
            {
                decimal hp = boss.CurrentHp;
                effect.Apply(unit, boss, Vector3.zero);
                Check(hp - boss.CurrentHp == expected, label);
            };
            hit(fixedSkill, 150, "Fixed skill 100 becomes 150 with 50 percent bonus");
            hit(atkSkill, 150, "Attack coefficient skill 100 becomes 150");
            hit(fixedBasic, 100, "Fixed basic damage remains 100");
            hit(atkBasic, 100, "Attack coefficient basic damage remains 100");
            foreach (IReplayableEffect effect in skill.hitEffects)
            {
                var captured = effect.Capture(unit);
                decimal hp = boss.CurrentHp;
                captured(boss, Vector3.zero);
                Check(hp - boss.CurrentHp == 150, "Captured skill damage receives bonus once: " + effect.GetType().Name);
            }
            typeof(BossBase).GetField("_research", Private).SetValue(boss, research);
            long burn = (long)typeof(BossBase).GetMethod("ScaleBurnDamage", Private).Invoke(boss, new object[] { 100L });
            Check(burn == 100L, "Skill research does not multiply burn damage");
            values[(int)ResearchStat.SkillDamage] = 0;
            hit(fixedSkill, 100, "Zero skill bonus retains original damage");
            return Results.Count + " checks passed";
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(data);
            Object.DestroyImmediate(skill); Object.DestroyImmediate(basic);
            ResearchChartParser.Destroy(trees); ResearchChartParser.Destroy(legacy);
        }
    }
}
