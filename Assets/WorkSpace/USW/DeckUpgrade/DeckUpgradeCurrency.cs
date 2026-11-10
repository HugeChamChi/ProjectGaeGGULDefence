using System;
using UnityEngine;

namespace GaeGGUL.DeckUpgrade
{
    public static class DeckUpgradeCurrency
    {
        public const string Token = "UpgradeTokenA";
        public static int Balance(PlayerData data, string item) => data == null ? 0 : item switch
        {
            "Gold" => data.Gold,
            "Diamond" => data.Diamond,
            Token => data.RewardInventory.Count(RewardType.Currency, 4),
            _ => throw new ArgumentException("Unsupported upgrade currency: " + item)
        };
        public static void Spend(PlayerData data, string item, int amount)
        {
            if (amount <= 0 || Balance(data, item) < amount) throw new ArgumentException("Insufficient upgrade currency");
            switch (item)
            {
                case "Gold": data.Gold -= amount; break;
                case "Diamond": data.Diamond -= amount; break;
                case Token: data.RewardInventory.Owned.Find(x => x.Type == RewardType.Currency && x.TargetId == 4).Count -= amount; break;
            }
        }
        // Temporary token artwork reuses the existing diamond icon.
        public static Sprite Icon => Resources.Load<RewardCatalog>("RewardCatalog")?.Find(10004).Icon;
    }
}
