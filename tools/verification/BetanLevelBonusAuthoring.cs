using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BetanLevelBonusAuthoring
{
    public const string UnitPath = "Assets/WorkSpace/USW/Data/DroneUnits/UnitData_Betan.asset";
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring.");
        var data = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        Undo.RecordObject(data, "Apply approved Betan balance");
        data.atk = new PerTierFloat { normal = 136, rare = 197, epic = 257, legend = 1234 };
        data.attackSpeed = new PerTierFloat { normal = 1.32f, rare = 1.43f, epic = 1.54f, legend = 1.65f };
        data.skillCooldown = new PerTierFloat { normal = 6, rare = 6, epic = 6, legend = 6 };
        data.maxDroneCount = new PerTierInt { normal = 1, rare = 2, epic = 3, legend = 4 };
        data.CombatDroneAttackCoefficient = .3f;
        data.SelfDestructAttackCoefficient = 3.6f;
        data.SelfDestructCoefficients = new PerTierFloat { normal = 3.6f, rare = 7.8f, epic = 12.6f, legend = 18f };
        data.LevelBonuses = new List<UnitLevelBonusDefinition> {
            Stat(3, "공격력 +5%", UnitBonusStat.AttackPercent, .05f),
            Cycle(6, "매 2번째 기본 자폭 피해 +40% (선택지 추가 자폭 제외)", .4f, 1f),
            Stat(9, "일반공격 속도 +5%", UnitBonusStat.BasicAttackFrequencyPercent, .05f),
            Cycle(12, "매 2번째 기본 자폭 충전시간 -30% (선택지 추가 자폭 제외)", 0f, .7f),
            Stat(15, "유닛 치명타 피해 +50%p", UnitBonusStat.CriticalDamagePoints, .5f)
        };
        EditorUtility.SetDirty(data);
        SetCard<PeriodicBombDefinition>(1, effect => effect.PeriodSeconds = 15f, card => card.droneEffect.Interval = 15f);
        SetCard<DroneAttackSpeedDefinition>(2, effect => effect.BonusRatio = .1f, card => card.droneEffect.Value = .1f);
        SetCard<FleetBombDefinition>(3, effect => effect.PeriodSeconds = 60f, card => card.droneEffect.Interval = 60f);
        SetCard<ExplosionSkillRecoveryDefinition>(15, effect => effect.Seconds = .15f, card => card.droneEffect.Value = .15f);
        AssetDatabase.SaveAssets();
        return "Applied Betan values, five composed milestones, and four existing choice values.";
    }
    private static UnitLevelBonusDefinition Stat(int level, string text, UnitBonusStat stat, float value) =>
        new UnitLevelBonusDefinition { Level = level, Description = text,
            Parts = new List<UnitLevelBonusPart> { new StatModifierBonus { Stat = stat, Value = value } } };
    private static UnitLevelBonusDefinition Cycle(int level, string text, float damage, float charge) =>
        new UnitLevelBonusDefinition { Level = level, Description = text,
            Parts = new List<UnitLevelBonusPart> { new EveryNthCycleBonus { CycleId = UnitLevelBonusController.BasicSkillCycle,
                Every = 2, DamageBonusRatio = damage, ChargeTimeMultiplier = charge } } };
    private static void SetCard<T>(int index, Action<T> configure, Action<LevelUpData> legacy) where T : SelectionEffectDefinition
    {
        var card = AssetDatabase.LoadAssetAtPath<LevelUpData>($"Assets/WorkSpace/USW/Data/SelectionData/DroneSelection{index}.asset");
        Undo.RecordObject(card, "Apply approved Betan choice");
        configure(card.Composition.Effects.OfType<T>().Single());
        legacy(card);
        EditorUtility.SetDirty(card);
    }
}
