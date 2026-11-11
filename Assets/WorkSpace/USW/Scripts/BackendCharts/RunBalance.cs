using System;
using System.Collections.Generic;
using System.Globalization;
using LitJson;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GGD.BackendCharts
{
    /// <summary>
    /// In-run balance values from the BACKND chart DEV_/LIVE_RunBalance (key, value rows; TODO G12 step 2).
    /// Gameplay passes the bundled ScriptableObject value and receives the chart value when one is installed.
    /// The tutorial and direct Editor play keep bundled values (user decision 2026-11-11); a Player build
    /// that reaches gameplay without the chart throws, because login blocks until the chart is fetched.
    /// </summary>
    public static class RunBalance
    {
        public const string ChartName = ChartEnvironment.Prefix + "RunBalance";

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

        /// <summary>Allowed range of one key. Lists are checked element by element.</summary>
        public readonly struct Rule
        {
            public readonly float Min, Max;
            public readonly bool IsList, WholeNumber;
            public Rule(float min, float max, bool isList = false, bool wholeNumber = false) { Min = min; Max = max; IsList = isList; WholeNumber = wholeNumber; }
        }

        /// <summary>Every key the chart must contain. A missing, unknown or out-of-range key rejects the whole chart.</summary>
        public static readonly IReadOnlyDictionary<string, Rule> Rules = new Dictionary<string, Rule>
        {
            { CombatBaseCritChance, new Rule(0f, 1f) },
            { CombatDamageVariance, new Rule(0f, 0.5f) },
            { CombatCritDamageVariance, new Rule(0f, 0.5f) },
            { CombatMaxAttackSpeedMultiplier, new Rule(1f, 100f) },
            { CombatBaseCritDamageMultiplier, new Rule(1f, 100f) },
            { EconomyStartingFood, new Rule(0f, 1000000f) },
            { SummonInitialCost, new Rule(0f, 1000000f) },
            { SummonCostIncrease, new Rule(0f, 1000000f) },
            { UpgradeInitialCost, new Rule(1f, 1000000f) },
            { UpgradeCostMultiplier, new Rule(1f, 100f) },
            { UpgradeAttackPercentPerLevel, new Rule(0f, 1000f) },
            { UpgradeAttackSpeedPercentPerLevel, new Rule(0f, 1000f) },
            { UpgradeZeltanFoodPerDronePerLevel, new Rule(0f, 1000f) },
            { UpgradeZeltanCosts, new Rule(0f, 100000000f, isList: true) },
            { ExpRequired, new Rule(1f, 100000000f, isList: true) },
            { ExpMaxLevel, new Rule(1f, 1000f, wholeNumber: true) },
            { CardsRareWeight, new Rule(0f, 1000000f) },
            { CardsEpicWeight, new Rule(0f, 1000000f) },
            { CardsLegendWeight, new Rule(0f, 1000000f) },
        };

        [Serializable] private sealed class Settings { public bool UseRemote; }
        private const string TutorialSceneName = "TutorialScene";
        private const char ListSeparator = '|';
        private static Dictionary<string, float[]> values;
        private static bool? remoteRequired;
        private static bool warnedBundled;

        public static bool IsLoaded => values != null;

        internal static bool RequiresRemote
        {
            get
            {
                if (!remoteRequired.HasValue)
                {
                    var settings = Resources.Load<TextAsset>("RunBalanceChartSettings");
                    remoteRequired = settings != null && JsonUtility.FromJson<Settings>(settings.text).UseRemote;
                }
                return remoteRequired.Value;
            }
        }

        /// <summary>Chart value of a single-number key, or <paramref name="bundled"/> in the tutorial and direct Editor play.</summary>
        public static float Number(string key, float bundled)
        {
            var list = Lookup(key);
            return list != null ? list[0] : bundled;
        }

        /// <summary>Chart value of a list key, or <paramref name="bundled"/> in the tutorial and direct Editor play.</summary>
        public static float[] List(string key, float[] bundled) => Lookup(key) ?? bundled;

        private static float[] Lookup(string key)
        {
            if (!Rules.ContainsKey(key)) throw new ArgumentException("Unknown run balance key: " + key, nameof(key));
            if (SceneManager.GetActiveScene().name == TutorialSceneName) return null;
            if (values != null) return values[key];
            if (RequiresRemote)
            {
                if (!Application.isEditor) throw new InvalidOperationException("Run balance chart must load before gameplay reads " + key);
                if (!warnedBundled)
                {
                    warnedBundled = true;
                    Debug.LogWarning("[RunBalance] No chart loaded (Editor play without login): using bundled values.");
                }
            }
            return null;
        }

        /// <summary>Validates chart rows (columns Key, Value). Throws <see cref="FormatException"/> naming the first problem.</summary>
        public static Dictionary<string, float[]> Parse(JsonData rows)
        {
            if (rows == null || !rows.IsArray || rows.Count == 0) throw new FormatException("RunBalance: no rows");
            var parsed = new Dictionary<string, float[]>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.IsObject || !row.ContainsKey("Key") || !row.ContainsKey("Value")) throw new FormatException("RunBalance row " + (i + 1) + ": Key or Value column missing");
                string key = row["Key"]?.ToString().Trim(), text = row["Value"]?.ToString().Trim();
                if (string.IsNullOrEmpty(key)) throw new FormatException("RunBalance row " + (i + 1) + ": empty Key");
                if (!Rules.TryGetValue(key, out var rule)) throw new FormatException("RunBalance: unknown key " + key);
                if (parsed.ContainsKey(key)) throw new FormatException("RunBalance: duplicate key " + key);
                if (string.IsNullOrEmpty(text)) throw new FormatException("RunBalance: empty value for " + key);
                var parts = rule.IsList ? text.Split(ListSeparator) : new[] { text };
                var numbers = new float[parts.Length];
                for (int n = 0; n < parts.Length; n++)
                {
                    if (!float.TryParse(parts[n].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                        || float.IsNaN(number) || float.IsInfinity(number))
                        throw new FormatException("RunBalance: " + key + " is not a number: " + parts[n]);
                    if (number < rule.Min || number > rule.Max)
                        throw new FormatException("RunBalance: " + key + " out of range " + rule.Min + "~" + rule.Max + ": " + parts[n]);
                    if (rule.WholeNumber && number != Mathf.Floor(number))
                        throw new FormatException("RunBalance: " + key + " must be a whole number: " + parts[n]);
                    numbers[n] = number;
                }
                parsed.Add(key, numbers);
            }
            foreach (var key in Rules.Keys)
                if (!parsed.ContainsKey(key)) throw new FormatException("RunBalance: missing key " + key);
            return parsed;
        }

        internal static void Install(Dictionary<string, float[]> next) => values = next;

        internal static void Clear()
        {
            values = null;
            remoteRequired = null;
            warnedBundled = false;
        }
    }
}
