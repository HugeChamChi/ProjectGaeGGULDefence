using System;
using System.Collections.Generic;

namespace GaeGGUL.DeckUpgrade
{
    // Isolated preview economy. Never writes Player data or source UnitData assets.
    [Serializable]
    public sealed class DeckUpgradeProgress
    {
        public const string SaveKey = "DeckUpgradePreview.v1";
        public const int MaxLevel = 20;
        public const string ChiefId = "AlphanChief";
        /// <summary>Existing preview-only growth per character level.</summary>
        public const float MultiplierPerLevel = .1f;
        /// <summary>Existing preview-only global bonus per chief level.</summary>
        public const float ChiefBonusPerLevel = .02f;
        // MaxLevel and MultiplierPerLevel above are the bundled values. Gameplay and UI read the two members below,
        // which return the DEV_/LIVE_OutgameBalance chart values once the chart is installed (TODO G12).
        public static int CurrentMaxLevel => GGD.BackendCharts.OutgameBalance.Integer(GGD.BackendCharts.OutgameBalance.DeckUpgradeMaxLevel, MaxLevel);
        public static float CurrentMultiplierPerLevel => GGD.BackendCharts.OutgameBalance.Number(GGD.BackendCharts.OutgameBalance.DeckUpgradeMultiplierPerLevel, MultiplierPerLevel);
        public int money = 12000;
        public List<Entry> units = new List<Entry>();

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public int level = 1;
        }

        public int Level(string id) => units.Find(x => x.id == id)?.level ?? 1;
        public int Cost(string id) => Level(id);
        public float Multiplier(string id) => 1f + (Level(id) - 1) * CurrentMultiplierPerLevel;
        public float ChiefBonus => (Level(ChiefId) - 1) * ChiefBonusPerLevel;
        public float UnitMultiplier(string id) => Multiplier(id) * (1 + ChiefBonus);

        public bool TryUpgrade(string id)
        {
            int cost = Cost(id);
            if (Level(id) >= CurrentMaxLevel || money < cost) return false;
            var entry = units.Find(x => x.id == id);
            if (entry == null)
            {
                entry = new Entry { id = id };
                units.Add(entry);
            }
            money -= cost;
            entry.level++;
            return true;
        }

    }
}
