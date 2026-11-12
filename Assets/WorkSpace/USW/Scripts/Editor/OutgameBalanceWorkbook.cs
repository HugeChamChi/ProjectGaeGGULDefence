using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GaeGGUL.DeckUpgrade;
using GGD.BackendCharts;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only bridge between the authoring workbook <c>Data/Balance/OutgameBalance.xlsx</c> and the bundled out-of-run values
/// (user decision 2026-11-11: Excel is the source; bundled values and the BACKND chart are both made from it).
/// The same workbook is uploaded to DEV_OutgameBalance, then LIVE_OutgameBalance. Never opened by a player build.
/// </summary>
public static class OutgameBalanceWorkbook
{
    public const string SourcePath = "Data/Balance/OutgameBalance.xlsx";
    public const string SheetName = "OutgameBalance";
    private const string StaminaPath = "Assets/WorkSpace/HSD/Data/StaminaConfig.asset";
    private const string EconomyPath = "Assets/WorkSpace/USW/Resources/PlayerEconomyConfig.asset";
    private const string AccountPath = "Assets/WorkSpace/USW/Resources/AccountProgressionConfig.asset";

    [MenuItem("Tools/Balance/OutgameBalance.xlsx 검사 (업로드 전)")]
    public static void CheckMenu() => Debug.Log(Check(SourcePath));

    [MenuItem("Tools/Balance/OutgameBalance.xlsx 값을 앱 내장 값에 반영")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath));

    /// <summary>Reads and validates the workbook with the rules the game applies to the chart.</summary>
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
        return OutgameBalance.Parse(rows);
    }

    public static string Check(string path)
    {
        var workbook = Read(path);
        var bundled = Bundled();
        var lines = new List<string> { "OutgameBalance.xlsx 형식 통과: 키 " + workbook.Count + "개" };
        foreach (var pair in workbook)
        {
            string excel = Text(pair.Value);
            if (!bundled.TryGetValue(pair.Key, out string app)) lines.Add("앱 내장 값 없음: " + pair.Key);
            else if (app != excel) lines.Add("다름 " + pair.Key + ": 엑셀 " + excel + " / 앱 내장 " + app);
        }
        if (lines.Count == 1) lines.Add("앱 내장 값과 전부 같음");
        return string.Join("\n", lines);
    }

    /// <summary>Writes workbook values into the bundled assets after the whole workbook passed validation.</summary>
    public static string Import(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before importing outgame balance.");
        var workbook = Read(path);
        int One(string key) => Mathf.RoundToInt(workbook[key][0]);
        var stamina = Load<StaminaConfig>(StaminaPath);
        var economy = Load<PlayerEconomyConfig>(EconomyPath);
        var account = Load<AccountProgressionConfig>(AccountPath);
        Undo.RecordObjects(new UnityEngine.Object[] { stamina, economy, account }, "Import OutgameBalance.xlsx");
        stamina.recoveryIntervalSeconds = One(OutgameBalance.StaminaRecoveryIntervalSeconds);
        stamina.defaultMaxStamina = One(OutgameBalance.StaminaDefaultMax);
        stamina.refillAmount = One(OutgameBalance.StaminaRefillAmount);
        stamina.diamondCostForRefill = One(OutgameBalance.StaminaRefillDiamondCost);
        stamina.stageEntryCost = One(OutgameBalance.StaminaStageEntryCost);
        economy.InitialGold = One(OutgameBalance.EconomyInitialGold);
        economy.InitialDiamond = One(OutgameBalance.EconomyInitialDiamond);
        account.RequiredExperience = workbook[OutgameBalance.AccountRequiredExperience].Select(value => Mathf.RoundToInt(value)).ToArray();
        account.WinBossExperience = One(OutgameBalance.AccountWinBossExperience);
        account.LoseBossExperience = One(OutgameBalance.AccountLoseBossExperience);
        foreach (var asset in new UnityEngine.Object[] { stamina, economy, account }) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }

        var notes = new List<string>();
        if (One(OutgameBalance.DeckUpgradeMaxLevel) != DeckUpgradeProgress.MaxLevel)
            notes.Add("DeckUpgrade.MaxLevel은 코드 상수(" + DeckUpgradeProgress.MaxLevel + ")라 반영되지 않았다");
        if (!Mathf.Approximately(workbook[OutgameBalance.DeckUpgradeMultiplierPerLevel][0], DeckUpgradeProgress.MultiplierPerLevel))
            notes.Add("DeckUpgrade.MultiplierPerLevel은 코드 상수(" + N(DeckUpgradeProgress.MultiplierPerLevel) + ")라 반영되지 않았다");
        return "OutgameBalance.xlsx 값을 앱 내장 값에 반영함: 키 " + workbook.Count + "개"
            + (notes.Count > 0 ? "\n주의: " + string.Join(", ", notes) + ". 차트 값은 실행 중에 적용된다. 계정 EXP는 AccountProgression.xlsx 가져오기를 다시 하면 그 값으로 덮인다." : "");
    }

    /// <summary>Bundled value of every key, in chart text form.</summary>
    public static Dictionary<string, string> Bundled()
    {
        var stamina = Load<StaminaConfig>(StaminaPath);
        var economy = Load<PlayerEconomyConfig>(EconomyPath);
        var account = Load<AccountProgressionConfig>(AccountPath);
        return new Dictionary<string, string>
        {
            { OutgameBalance.StaminaRecoveryIntervalSeconds, N(stamina.recoveryIntervalSeconds) },
            { OutgameBalance.StaminaDefaultMax, N(stamina.defaultMaxStamina) },
            { OutgameBalance.StaminaRefillAmount, N(stamina.refillAmount) },
            { OutgameBalance.StaminaRefillDiamondCost, N(stamina.diamondCostForRefill) },
            { OutgameBalance.StaminaStageEntryCost, N(stamina.stageEntryCost) },
            { OutgameBalance.DeckUpgradeMultiplierPerLevel, N(DeckUpgradeProgress.MultiplierPerLevel) },
            { OutgameBalance.DeckUpgradeMaxLevel, N(DeckUpgradeProgress.MaxLevel) },
            { OutgameBalance.EconomyInitialGold, N(economy.InitialGold) },
            { OutgameBalance.EconomyInitialDiamond, N(economy.InitialDiamond) },
            { OutgameBalance.AccountRequiredExperience, string.Join("|", account.RequiredExperience.Select(value => N(value))) },
            { OutgameBalance.AccountWinBossExperience, N(account.WinBossExperience) },
            { OutgameBalance.AccountLoseBossExperience, N(account.LoseBossExperience) },
        };
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

    private static string Text(float[] values) => string.Join("|", values.Select(N));
    public static string N(float value) => ((double)(decimal)value).ToString("0.######", CultureInfo.InvariantCulture);
}
