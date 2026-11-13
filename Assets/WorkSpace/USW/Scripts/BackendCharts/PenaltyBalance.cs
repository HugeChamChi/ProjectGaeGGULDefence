using System.Collections.Generic;
using LitJson;
using UnityEngine;

namespace GGD.BackendCharts
{
    /// <summary>
    /// Endless-mode run penalty numbers from the BACKND chart DEV_/LIVE_PenaltyBalance (columns Key, Value; TODO G12).
    /// Excel source: Data/Balance/PenaltyBalance.xlsx. Keys are <c>&lt;penalty key&gt;.Enabled</c> (1 = can be offered),
    /// <c>&lt;penalty key&gt;.Weight</c> (draw weight against the other enabled penalties) and, for percent penalties,
    /// <c>&lt;penalty key&gt;.Delta</c> (signed percent per stack, -10 = 10% less).
    /// Rule penalties (one choice fewer, one sealed cell) have no Delta: the game fixes that amount.
    /// Penalty kinds, names and the count of offered candidates (2) stay in the assets and code.
    /// The key list (PenaltyBalanceKeys.cs) is generated from the configured penalties of the pool assets
    /// (menu Tools/Balance/PenaltyBalance 키 목록 다시 만들기); a penalty without keys keeps its bundled values.
    /// </summary>
    public static class PenaltyBalance
    {
        public const string Enabled = "Enabled";
        public const string Weight = "Weight";
        public const string Delta = "Delta";
        /// <summary>The game offers two different penalties every time, so two that can repeat must stay enabled (same rule as EndlessConfigurationValidator.TryValidatePool).</summary>
        public const int MinimumRepeatable = 2;

        private static readonly HashSet<string> Penalties = new HashSet<string>(PenaltyBalanceKeys.Penalties);
        private static readonly Dictionary<string, BalanceRule> RuleTable = BuildRules();

        public static readonly BalanceChart Chart = new BalanceChart("PenaltyBalance", "PenaltyBalanceChartSettings", RuleTable, extraCheck: CheckAcrossKeys);

        private static Dictionary<string, BalanceRule> BuildRules()
        {
            var rules = new Dictionary<string, BalanceRule>();
            foreach (string penalty in PenaltyBalanceKeys.Penalties)
            {
                rules.Add(penalty + "." + Enabled, new BalanceRule(0f, 1f, wholeNumber: true));
                rules.Add(penalty + "." + Weight, new BalanceRule(0f, 1000f));
            }
            // More than -100 keeps the multiplier above zero; a penalty never helps the player, so it is not positive.
            foreach (string penalty in PenaltyBalanceKeys.PercentPenalties) rules.Add(penalty + "." + Delta, new BalanceRule(-99f, 0f));
            return rules;
        }

        private static string CheckAcrossKeys(Dictionary<string, float[]> values)
        {
            int repeatable = 0;
            foreach (string penalty in PenaltyBalanceKeys.Penalties)
            {
                if (values[penalty + "." + Enabled][0] == 0f) continue;
                if (System.Array.IndexOf(PenaltyBalanceKeys.RepeatablePenalties, penalty) >= 0) repeatable++;
                if (values[penalty + "." + Weight][0] <= 0f) return penalty + "." + Weight + " must be above 0 while the penalty is enabled";
            }
            return repeatable < MinimumRepeatable ? "at least " + MinimumRepeatable + " repeatable penalties must be enabled (" + string.Join(", ", PenaltyBalanceKeys.RepeatablePenalties) + ")" : null;
        }

        /// <summary>True when the chart has keys for the penalty with this key.</summary>
        public static bool Has(string penalty) => penalty != null && Penalties.Contains(penalty);

        /// <summary>Whether the penalty can be offered: the chart value, or <paramref name="bundled"/> outside the chart, in the tutorial and in direct Editor play.</summary>
        public static bool IsEnabled(string penalty, bool bundled) => Has(penalty) ? Chart.Number(penalty + "." + Enabled, bundled ? 1f : 0f) != 0f : bundled;

        /// <summary>Draw weight of the penalty: the chart value, or <paramref name="bundled"/> as for <see cref="IsEnabled"/>.</summary>
        public static double WeightOf(string penalty, double bundled)
        {
            if (!Has(penalty)) return bundled;
            float asFloat = (float)bundled;
            float value = Chart.Number(penalty + "." + Weight, asFloat);
            return value == asFloat ? bundled : value;
        }

        /// <summary>Signed amount per stack: the chart value for percent penalties, otherwise <paramref name="bundled"/>.</summary>
        public static double DeltaOf(string penalty, double bundled)
        {
            if (!Has(penalty) || !RuleTable.ContainsKey(penalty + "." + Delta)) return bundled;
            float asFloat = (float)bundled;
            float value = Chart.Number(penalty + "." + Delta, asFloat);
            return value == asFloat ? bundled : value;
        }

        public static string ChartName => Chart.ChartName;
        public static IReadOnlyDictionary<string, BalanceRule> Rules => Chart.Rules;
        public static bool IsLoaded => Chart.IsLoaded;
        /// <inheritdoc cref="BalanceChart.Parse"/>
        public static Dictionary<string, float[]> Parse(JsonData rows) => Chart.Parse(rows);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Chart.Clear();
    }
}
