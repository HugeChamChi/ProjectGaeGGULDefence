using System.Collections.Generic;
using LitJson;
using UnityEngine;

namespace GGD.BackendCharts
{
    /// <summary>
    /// Out-of-run balance values from the BACKND chart DEV_/LIVE_OutgameBalance (columns Key, Value; TODO G12):
    /// stamina, account EXP, account (deck) upgrade growth and new-account currency. Excel source: Data/Balance/OutgameBalance.xlsx.
    /// Loaded first during login, before player data, because new-account creation and stamina recovery read it.
    /// Unlike the in-run charts, a read before login returns the bundled value instead of throwing: title screens and
    /// local notifications run before any account exists. After login the chart value always applies.
    /// </summary>
    public static class OutgameBalance
    {
        public const string StaminaRecoveryIntervalSeconds = "Stamina.RecoveryIntervalSeconds";
        public const string StaminaDefaultMax = "Stamina.DefaultMax";
        public const string StaminaRefillAmount = "Stamina.RefillAmount";
        public const string StaminaRefillDiamondCost = "Stamina.RefillDiamondCost";
        public const string StaminaStageEntryCost = "Stamina.StageEntryCost";
        public const string DeckUpgradeMultiplierPerLevel = "DeckUpgrade.MultiplierPerLevel";
        public const string DeckUpgradeMaxLevel = "DeckUpgrade.MaxLevel";
        public const string EconomyInitialGold = "Economy.InitialGold";
        public const string EconomyInitialDiamond = "Economy.InitialDiamond";
        public const string AccountRequiredExperience = "Account.RequiredExperience";
        public const string AccountWinBossExperience = "Account.WinBossExperience";
        public const string AccountLoseBossExperience = "Account.LoseBossExperience";

        public static readonly BalanceChart Chart = new BalanceChart("OutgameBalance", "OutgameBalanceChartSettings", new Dictionary<string, BalanceRule>
        {
            { StaminaRecoveryIntervalSeconds, new BalanceRule(1f, 86400f, wholeNumber: true) },
            { StaminaDefaultMax, new BalanceRule(1f, 100000f, wholeNumber: true) },
            { StaminaRefillAmount, new BalanceRule(1f, 100000f, wholeNumber: true) },
            { StaminaRefillDiamondCost, new BalanceRule(0f, 1000000f, wholeNumber: true) },
            { StaminaStageEntryCost, new BalanceRule(1f, 100000f, wholeNumber: true) },
            { DeckUpgradeMultiplierPerLevel, new BalanceRule(0f, 100f) },
            { DeckUpgradeMaxLevel, new BalanceRule(1f, 1000f, wholeNumber: true) },
            { EconomyInitialGold, new BalanceRule(0f, 10000000f, wholeNumber: true) },
            { EconomyInitialDiamond, new BalanceRule(0f, 10000000f, wholeNumber: true) },
            { AccountRequiredExperience, new BalanceRule(0f, 10000000f, BalanceShape.List, true) },
            { AccountWinBossExperience, new BalanceRule(0f, 10000000f, wholeNumber: true) },
            { AccountLoseBossExperience, new BalanceRule(0f, 10000000f, wholeNumber: true) },
        }, bundledBeforeLoad: true, extraCheck: CheckAccountCurve);

        // Same shape AccountProgressionConfig.Validate requires: every level needs positive EXP except the last (the cap), which is 0.
        private static string CheckAccountCurve(Dictionary<string, float[]> values)
        {
            var curve = values[AccountRequiredExperience];
            if (curve.Length < 2) return AccountRequiredExperience + " needs at least 2 levels";
            double total = 0;
            for (int i = 0; i < curve.Length - 1; i++)
            {
                if (curve[i] <= 0f) return AccountRequiredExperience + " level " + (i + 1) + " must be positive";
                total += curve[i];
            }
            if (curve[curve.Length - 1] != 0f) return AccountRequiredExperience + " must end with 0 (the level cap)";
            return total > int.MaxValue ? AccountRequiredExperience + " total is too large" : null;
        }

        public static string ChartName => Chart.ChartName;
        public static IReadOnlyDictionary<string, BalanceRule> Rules => Chart.Rules;
        public static bool IsLoaded => Chart.IsLoaded;
        /// <inheritdoc cref="BalanceChart.Number"/>
        public static float Number(string key, float bundled) => Chart.Number(key, bundled);
        /// <summary>Whole-number key as int.</summary>
        public static int Integer(string key, int bundled) => Mathf.RoundToInt(Chart.Number(key, bundled));
        /// <inheritdoc cref="BalanceChart.List"/>
        public static float[] List(string key, float[] bundled) => Chart.List(key, bundled);
        /// <inheritdoc cref="BalanceChart.Parse"/>
        public static Dictionary<string, float[]> Parse(JsonData rows) => Chart.Parse(rows);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Chart.Clear();
    }
}
