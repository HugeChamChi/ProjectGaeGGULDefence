using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GGD.BackendCharts;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only bridge between the authoring workbook <c>Data/Balance/RunBalance.xlsx</c> and the bundled ScriptableObject values
/// (user decision 2026-11-11: Excel is the source; bundled SO values and the BACKND chart are both made from it).
/// The same workbook is uploaded to DEV_RunBalance, then LIVE_RunBalance. Never opened by a player build.
/// </summary>
public static class RunBalanceWorkbook
{
    public const string SourcePath = "Data/Balance/RunBalance.xlsx";
    public const string SheetName = "RunBalance";
    // Three GameConfig assets exist; IngameScene references this one (the other two are test configs).
    private const string GameConfigPath = "Assets/WorkSpace/HSD/Resources/Data/GameConfig.asset";

    /// <summary>Checks the workbook with the same rules the game applies to the chart and lists differences from bundled values.</summary>
    [MenuItem("Tools/Balance/RunBalance.xlsx 검사 (업로드 전)")]
    public static void CheckMenu() => Debug.Log(Check(SourcePath));

    /// <summary>Writes workbook values into the bundled ScriptableObjects after the whole workbook passed validation.</summary>
    [MenuItem("Tools/Balance/RunBalance.xlsx 값을 앱 내장 값에 반영")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath));

    /// <summary>Reads and validates the workbook. Throws <see cref="FormatException"/> naming the first problem.</summary>
    public static Dictionary<string, float[]> Read(string path)
    {
        var table = BalanceXlsxTable.Read(File.ReadAllBytes(path), SheetName, 1, "Key", "Value");
        var rows = new JsonData(); rows.SetJsonType(JsonType.Array);
        foreach (var row in table.Rows)
        {
            var item = new JsonData();
            item["Key"] = row.Get("Key") ?? "";
            item["Value"] = row.Get("Value") ?? "";
            rows.Add(item);
        }
        return RunBalance.Parse(rows);
    }

    public static string Check(string path)
    {
        var workbook = Read(path);
        var bundled = Bundled();
        var lines = new List<string> { "RunBalance.xlsx 형식 통과: 키 " + workbook.Count + "개" };
        foreach (var pair in workbook)
        {
            string excel = Text(pair.Value);
            if (!bundled.TryGetValue(pair.Key, out string app)) lines.Add("앱 내장 값 없음: " + pair.Key);
            else if (app != excel) lines.Add("다름 " + pair.Key + ": 엑셀 " + excel + " / 앱 내장 " + app);
        }
        if (lines.Count == 1) lines.Add("앱 내장 값과 전부 같음");
        return string.Join("\n", lines);
    }

    public static string Import(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before importing run balance.");
        var workbook = Read(path);
        float One(string key) => workbook[key][0];
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
        var upgrade = Single<UpgradeSettings>();
        var exp = Single<ExpLevelData>();
        var pool = Single<LevelUpPoolData>();
        if (config == null || upgrade == null || exp == null || pool == null) throw new InvalidOperationException("RunBalance import: a target asset is missing or ambiguous.");

        Undo.RecordObjects(new UnityEngine.Object[] { config, upgrade, exp, pool }, "Import RunBalance.xlsx");
        config.baseCritChance = One(RunBalance.CombatBaseCritChance);
        config.damageVariance = One(RunBalance.CombatDamageVariance);
        config.critDamageVariance = One(RunBalance.CombatCritDamageVariance);
        config.maxAttackSpeedMultiplier = One(RunBalance.CombatMaxAttackSpeedMultiplier);
        config.baseCritDamageMultiplier = One(RunBalance.CombatBaseCritDamageMultiplier);
        config.startingFood = One(RunBalance.EconomyStartingFood);
        config.summonInitialCost = One(RunBalance.SummonInitialCost);
        config.summonCostIncrease = One(RunBalance.SummonCostIncrease);
        upgrade.InitialCost = One(RunBalance.UpgradeInitialCost);
        upgrade.CostMultiplier = One(RunBalance.UpgradeCostMultiplier);
        upgrade.AttackPercentPerLevel = One(RunBalance.UpgradeAttackPercentPerLevel);
        upgrade.AttackSpeedPercentPerLevel = One(RunBalance.UpgradeAttackSpeedPercentPerLevel);
        upgrade.ZeltanFoodPerDronePerLevel = One(RunBalance.UpgradeZeltanFoodPerDronePerLevel);
        upgrade.ZeltanCosts = (float[])workbook[RunBalance.UpgradeZeltanCosts].Clone();
        pool.RareWeight = One(RunBalance.CardsRareWeight);
        pool.EpicWeight = One(RunBalance.CardsEpicWeight);
        pool.LegendWeight = One(RunBalance.CardsLegendWeight);
        var serializedExp = new SerializedObject(exp);
        var required = serializedExp.FindProperty("expRequired");
        var curve = workbook[RunBalance.ExpRequired];
        required.arraySize = curve.Length;
        for (int i = 0; i < curve.Length; i++) required.GetArrayElementAtIndex(i).floatValue = curve[i];
        serializedExp.ApplyModifiedProperties();
        foreach (var asset in new UnityEngine.Object[] { config, upgrade, exp, pool }) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }

        string note = Mathf.RoundToInt(One(RunBalance.ExpMaxLevel)) != ExpManager.MaxLevel
            ? "\n주의: Exp.MaxLevel은 코드 상수(ExpManager.MaxLevel = " + ExpManager.MaxLevel + ")라 반영되지 않았다. 차트 값은 실행 중에 적용된다." : "";
        return "RunBalance.xlsx 값을 앱 내장 값에 반영함: 키 " + workbook.Count + "개" + note;
    }

    /// <summary>Bundled ScriptableObject value of every key, in chart text form.</summary>
    public static Dictionary<string, string> Bundled()
    {
        var map = new Dictionary<string, string>();
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
        var upgrade = Single<UpgradeSettings>();
        var exp = Single<ExpLevelData>();
        var pool = Single<LevelUpPoolData>();
        if (config != null)
        {
            map[RunBalance.CombatBaseCritChance] = N(config.baseCritChance);
            map[RunBalance.CombatDamageVariance] = N(config.damageVariance);
            map[RunBalance.CombatCritDamageVariance] = N(config.critDamageVariance);
            map[RunBalance.CombatMaxAttackSpeedMultiplier] = N(config.maxAttackSpeedMultiplier);
            map[RunBalance.CombatBaseCritDamageMultiplier] = N(config.baseCritDamageMultiplier);
            map[RunBalance.EconomyStartingFood] = N(config.startingFood);
            map[RunBalance.SummonInitialCost] = N(config.summonInitialCost);
            map[RunBalance.SummonCostIncrease] = N(config.summonCostIncrease);
        }
        if (upgrade != null)
        {
            map[RunBalance.UpgradeInitialCost] = N(upgrade.InitialCost);
            map[RunBalance.UpgradeCostMultiplier] = N(upgrade.CostMultiplier);
            map[RunBalance.UpgradeAttackPercentPerLevel] = N(upgrade.AttackPercentPerLevel);
            map[RunBalance.UpgradeAttackSpeedPercentPerLevel] = N(upgrade.AttackSpeedPercentPerLevel);
            map[RunBalance.UpgradeZeltanFoodPerDronePerLevel] = N(upgrade.ZeltanFoodPerDronePerLevel);
            map[RunBalance.UpgradeZeltanCosts] = Text(upgrade.ZeltanCosts);
        }
        if (exp != null)
        {
            var curve = new float[exp.LevelCount];
            for (int level = 1; level <= curve.Length; level++) curve[level - 1] = exp.GetExpRequired(level);
            map[RunBalance.ExpRequired] = Text(curve);
            map[RunBalance.ExpMaxLevel] = N(ExpManager.MaxLevel);
        }
        if (pool != null)
        {
            map[RunBalance.CardsRareWeight] = N(pool.RareWeight);
            map[RunBalance.CardsEpicWeight] = N(pool.EpicWeight);
            map[RunBalance.CardsLegendWeight] = N(pool.LegendWeight);
        }
        return map;
    }

    private static T Single<T>() where T : ScriptableObject
    {
        var paths = AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.Contains("/Lab/") && !path.Contains("Tutorial")).ToArray();
        return paths.Length == 1 ? AssetDatabase.LoadAssetAtPath<T>(paths[0]) : null;
    }

    private static string Text(float[] values) => string.Join("|", values.Select(N));
    private static string N(float value) => ((double)(decimal)value).ToString("0.######", CultureInfo.InvariantCulture);
}
