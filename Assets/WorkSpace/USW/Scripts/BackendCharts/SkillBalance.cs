using System.Collections.Generic;
using LitJson;
using UnityEngine;

namespace GGD.BackendCharts
{
    /// <summary>
    /// Skill numbers from the BACKND chart DEV_/LIVE_SkillBalance (columns Key, Value, Normal, Rare, Epic, Legend; TODO G12 step 2).
    /// Excel source: Data/Balance/SkillBalance.xlsx. Debuff definitions are a separate chart; unit base stats stay in UnitBaseStats.
    /// Behaviour (tutorial, Editor, blocking) is described on <see cref="BalanceChart"/>.
    /// </summary>
    public static class SkillBalance
    {
        public const string HackingCapacity = "Hacking.Capacity";
        public const string HackingAttacksToCharge = "Hacking.AttacksToCharge";
        public const string HackingStacksProduced = "Hacking.StacksProduced";
        public const string HackingMaxStacksConsumed = "Hacking.MaxStacksConsumed";
        public const string HackingBaseCoefficient = "Hacking.BaseCoefficient";
        public const string HackingCoefficientPerStack = "Hacking.CoefficientPerStack";
        public const string DisigmanAttacksToCharge = "Disigman.AttacksToCharge";
        public const string DisigmanSecondsRecovered = "Disigman.SecondsRecovered";
        public const string DisigmanBasicCoefficient = "Disigman.BasicCoefficient";
        public const string DisigmanSkillCoefficient = "Disigman.SkillCoefficient";
        public const string AlphanCooldownSeconds = "Alphan.CooldownSeconds";
        public const string AlphanRallyAttackCoefficient = "Alphan.RallyAttackCoefficient";

        public static readonly BalanceChart Chart = new BalanceChart("SkillBalance", "SkillBalanceChartSettings", new Dictionary<string, BalanceRule>
        {
            { HackingCapacity, new BalanceRule(1f, 100000f, wholeNumber: true) },
            { HackingAttacksToCharge, new BalanceRule(1f, 100000f, BalanceShape.Tiered, true) },
            { HackingStacksProduced, new BalanceRule(0f, 100000f, BalanceShape.Tiered, true) },
            { HackingMaxStacksConsumed, new BalanceRule(0f, 100000f, BalanceShape.Tiered, true) },
            { HackingBaseCoefficient, new BalanceRule(0f, 100000f, BalanceShape.Tiered) },
            { HackingCoefficientPerStack, new BalanceRule(0f, 100000f, BalanceShape.Tiered) },
            { DisigmanAttacksToCharge, new BalanceRule(1f, 100000f, BalanceShape.Tiered, true) },
            { DisigmanSecondsRecovered, new BalanceRule(0f, 100000f, BalanceShape.Tiered) },
            { DisigmanBasicCoefficient, new BalanceRule(0f, 100000f, BalanceShape.Tiered) },
            { DisigmanSkillCoefficient, new BalanceRule(0f, 100000f, BalanceShape.Tiered) },
            { AlphanCooldownSeconds, new BalanceRule(0.05f, 100000f) },
            { AlphanRallyAttackCoefficient, new BalanceRule(0f, 100000f) },
        });

        public static string ChartName => Chart.ChartName;
        public static IReadOnlyDictionary<string, BalanceRule> Rules => Chart.Rules;
        public static bool IsLoaded => Chart.IsLoaded;
        /// <inheritdoc cref="BalanceChart.Number"/>
        public static float Number(string key, float bundled) => Chart.Number(key, bundled);
        /// <inheritdoc cref="BalanceChart.Tiered"/>
        public static float Tiered(string key, Tier tier, float bundled) => Chart.Tiered(key, tier, bundled);
        /// <inheritdoc cref="BalanceChart.Parse"/>
        public static Dictionary<string, float[]> Parse(JsonData rows) => Chart.Parse(rows);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Chart.Clear();
    }
}
