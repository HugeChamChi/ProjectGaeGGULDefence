using System.Collections.Generic;
using LitJson;
using UnityEngine;

namespace GGD.BackendCharts
{
    /// <summary>
    /// Totem numbers from the BACKND chart DEV_/LIVE_TotemBalance (columns Key, Value; TODO G12).
    /// Keys are <c>TD&lt;totemId&gt;.&lt;value name&gt;</c>, for example <c>TD1009.AttackBonus</c>. Excel source: Data/Balance/TotemBalance.xlsx.
    /// Only numbers and the on/off switch live here; effect kinds and ranges stay in the TotemData assets.
    /// A totem or value name without a key (tutorial totem, lab and test data) keeps its bundled value.
    /// Behaviour (tutorial, Editor, blocking) is described on <see cref="BalanceChart"/>.
    /// </summary>
    public static class TotemBalance
    {
        /// <summary>1 = offered as a boss reward, 0 = never offered. Totems already held keep working.</summary>
        public const string Enabled = "Enabled";
        public const string Chance = "Chance";
        public const string AttackCoefficient = "AttackCoefficient";
        public const string StageSeconds = "StageSeconds";
        public const string StageAttackBonus = "StageAttackBonus";
        public const string HarvestSeconds = "HarvestSeconds";
        public const string HarvestFood = "HarvestFood";
        public const string FireIntervalSeconds = "FireIntervalSeconds";
        public const string ImpactDamage = "ImpactDamage";
        public const string SpawnIntervalSeconds = "SpawnIntervalSeconds";
        public const string ShadowDelaySeconds = "ShadowDelaySeconds";
        public const string FoodIntervalSeconds = "FoodIntervalSeconds";
        public const string Food = "Food";
        public const string AttackBonus = "AttackBonus";
        public const string SpeedBonus = "SpeedBonus";
        public const string CritChanceBonus = "CritChanceBonus";
        public const string CritDamageBonus = "CritDamageBonus";
        public const string WindowSeconds = "WindowSeconds";

        /// <summary>TD1002 growth stages. The cell ranges of each stage are authored in the asset, so the count is fixed.</summary>
        public const int GrowthStageCount = 4;
        private const int GrowthTotemId = 1002;
        private const float MaxSeconds = 3600f;

        private static readonly Dictionary<int, Dictionary<string, string>> KeysByTotem = new Dictionary<int, Dictionary<string, string>>();
        private static readonly Dictionary<string, BalanceRule> RuleTable = BuildRules();

        public static readonly BalanceChart Chart = new BalanceChart("TotemBalance", "TotemBalanceChartSettings", RuleTable, extraCheck: CheckGrowth);

        private static Dictionary<string, BalanceRule> BuildRules()
        {
            var rules = new Dictionary<string, BalanceRule>();
            void Add(int totemId, string name, BalanceRule rule)
            {
                string key = "TD" + totemId + "." + name;
                rules.Add(key, rule);
                if (!KeysByTotem.TryGetValue(totemId, out var names)) KeysByTotem.Add(totemId, names = new Dictionary<string, string>());
                names.Add(name, key);
            }
            var seconds = new BalanceRule(0.1f, MaxSeconds);
            var bonus = new BalanceRule(0f, 100f);
            for (int id = 1001; id <= 1014; id++) Add(id, Enabled, new BalanceRule(0f, 1f, wholeNumber: true));
            Add(1001, Chance, new BalanceRule(0f, 1f));
            Add(1001, AttackCoefficient, new BalanceRule(0f, 100f));
            Add(1002, StageSeconds, new BalanceRule(0f, MaxSeconds, BalanceShape.List));
            Add(1002, StageAttackBonus, new BalanceRule(0f, 100f, BalanceShape.List));
            Add(1002, HarvestSeconds, seconds);
            Add(1002, HarvestFood, new BalanceRule(0f, 1000000f));
            Add(1003, FireIntervalSeconds, seconds);
            Add(1003, ImpactDamage, new BalanceRule(0f, 100000000f));
            Add(1004, SpawnIntervalSeconds, seconds);
            Add(1007, ShadowDelaySeconds, new BalanceRule(0f, 60f));
            Add(1008, AttackBonus, bonus);
            Add(1008, SpeedBonus, bonus);
            Add(1009, AttackBonus, bonus);
            // Negative = slower. -0.95 keeps the attack speed above zero.
            Add(1009, SpeedBonus, new BalanceRule(-0.95f, 100f));
            Add(1010, FoodIntervalSeconds, seconds);
            Add(1010, Food, new BalanceRule(0f, 1000000f));
            Add(1011, SpeedBonus, bonus);
            Add(1012, CritChanceBonus, new BalanceRule(0f, 1f));
            Add(1012, CritDamageBonus, bonus);
            Add(1013, AttackBonus, bonus);
            Add(1014, WindowSeconds, seconds);
            Add(1014, SpeedBonus, bonus);
            return rules;
        }

        // TD1002: both stage lists have one entry per authored stage, the first stage starts at placement and later stages come later.
        private static string CheckGrowth(Dictionary<string, float[]> values)
        {
            var stageSeconds = values[Key(GrowthTotemId, StageSeconds)];
            var stageBonus = values[Key(GrowthTotemId, StageAttackBonus)];
            if (stageSeconds.Length != GrowthStageCount || stageBonus.Length != GrowthStageCount)
                return "TD1002 stage lists need exactly " + GrowthStageCount + " values";
            if (stageSeconds[0] != 0f) return Key(GrowthTotemId, StageSeconds) + " must start with 0";
            for (int i = 1; i < stageSeconds.Length; i++)
                if (stageSeconds[i] <= stageSeconds[i - 1]) return Key(GrowthTotemId, StageSeconds) + " must increase";
            return null;
        }

        /// <summary>Chart key of a totem value, or null when the chart has no such key.</summary>
        public static string Key(int totemId, string name)
            => KeysByTotem.TryGetValue(totemId, out var names) && names.TryGetValue(name, out string key) ? key : null;

        /// <summary>Value names the chart holds for <paramref name="totemId"/> (empty for totems outside the chart).</summary>
        public static IEnumerable<string> Names(int totemId)
            => KeysByTotem.TryGetValue(totemId, out var names) ? names.Keys : (IEnumerable<string>)System.Array.Empty<string>();

        /// <summary>Chart value of a totem number, or <paramref name="bundled"/> when the chart has no such key, in the tutorial and in direct Editor play.</summary>
        public static float Number(int totemId, string name, float bundled)
        {
            string key = Key(totemId, name);
            return key == null ? bundled : Chart.Number(key, bundled);
        }

        /// <summary>Chart list of a totem, or null when the chart has no such key, in the tutorial and in direct Editor play.</summary>
        public static float[] List(int totemId, string name)
        {
            string key = Key(totemId, name);
            return key == null ? null : Chart.List(key, null);
        }

        /// <summary>False only when the chart switches the totem off. Totems outside the chart are always on.</summary>
        public static bool IsEnabled(int totemId) => Number(totemId, Enabled, 1f) != 0f;

        public static string ChartName => Chart.ChartName;
        public static IReadOnlyDictionary<string, BalanceRule> Rules => Chart.Rules;
        public static bool IsLoaded => Chart.IsLoaded;
        /// <inheritdoc cref="BalanceChart.Parse"/>
        public static Dictionary<string, float[]> Parse(JsonData rows) => Chart.Parse(rows);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Chart.Clear();
    }
}
