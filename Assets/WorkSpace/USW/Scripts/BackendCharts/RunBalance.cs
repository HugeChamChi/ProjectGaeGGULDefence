using System.Collections.Generic;
using LitJson;
using UnityEngine;

namespace GGD.BackendCharts
{
    /// <summary>
    /// In-run balance values from the BACKND chart DEV_/LIVE_RunBalance (columns Key, Value; TODO G12 step 2).
    /// Excel source: Data/Balance/RunBalance.xlsx. Behaviour (tutorial, Editor, blocking) is described on <see cref="BalanceChart"/>.
    /// </summary>
    public static class RunBalance
    {
        public const string CombatBaseCritChance = "Combat.BaseCritChance";
        public const string CombatDamageVariance = "Combat.DamageVariance";
        public const string CombatCritDamageVariance = "Combat.CritDamageVariance";
        public const string CombatMaxAttackSpeedMultiplier = "Combat.MaxAttackSpeedMultiplier";
        public const string CombatBaseCritDamageMultiplier = "Combat.BaseCritDamageMultiplier";
        public const string EconomyStartingFood = "Economy.StartingFood";
        public const string SummonInitialCost = "Summon.InitialCost";
        public const string SummonCostIncrease = "Summon.CostIncrease";
        public const string UpgradeInitialCost = "Upgrade.InitialCost";
        public const string UpgradeCostMultiplier = "Upgrade.CostMultiplier";
        public const string UpgradeAttackPercentPerLevel = "Upgrade.AttackPercentPerLevel";
        public const string UpgradeAttackSpeedPercentPerLevel = "Upgrade.AttackSpeedPercentPerLevel";
        public const string UpgradeZeltanFoodPerDronePerLevel = "Upgrade.ZeltanFoodPerDronePerLevel";
        public const string UpgradeZeltanCosts = "Upgrade.ZeltanCosts";
        public const string ExpRequired = "Exp.Required";
        public const string ExpMaxLevel = "Exp.MaxLevel";
        public const string CardsRareWeight = "Cards.RareWeight";
        public const string CardsEpicWeight = "Cards.EpicWeight";
        public const string CardsLegendWeight = "Cards.LegendWeight";
        public const string TotemCapacity = "Totem.Capacity";
        public const string TotemFullInventoryFood = "Totem.FullInventoryFood";
        public const string ConversionHardFastSeconds = "Conversion.HardFastSeconds";
        public const string ConversionHardFastStones = "Conversion.HardFastStones";
        public const string ConversionHardSeconds = "Conversion.HardSeconds";
        public const string ConversionHardStones = "Conversion.HardStones";
        public const string ConversionBreatherFastSeconds = "Conversion.BreatherFastSeconds";
        public const string ConversionBreatherFastStones = "Conversion.BreatherFastStones";
        public const string ConversionCostPerTier = "Conversion.CostPerTier";

        public static readonly BalanceChart Chart = new BalanceChart("RunBalance", "RunBalanceChartSettings", new Dictionary<string, BalanceRule>
        {
            { CombatBaseCritChance, new BalanceRule(0f, 1f) },
            { CombatDamageVariance, new BalanceRule(0f, 0.5f) },
            { CombatCritDamageVariance, new BalanceRule(0f, 0.5f) },
            { CombatMaxAttackSpeedMultiplier, new BalanceRule(1f, 100f) },
            { CombatBaseCritDamageMultiplier, new BalanceRule(1f, 100f) },
            { EconomyStartingFood, new BalanceRule(0f, 1000000f) },
            { SummonInitialCost, new BalanceRule(0f, 1000000f) },
            { SummonCostIncrease, new BalanceRule(0f, 1000000f) },
            { UpgradeInitialCost, new BalanceRule(1f, 1000000f) },
            { UpgradeCostMultiplier, new BalanceRule(1f, 100f) },
            { UpgradeAttackPercentPerLevel, new BalanceRule(0f, 1000f) },
            { UpgradeAttackSpeedPercentPerLevel, new BalanceRule(0f, 1000f) },
            { UpgradeZeltanFoodPerDronePerLevel, new BalanceRule(0f, 1000f) },
            { UpgradeZeltanCosts, new BalanceRule(0f, 100000000f, BalanceShape.List) },
            { ExpRequired, new BalanceRule(1f, 100000000f, BalanceShape.List) },
            { ExpMaxLevel, new BalanceRule(1f, 1000f, wholeNumber: true) },
            { CardsRareWeight, new BalanceRule(0f, 1000000f) },
            { CardsEpicWeight, new BalanceRule(0f, 1000000f) },
            { CardsLegendWeight, new BalanceRule(0f, 1000000f) },
            // The totem inventory screen has five slots, so the chart cannot exceed five.
            { TotemCapacity, new BalanceRule(1f, 5f, wholeNumber: true) },
            { TotemFullInventoryFood, new BalanceRule(0f, 1000000f) },
            { ConversionHardFastSeconds, new BalanceRule(0f, 3600f) },
            { ConversionHardFastStones, new BalanceRule(0f, 100f, wholeNumber: true) },
            { ConversionHardSeconds, new BalanceRule(0f, 3600f) },
            { ConversionHardStones, new BalanceRule(0f, 100f, wholeNumber: true) },
            { ConversionBreatherFastSeconds, new BalanceRule(0f, 3600f) },
            { ConversionBreatherFastStones, new BalanceRule(0f, 100f, wholeNumber: true) },
            { ConversionCostPerTier, new BalanceRule(0f, 100f, wholeNumber: true) },
        });

        public static string ChartName => Chart.ChartName;
        public static IReadOnlyDictionary<string, BalanceRule> Rules => Chart.Rules;
        public static bool IsLoaded => Chart.IsLoaded;
        /// <inheritdoc cref="BalanceChart.Number"/>
        public static float Number(string key, float bundled) => Chart.Number(key, bundled);
        /// <inheritdoc cref="BalanceChart.List"/>
        public static float[] List(string key, float[] bundled) => Chart.List(key, bundled);
        /// <inheritdoc cref="BalanceChart.Parse"/>
        public static Dictionary<string, float[]> Parse(JsonData rows) => Chart.Parse(rows);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Chart.Clear();
    }
}
