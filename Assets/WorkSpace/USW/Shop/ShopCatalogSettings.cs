using System;
using UnityEngine;

namespace GaeGGUL.Shop.UI
{
    [Serializable]
    public sealed class GoldShopProduct
    {
        public int ShopId;
        public int GoldAmount;
        public int DiamondPrice;
        public bool IsConfigured => ShopId > 0 && GoldAmount > 0 && DiamondPrice > 0;
    }

    [CreateAssetMenu(menuName = "Shop/Catalog Settings")]
    public sealed class ShopCatalogSettings : ScriptableObject
    {
        public ItemData Gold;
        public ItemData Diamond;
        [Tooltip("Repeatable gold products purchased with diamonds. Zero values remain unavailable.")]
        public GoldShopProduct[] GoldProducts = Array.Empty<GoldShopProduct>();

        [Header("Lobby shop art (UI3300)")]
        [Tooltip("정수 = character upgrade token UpgradeTokenA.")]
        public string EssenceName = "정수";
        public Sprite EssenceIcon;
        [Tooltip("Product art by tier within a section: small, medium, large.")]
        public Sprite[] GoldPiles = new Sprite[3];
        public Sprite[] DiamondPiles = new Sprite[3];
        public Sprite[] EssencePiles = new Sprite[3];
        [Tooltip("Small cost icons drawn next to prices.")]
        public Sprite GoldCostIcon;
        public Sprite DiamondCostIcon;
        public Sprite AdIcon;

        [Header("AdMob")]
        [Tooltip("Real rewarded ad unit (AdMob app 개꿀띠펜스, unit '보상형 공용', created 2026-11-16) for the shop and battle result. Editor, Development Build and INTERNAL_TEST_BUILD load Google's test unit instead (ShopAds.ResolveUnitId).")]
        public string RewardedAdUnitIdAndroid = "ca-app-pub-4522111807051546/8378100657";

        /// <summary>Product art for a reward currency at a tier (0 = single, 2 = largest pile).</summary>
        public Sprite Pile(CurrencyType currency, int tier)
        {
            var piles = currency == CurrencyType.Gold ? GoldPiles : currency == CurrencyType.Diamond ? DiamondPiles : currency == CurrencyType.Essence ? EssencePiles : null;
            if (piles == null || piles.Length == 0) return null;
            return piles[Mathf.Clamp(tier, 0, piles.Length - 1)];
        }

        /// <summary>Small icon drawn before a price (null for free and cash).</summary>
        public Sprite CostIcon(CurrencyType currency) =>
            currency == CurrencyType.Gold ? GoldCostIcon : currency == CurrencyType.Diamond ? DiamondCostIcon : currency == CurrencyType.Ad ? AdIcon : null;

        public string CurrencyName(CurrencyType currency) =>
            currency == CurrencyType.Gold ? Gold?.Name : currency == CurrencyType.Diamond ? Diamond?.Name : currency == CurrencyType.Essence ? EssenceName : null;
    }
}
