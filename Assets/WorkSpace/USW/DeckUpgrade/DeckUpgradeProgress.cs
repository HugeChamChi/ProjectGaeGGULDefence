using System;
using System.Collections.Generic;
using UnityEngine;

namespace GaeGGUL.DeckUpgrade
{
    // Isolated preview economy. Never writes Player data or source UnitData assets.
    [Serializable]
    public sealed class DeckUpgradeProgress
    {
        public const string SaveKey = "DeckUpgradePreview.v1";
        public const int MaxLevel = 20;
        public const string ChiefId = "AlphanChief";
        public int money = 12000;
        public List<Entry> units = new List<Entry>();

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public int level = 1;
        }

        public int Level(string id) => units.Find(x => x.id == id)?.level ?? 1;
        public int Cost(string id) => 150 * Level(id);
        public float Multiplier(string id) => 1f + (Level(id) - 1) * .1f;
        public float ChiefBonus => (Level(ChiefId) - 1) * .02f;
        public float UnitMultiplier(string id) => Multiplier(id) * (1 + ChiefBonus);

        public bool TryUpgrade(string id)
        {
            int cost = Cost(id);
            if (Level(id) >= MaxLevel || money < cost) return false;
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

        public static DeckUpgradeProgress Load() => PlayerPrefs.HasKey(SaveKey)
            ? JsonUtility.FromJson<DeckUpgradeProgress>(PlayerPrefs.GetString(SaveKey))
            : new DeckUpgradeProgress();

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
