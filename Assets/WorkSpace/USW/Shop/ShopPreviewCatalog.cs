using System;
using UnityEngine;

namespace GaeGGUL.Shop.UI
{
    [CreateAssetMenu(menuName = "Shop/Preview Catalog")]
    public sealed class ShopPreviewCatalog : ScriptableObject
    {
        [Tooltip("Preview only. Production daily/weekly products come from Player.Shop saved selections.")]
        public ShopProductViewData[] Daily = Array.Empty<ShopProductViewData>();
        public ShopProductViewData[] Weekly = Array.Empty<ShopProductViewData>();
    }
}
