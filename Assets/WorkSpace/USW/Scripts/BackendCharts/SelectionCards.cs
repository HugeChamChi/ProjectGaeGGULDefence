using System;
using System.Collections.Generic;
using System.Reflection;
using LitJson;
using UnityEngine;

namespace GGD.BackendCharts
{
    /// <summary>
    /// Level-up card numbers from the BACKND chart DEV_/LIVE_SelectionCards (columns Key, Value; TODO G12, user answer Q4 2026-11-11:
    /// numbers, tier and on/off; no per-card weight). Excel source: Data/Balance/SelectionCards.xlsx.
    /// Keys are <c>&lt;chooseId&gt;.Enabled</c>, <c>&lt;chooseId&gt;.Tier</c> (1 rare, 2 epic, 3 legend) and
    /// <c>&lt;chooseId&gt;.&lt;effect&gt;.&lt;field&gt;</c>, for example <c>9101.StatModifier.Ratio</c>. The effect name is the definition
    /// class without "Definition"; a second effect of the same class in one card is numbered (<c>StatModifier2</c>).
    /// Effect kinds, targets and texts stay in the card assets. Cards without keys (glitch cards, test data) keep their bundled values.
    /// The key list lives in SelectionCardsKeys.cs and is generated from the pool assets (menu Tools/Balance).
    /// The chart is checked for shape at login; the per-effect value rules run when a card is read, so the
    /// Editor check (menu "SelectionCards.xlsx 검사") must pass before an upload.
    /// </summary>
    public static class SelectionCards
    {
        public const string Enabled = "Enabled";
        public const string TierName = "Tier";
        private const float Limit = 1000000f;
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();
        private static readonly HashSet<int> Cards = new HashSet<int>(SelectionCardsKeys.CardIds);
        private static readonly Dictionary<string, BalanceRule> RuleTable = BuildRules();

        public static readonly BalanceChart Chart = new BalanceChart("SelectionCards", "SelectionCardsChartSettings", RuleTable);

        private static Dictionary<string, BalanceRule> BuildRules()
        {
            var rules = new Dictionary<string, BalanceRule>();
            foreach (int id in SelectionCardsKeys.CardIds)
            {
                rules.Add(id + "." + Enabled, new BalanceRule(0f, 1f, wholeNumber: true));
                rules.Add(id + "." + TierName, new BalanceRule((int)global::Tier.Rare, (int)global::Tier.Legend, wholeNumber: true));
            }
            foreach (string key in SelectionCardsKeys.WholeKeys) rules.Add(key, new BalanceRule(-Limit, Limit, wholeNumber: true));
            foreach (string key in SelectionCardsKeys.FloatKeys) rules.Add(key, new BalanceRule(-Limit, Limit));
            return rules;
        }

        /// <summary>True when the chart has keys for this card.</summary>
        public static bool Has(int chooseId) => Cards.Contains(chooseId);

        /// <summary>False only when the chart switches the card off. Cards already taken keep working.</summary>
        public static bool IsEnabled(int chooseId) => !Has(chooseId) || Chart.Number(chooseId + "." + Enabled, 1f) != 0f;

        /// <summary>Chart tier of the card, or <paramref name="bundled"/> for cards outside the chart, in the tutorial and in direct Editor play.</summary>
        public static Tier Tier(int chooseId, Tier bundled)
            => Has(chooseId) ? (Tier)Mathf.RoundToInt(Chart.Number(chooseId + "." + TierName, (int)bundled)) : bundled;

        /// <summary>
        /// Writes chart numbers into a copied card definition. Returns true when a number changed; the caller validates the result again.
        /// </summary>
        public static bool Apply(int chooseId, SelectionCardDefinition definition)
        {
            if (!Has(chooseId) || definition == null) return false;
            bool changed = false;
            foreach (var slot in Slots(definition))
                foreach (var field in NumberFields(slot.Value.GetType()))
                {
                    string key = chooseId + "." + slot.Key + "." + field.Name;
                    if (!RuleTable.ContainsKey(key)) continue;
                    float bundled = Convert.ToSingle(field.GetValue(slot.Value));
                    float value = Chart.Number(key, bundled);
                    if (value == bundled) continue;
                    field.SetValue(slot.Value, field.FieldType == typeof(int) ? (object)Mathf.RoundToInt(value) : value);
                    changed = true;
                }
            return changed;
        }

        /// <summary>Effects then commands of a card with their chart names, in authored order.</summary>
        public static List<KeyValuePair<string, object>> Slots(SelectionCardDefinition definition)
        {
            var result = new List<KeyValuePair<string, object>>();
            var counts = new Dictionary<string, int>();
            void Add(object target)
            {
                if (target == null) return;
                string name = target.GetType().Name;
                if (name.EndsWith("Definition")) name = name.Substring(0, name.Length - "Definition".Length);
                counts.TryGetValue(name, out int count);
                counts[name] = ++count;
                result.Add(new KeyValuePair<string, object>(count == 1 ? name : name + count, target));
            }
            if (definition.Effects != null) foreach (var effect in definition.Effects) Add(effect);
            if (definition.Commands != null) foreach (var command in definition.Commands) Add(command);
            return result;
        }

        /// <summary>Public float and int fields of an effect or command definition.</summary>
        public static FieldInfo[] NumberFields(Type type)
        {
            if (FieldCache.TryGetValue(type, out var fields)) return fields;
            var list = new List<FieldInfo>();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (field.FieldType == typeof(float) || field.FieldType == typeof(int)) list.Add(field);
            return FieldCache[type] = list.ToArray();
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
