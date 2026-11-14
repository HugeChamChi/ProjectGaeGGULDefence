using System;
using System.Collections.Generic;
using System.Globalization;

namespace BackendFunction
{
    // Calculation kernel for a Backend Function. Construct only with a server-owned chart row.
    // The Function handler owns run validation, receipts, balances, purchases and persistence.
    public sealed class OutgameEconomy
    {
        private readonly Dictionary<string, double> values = new Dictionary<string, double>();
        private double V(string key) { return values[key]; }
        public OutgameEconomy(IDictionary<string, string> chart)
        {
            if (!chart.ContainsKey("RuleKey") || chart["RuleKey"] != "HpLinkedV1") throw new ArgumentException("Unknown formula version");
            Check(chart, "BaseGold", 0, 1000000, true); Check(chart, "BaseToken", 0, 1000000, true); Check(chart, "HpExponent", 0, 1);
            Check(chart, "UnitCoefficient", 0.000001, 1000000); Check(chart, "UnitExponent", 0, 10); Check(chart, "UnitSoftCap", 1, 19, true);
            Check(chart, "UnitTailSlope", 0, 10); Check(chart, "UnitRound", 1, 1000000, true);
            Check(chart, "ResearchBase", 1, 1000000); Check(chart, "ResearchRowRatio", 1, 10); Check(chart, "ResearchStepRatio", 0, 10);
            Check(chart, "ResearchRound", 1, 1000000, true); Check(chart, "ReferenceGain", 0.000001, 1);
            Check(chart, "WeightMin", 0.000001, 100); Check(chart, "WeightMax", 0.000001, 100); Check(chart, "UtilityWeight", 0.000001, 100);
            Check(chart, "CritChance", 0, 1); Check(chart, "CritMultiplier", 1, 100); Check(chart, "SkillShare", 0, 1);
            if (chart.Count != values.Count + 1 || V("WeightMin") > V("WeightMax")) throw new ArgumentException("Invalid/mixed chart");
            for (int level = 2; level <= 20; level++) if (UnitUpgradeCost(level) <= 0) throw new ArgumentException("Nonpositive unit cost");
        }
        private void Check(IDictionary<string, string> row, string key, double min, double max, bool integer = false)
        {
            string text; double v;
            if (!row.TryGetValue(key, out text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                || !Finite(v) || v < min || v > max || integer && Math.Floor(v) != v) throw new ArgumentException("Invalid chart key: " + key);
            values.Add(key, v);
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static double Clamp(double x, double min, double max) { return Math.Max(min, Math.Min(max, x)); }
        private static double Round(double x, double unit) { return Math.Floor(x / unit + 0.5) * unit; }
        private static int Amount(double x)
        {
            if (!Finite(x) || x < 0 || x > int.MaxValue || x != Math.Floor(x)) throw new ArgumentException("Currency overflow");
            return (int)x;
        }
        public int UnitUpgradeCost(int targetLevel)
        {
            if (targetLevel < 2 || targetLevel > 20) throw new ArgumentOutOfRangeException("targetLevel");
            int k = targetLevel - 1;
            double cost = V("UnitCoefficient") * Math.Pow(Math.Min(k, V("UnitSoftCap")), V("UnitExponent"));
            if (k > V("UnitSoftCap")) cost *= 1 + V("UnitTailSlope") * (k - V("UnitSoftCap"));
            return Amount(Round(cost, V("UnitRound")));
        }
        public double ResearchWeight(string stat, double valuePerLevel)
        {
            if (!Finite(valuePerLevel) || valuePerLevel < 0) throw new ArgumentException("Invalid research value");
            double gain, crit = 1 + V("CritChance") * (V("CritMultiplier") - 1);
            switch (stat)
            {
                case "FoodProduction": case "StartFood": case "TotemEffect": return V("UtilityWeight");
                case "AttackPercent": case "BossDamage": gain = valuePerLevel; break;
                case "CritChance": gain = (Math.Min(1, V("CritChance") + valuePerLevel) - V("CritChance")) * (V("CritMultiplier") - 1) / crit; break;
                case "CritDamage": gain = V("CritChance") * valuePerLevel / crit; break;
                case "AttackSpeedPercent": gain = (1 - V("SkillShare")) * valuePerLevel; break;
                case "SkillDamage": gain = V("SkillShare") * valuePerLevel; break;
                case "SkillCooldownReduction":
                    if (valuePerLevel >= 1) throw new ArgumentException("Invalid cooldown reduction");
                    gain = V("SkillShare") * (1 / (1 - valuePerLevel) - 1); break;
                default: throw new ArgumentException("Unsupported BASIC stat: " + stat);
            }
            return Clamp(gain / V("ReferenceGain"), V("WeightMin"), V("WeightMax"));
        }
        public int ResearchUpgradeCost(string treeKey, int row, string stat, double valuePerLevel, int currentLevel, int maxLevel)
        {
            if (treeKey != "BASIC" || row < 0 || row > 200 || maxLevel < 1 || maxLevel > 1000 || currentLevel < 0 || currentLevel >= maxLevel)
                throw new ArgumentException("Invalid BASIC purchase");
            double raw = V("ResearchBase") * Math.Pow(V("ResearchRowRatio"), row) * ResearchWeight(stat, valuePerLevel);
            double first = Math.Max(V("ResearchRound"), Round(raw, V("ResearchRound")));
            double step = Math.Max(V("ResearchRound"), Round(raw * V("ResearchStepRatio"), V("ResearchRound")));
            Amount(first + step * (maxLevel - 1));
            return Amount(first + step * currentLevel);
        }
        public sealed class BossProgress
        {
            public double SpawnMaxHp;
            public double LowestHp;
        }
        public sealed class Rewards
        {
            public int Gold;
            public int UpgradeTokenA;
        }
        public Rewards BattleReward(double referenceHp, IEnumerable<BossProgress> bosses, double goldBonus, double tokenBonus)
        {
            if (!Finite(referenceHp) || referenceHp <= 0 || !Finite(goldBonus) || goldBonus < 0 || !Finite(tokenBonus) || tokenBonus < 0)
                throw new ArgumentException("Invalid battle reference/bonus");
            double total = 0;
            foreach (var boss in bosses)
            {
                if (!Finite(boss.SpawnMaxHp) || boss.SpawnMaxHp <= 0 || !Finite(boss.LowestHp)) throw new ArgumentException("Invalid boss HP");
                total += Math.Pow(boss.SpawnMaxHp / referenceHp, V("HpExponent")) * Clamp((boss.SpawnMaxHp - boss.LowestHp) / boss.SpawnMaxHp, 0, 1);
            }
            return new Rewards { Gold = Amount(Math.Floor(V("BaseGold") * total * (1 + goldBonus))),
                UpgradeTokenA = Amount(Math.Floor(V("BaseToken") * total * (1 + tokenBonus))) };
        }
    }
}
