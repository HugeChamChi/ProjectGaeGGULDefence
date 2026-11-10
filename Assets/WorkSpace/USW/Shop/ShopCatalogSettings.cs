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
    }
}
