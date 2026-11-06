using System;
using System.Collections.Generic;
using System.Linq;
using GaeGGUL.IAP;
using UnityEngine.Purchasing;

namespace GaeGGUL.Shop.UI
{
    public interface IShopProductSource
    {
        IReadOnlyList<ShopProductViewData> GetProducts(ShopSection section);
    }

    /// <summary>Local exported chart preview. This source never loads or writes account data.</summary>
    public sealed class ChartPreviewShopProductSource : IShopProductSource
    {
        private readonly IReadOnlyList<ShopCatalogProduct> _products;
        private readonly ShopCatalogSettings _settings;
        public ChartPreviewShopProductSource(IReadOnlyList<ShopCatalogProduct> products, ShopCatalogSettings settings)
        {
            var random = new Random();
            _products = new[] { ShopCategory.Daily, ShopCategory.Weekly }.SelectMany(section =>
                ShopStockSelection.SelectLocal(products.Where(p => p.Section == section), random.NextDouble))
                .Concat(products.Where(p => p.Section == ShopCategory.Gold || p.Section == ShopCategory.InApp)).ToArray();
            _settings = settings;
        }

        public IReadOnlyList<ShopProductViewData> GetProducts(ShopSection section)
        {
            var products = _products.Where(p => (int)p.Section == (int)section).ToArray();
            if (section != ShopSection.Diamond)
                return ShopProductLists.Period(products.Select(p => p.Item).ToArray(), new HashSet<int>(), true, _settings);
            var store = ShopProductLists.Diamonds(_settings);
            var result = new List<ShopProductViewData>();
            foreach (var product in products)
            {
                var listing = store.FirstOrDefault(p => p.StoreId == product.StoreId);
                if (listing == null || listing.Amount != product.Item.Amount.ToString("N0") + "개")
                    throw new InvalidOperationException("ShopCatalog and IAP catalog disagree: " + product.StoreId);
                listing.ShopId = product.Item.ShopID;
                listing.CanBuy = true; // Host provides local feedback only.
                result.Add(listing);
            }
            return result;
        }
    }

    /// <summary>Reads loaded account selections; never fetches the server or rolls shop stock while drawing UI.</summary>
    public sealed class AccountShopProductSource : IShopProductSource
    {
        private readonly ShopCatalogSettings _settings;
        private readonly IAPManager _iap;
        public AccountShopProductSource(ShopCatalogSettings settings, IAPManager iap = null)
        { _settings = settings; _iap = iap; }

        public IReadOnlyList<ShopProductViewData> GetProducts(ShopSection section)
        {
            switch (section)
            {
                case ShopSection.Daily: return Period(Player.Shop.Daily, _settings);
                case ShopSection.Weekly: return Period(Player.Shop.Weekly, _settings);
                case ShopSection.Gold: return ChartGold();
                case ShopSection.Diamond: return ChartDiamonds();
                default: throw new ArgumentOutOfRangeException(nameof(section));
            }
        }

        private IReadOnlyList<ShopProductViewData> ChartGold()
        {
            if (!Table.Shop.Catalog.IsReady) return Array.Empty<ShopProductViewData>();
            return ShopProductLists.Period(Table.Shop.Catalog.Products(ShopCategory.Gold).Select(p => p.Item).ToArray(),
                new HashSet<int>(), Player.PlayerData.IsReady, _settings);
        }

        private IReadOnlyList<ShopProductViewData> ChartDiamonds()
        {
            if (!Table.Shop.Catalog.IsReady) return Array.Empty<ShopProductViewData>();
            var store = ShopProductLists.Diamonds(_settings, _iap);
            var result = new List<ShopProductViewData>();
            foreach (var product in Table.Shop.Catalog.Products(ShopCategory.InApp))
            {
                var listing = store.FirstOrDefault(p => p.StoreId == product.StoreId);
                // A chart cannot invent a store ID or change the receipt's grant amount.
                if (listing == null || listing.Amount != product.Item.Amount.ToString("N0") + "개")
                    throw new InvalidOperationException("ShopCatalog and IAP catalog disagree: " + product.StoreId);
                listing.ShopId = product.Item.ShopID;
                result.Add(listing);
            }
            return result;
        }

        private static IReadOnlyList<ShopProductViewData> Period(DailyShopManager shop, ShopCatalogSettings settings)
        {
            // A failed account load must not display stock left from a previous account or a partial selection.
            if (!shop.IsReady) return Array.Empty<ShopProductViewData>();
            return ShopProductLists.Period(shop.DailyItems, shop.PurchasedItemIDs, true, settings);
        }
    }

    /// <summary>Uses the same gold/IAP definitions, with separate period fixtures and no live purchases.</summary>
    public sealed class PreviewShopProductSource : IShopProductSource
    {
        private readonly ShopCatalogSettings _settings;
        private readonly ShopPreviewCatalog _preview;
        private IReadOnlyList<ShopProductViewData> _daily, _weekly;
        public PreviewShopProductSource(ShopCatalogSettings settings, ShopPreviewCatalog preview)
        { _settings = settings; _preview = preview; Reset(); }

        public void Reset()
        {
            // Copy display state: clicking Sold Out must never mutate a ScriptableObject asset.
            _daily = _preview.Daily.Select(Copy).ToArray();
            _weekly = _preview.Weekly.Select(Copy).ToArray();
        }
        private static ShopProductViewData Copy(ShopProductViewData item) => new ShopProductViewData
        {
            ShopId = item.ShopId, StoreId = item.StoreId, Title = item.Title, Amount = item.Amount,
            Price = item.Price, Icon = item.Icon, CostIcon = item.CostIcon, CostCurrency = item.CostCurrency,
            CanBuy = item.CanBuy, SoldOut = item.SoldOut
        };
        public IReadOnlyList<ShopProductViewData> GetProducts(ShopSection section)
        {
            switch (section)
            {
                case ShopSection.Daily: return _daily;
                case ShopSection.Weekly: return _weekly;
                case ShopSection.Gold: return ShopProductLists.Gold(_settings, false);
                case ShopSection.Diamond:
                    var diamonds = ShopProductLists.Diamonds(_settings);
                    foreach (var item in diamonds) item.CanBuy = true; // The preview host only displays feedback.
                    return diamonds;
                default: throw new ArgumentOutOfRangeException(nameof(section));
            }
        }
    }

    public static class ShopProductLists
    {
        private static void Limit(int count)
        { if (count > 6) throw new InvalidOperationException("A shop section cannot contain more than six products."); }

        public static List<ShopProductViewData> Period(IReadOnlyList<ShopItemData> items, ISet<int> purchased, bool ready, ShopCatalogSettings settings)
        {
            Limit(items.Count);
            var list = new List<ShopProductViewData>();
            foreach (var item in items)
            {
                string title = null; UnityEngine.Sprite icon = null;
                if (item.Type == ItemType.Currency)
                {
                    var currency = item.ItemID == (int)CurrencyType.Gold ? settings.Gold : item.ItemID == (int)CurrencyType.Diamond ? settings.Diamond : null;
                    if (currency != null) { title = currency.Name; icon = currency.Icon; }
                }
                else if (item.Type == ItemType.Item)
                {
                    var data = Table.Item.Items.FirstOrDefault(value => value.Id == item.ItemID);
                    if (data != null) { title = data.Name; icon = data.Icon; }
                }
                else if (item.Type == ItemType.Character)
                {
                    var data = Table.Character.Characters.FirstOrDefault(value => value.Id == item.ItemID);
                    if (data != null) { title = data.Name; icon = data.Icon; }
                }
                bool supportedCost = item.CurrencyType == CurrencyType.Free || item.CurrencyType == CurrencyType.Gold || item.CurrencyType == CurrencyType.Diamond;
                list.Add(new ShopProductViewData
                {
                    ShopId = item.ShopID, Title = title ?? "상품 정보 없음", Icon = icon, Amount = item.Amount.ToString("N0") + "개",
                    Price = item.CurrencyType == CurrencyType.Free ? "무료" : item.Price.ToString("N0"), CostCurrency = item.CurrencyType,
                    CostIcon = item.CurrencyType == CurrencyType.Gold ? settings.Gold.Icon : item.CurrencyType == CurrencyType.Diamond ? settings.Diamond.Icon : null,
                    SoldOut = purchased.Contains(item.ShopID),
                    CanBuy = ready && title != null && supportedCost && item.Amount > 0 && (item.CurrencyType == CurrencyType.Free ? item.Price == 0 : item.Price > 0)
                });
            }
            return list;
        }

        public static List<ShopProductViewData> Gold(ShopCatalogSettings settings, bool canBuy)
        {
            Limit(settings.GoldProducts.Length);
            var ids = new HashSet<int>(); var list = new List<ShopProductViewData>();
            foreach (var item in settings.GoldProducts)
            {
                if (item.ShopId <= 0 || !ids.Add(item.ShopId)) throw new InvalidOperationException("Gold shop IDs must be positive and unique.");
                list.Add(new ShopProductViewData
                {
                    ShopId = item.ShopId, Title = settings.Gold.Name, Icon = settings.Gold.Icon,
                    Amount = item.GoldAmount > 0 ? item.GoldAmount.ToString("N0") + "개" : "수량 미정",
                    Price = item.DiamondPrice > 0 ? item.DiamondPrice.ToString("N0") : "가격 미정",
                    CostCurrency = CurrencyType.Diamond, CostIcon = settings.Diamond.Icon, CanBuy = canBuy && item.IsConfigured
                });
            }
            return list;
        }

        public static List<ShopProductViewData> Diamonds(ShopCatalogSettings settings, IAPManager iap = null)
        {
            var catalog = ProductCatalog.LoadDefaultCatalog();
            var definitions = DiamondIapCatalog.Build(catalog, GooglePlay.Name, out var amounts); Limit(definitions.Count);
            var list = new List<ShopProductViewData>();
            foreach (var definition in definitions)
            {
                string price = iap != null ? iap.GetMissingPriceLabel(definition.id) : "스토어 초기화 대기"; bool available = false;
                if (iap != null && iap.TryGetProductListing(definition.id, out var fetchedPrice, out available)) price = fetchedPrice;
                list.Add(new ShopProductViewData
                {
                    StoreId = definition.id, Title = settings.Diamond.Name, Icon = settings.Diamond.Icon,
                    Amount = amounts[definition.id].ToString("N0") + "개", Price = price,
                    CostCurrency = CurrencyType.Cash, CanBuy = available
                });
            }
            return list;
        }
    }
}
