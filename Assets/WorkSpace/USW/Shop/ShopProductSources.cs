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
        { _products = products; _settings = settings; }

        public IReadOnlyList<ShopProductViewData> GetProducts(ShopSection section)
        {
            if (section == ShopSection.Diamond)
            {
                var store = ShopProductLists.Diamonds(_settings);
                foreach (var item in store) item.CanBuy = true; // Host provides local feedback only.
                return store;
            }
            var category = ShopProductLists.Category(section);
            if (category == null) return Array.Empty<ShopProductViewData>();
            return ShopProductLists.FromCatalog(_products.Where(p => p.Section == category).ToList(), new HashSet<int>(), null, _settings);
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
            if (!Table.Shop.Catalog.IsReady) return Array.Empty<ShopProductViewData>();
            var data = Player.PlayerData.IsReady ? Player.PlayerData.Data : null;
            switch (section)
            {
                case ShopSection.Daily:
                    // A failed account load must not display stock left from a previous account or a partial selection.
                    var daily = Player.Shop.Daily;
                    if (!daily.IsReady) return Array.Empty<ShopProductViewData>();
                    var chart = Table.Shop.Catalog.Products(ShopCategory.Daily).ToDictionary(p => p.Item.ShopID);
                    var stock = daily.DailyItems.Select(item => chart.TryGetValue(item.ShopID, out var p) ? p : null).ToList();
                    if (stock.Any(p => p == null)) return Array.Empty<ShopProductViewData>();
                    return ShopProductLists.FromCatalog(stock, daily.PurchasedItemIDs, data, _settings, daily.PurchaseCount);
                case ShopSection.Diamond: return ChartDiamonds();
                case ShopSection.Package: return ChartPackages();
                case ShopSection.Weekly: return Array.Empty<ShopProductViewData>();
                default:
                    return ShopProductLists.FromCatalog(Table.Shop.Catalog.Products(ShopProductLists.Category(section).Value).ToList(), new HashSet<int>(), data, _settings);
            }
        }

        /// <summary>
        /// Packages sell through the IAP catalog when the store lists them and the catalog payouts match the
        /// chart rewards; otherwise they stay visible with the chart KRW price and report "준비 중".
        /// </summary>
        private IReadOnlyList<ShopProductViewData> ChartPackages()
        {
            var products = Table.Shop.Catalog.Products(ShopCategory.Package).ToList();
            var views = ShopProductLists.FromCatalog(products, new HashSet<int>(), Player.PlayerData.IsReady ? Player.PlayerData.Data : null, _settings);
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i]; var view = views[i];
                if (string.IsNullOrEmpty(p.StoreId) || !Player.PlayerData.IsReady) continue;
                string period = p.LimitPeriod == "Weekly" && ServerClock.IsSynced ? IAPManager.PackagePeriodKey() : null;
                if (p.LimitPeriod == "Weekly" && period == null) continue; // Weekly limits need server time.
                int bought = Player.PlayerData.StorePurchaseCount(p.StoreId, period);
                view.SoldOut = bought >= p.Limit;
                if (_iap == null || !_iap.TryGetProductListing(p.StoreId, out string price, out bool available)) continue;
                if (!_iap.TryGetPackagePayout(p.StoreId, out var payout) || !Matches(p, payout))
                { UnityEngine.Debug.LogWarning("[Shop] Package chart rewards differ from the IAP catalog: " + p.StoreId); continue; }
                view.Price = price; view.ComingSoon = false; view.CanBuy = available && !view.SoldOut;
            }
            return views;
        }

        private static bool Matches(ShopCatalogProduct p, PackagePayout payout)
        {
            int Sum(CurrencyType c) => p.Rewards.Where(r => r.Currency == c).Sum(r => r.Amount);
            return Sum(CurrencyType.Diamond) == payout.Diamond && Sum(CurrencyType.Gold) == payout.Gold && Sum(CurrencyType.Essence) == payout.Essence;
        }

        private IReadOnlyList<ShopProductViewData> ChartDiamonds()
        {
            var store = ShopProductLists.Diamonds(_settings, _iap);
            var result = new List<ShopProductViewData>();
            int tier = 0;
            foreach (var product in Table.Shop.Catalog.Products(ShopCategory.InApp))
            {
                var listing = store.FirstOrDefault(p => p.StoreId == product.StoreId);
                // A chart cannot invent a store ID or change the receipt's grant amount.
                if (listing == null || listing.Amount != product.Item.Amount.ToString("N0") + "개")
                    throw new InvalidOperationException("ShopCatalog and IAP catalog disagree: " + product.StoreId);
                listing.ShopId = product.Item.ShopID;
                listing.Icon = _settings.Pile(CurrencyType.Diamond, tier++);
                result.Add(listing);
            }
            return result;
        }
    }

    /// <summary>Uses the same gold/IAP definitions, with separate period fixtures and no live purchases.</summary>
    public sealed class PreviewShopProductSource : IShopProductSource
    {
        private readonly ShopCatalogSettings _settings;
        private readonly ShopPreviewCatalog _preview;
        private IReadOnlyList<ShopProductViewData> _daily;
        public PreviewShopProductSource(ShopCatalogSettings settings, ShopPreviewCatalog preview)
        { _settings = settings; _preview = preview; Reset(); }

        public void Reset()
        {
            // Copy display state: clicking Sold Out must never mutate a ScriptableObject asset.
            _daily = _preview.Daily.Select(Copy).ToArray();
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
                case ShopSection.Gold: return ShopProductLists.Gold(_settings, false);
                case ShopSection.Diamond:
                    var diamonds = ShopProductLists.Diamonds(_settings);
                    foreach (var item in diamonds) item.CanBuy = true; // The preview host only displays feedback.
                    return diamonds;
                default: return Array.Empty<ShopProductViewData>();
            }
        }
    }

    public static class ShopProductLists
    {
        /// <summary>Chart section shown by a lobby section; null when the section has no chart rows.</summary>
        public static ShopCategory? Category(ShopSection section) => section switch
        {
            ShopSection.Daily => ShopCategory.Daily,
            ShopSection.Gold => ShopCategory.Gold,
            ShopSection.Essence => ShopCategory.Essence,
            ShopSection.Package => ShopCategory.Package,
            ShopSection.Diamond => ShopCategory.InApp,
            _ => null
        };

        /// <summary>
        /// Builds cards from chart products. <paramref name="balances"/> null means a preview: every
        /// payable product is shown as buyable. Art tier follows the order within the section.
        /// </summary>
        public static List<ShopProductViewData> FromCatalog(IReadOnlyList<ShopCatalogProduct> products, ISet<int> purchased, PlayerData balances, ShopCatalogSettings settings, Func<int, int> purchaseCount = null)
        {
            var list = new List<ShopProductViewData>();
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i]; var item = p.Item; var reward = (CurrencyType)item.ItemID;
                bool daily = p.Section == ShopCategory.Daily;
                var view = new ShopProductViewData
                {
                    ShopId = item.ShopID, StoreId = p.StoreId, Title = string.IsNullOrEmpty(p.Name) ? settings.CurrencyName(reward) : p.Name,
                    Amount = item.Amount.ToString("N0"), CostCurrency = item.CurrencyType, CostIcon = settings.CostIcon(item.CurrencyType),
                    Icon = settings.Pile(reward, daily ? 0 : i), Tag = p.Tag, SoldOut = purchased.Contains(item.ShopID)
                };
                foreach (var extra in p.Rewards)
                    view.Rewards.Add(new ShopRewardView { Icon = settings.Pile(extra.Currency, 0), Amount = extra.Amount.ToString("N0") });
                switch (item.CurrencyType)
                {
                    case CurrencyType.Free:
                        view.Price = "무료"; view.CanBuy = true; break;
                    case CurrencyType.Ad:
                        // Remaining views today: 2/2, 1/2, then dimmed 0/2.
                        int limit = Math.Max(1, p.Limit), left = Math.Max(0, limit - (purchaseCount?.Invoke(item.ShopID) ?? 0));
                        view.Price = $"{left}/{limit}"; view.SoldOutText = $"0/{limit}"; view.SoldOut = left == 0;
                        view.ComingSoon = !ShopAds.IsAvailable; view.CanBuy = ShopAds.IsAvailable && left > 0; break;
                    case CurrencyType.Cash:
                        view.Price = "KRW " + item.Price.ToString(System.Globalization.CultureInfo.InvariantCulture); view.ComingSoon = true; break;
                    default:
                        view.Price = item.Price.ToString("N0");
                        bool affordable = balances == null || Balance(balances, item.CurrencyType) >= item.Price;
                        view.CanBuy = affordable; view.Locked = !affordable; break;
                }
                if (balances == null && view.Locked) { view.Locked = false; view.CanBuy = true; }
                list.Add(view);
            }
            return list;
        }

        private static int Balance(PlayerData data, CurrencyType currency) =>
            currency == CurrencyType.Gold ? data.Gold : currency == CurrencyType.Diamond ? data.Diamond : 0;

        public static List<ShopProductViewData> Gold(ShopCatalogSettings settings, bool canBuy)
        {
            if (settings.GoldProducts.Length > 3) throw new InvalidOperationException("The gold row has three slots.");
            var ids = new HashSet<int>(); var list = new List<ShopProductViewData>();
            for (int i = 0; i < settings.GoldProducts.Length; i++)
            {
                var item = settings.GoldProducts[i];
                if (item.ShopId <= 0 || !ids.Add(item.ShopId)) throw new InvalidOperationException("Gold shop IDs must be positive and unique.");
                list.Add(new ShopProductViewData
                {
                    ShopId = item.ShopId, Title = settings.Gold.Name, Icon = settings.Pile(CurrencyType.Gold, i),
                    Amount = item.GoldAmount > 0 ? item.GoldAmount.ToString("N0") : "수량 미정",
                    Price = item.DiamondPrice > 0 ? item.DiamondPrice.ToString("N0") : "가격 미정",
                    CostCurrency = CurrencyType.Diamond, CostIcon = settings.CostIcon(CurrencyType.Diamond), CanBuy = canBuy && item.IsConfigured
                });
            }
            return list;
        }

        public static List<ShopProductViewData> Diamonds(ShopCatalogSettings settings, IAPManager iap = null)
        {
            var catalog = ProductCatalog.LoadDefaultCatalog();
            var definitions = DiamondIapCatalog.Build(catalog, GooglePlay.Name, out var amounts);
            var list = new List<ShopProductViewData>();
            foreach (var definition in definitions)
            {
                string price = iap != null ? iap.GetMissingPriceLabel(definition.id) : "준비 중"; bool available = false;
                if (iap != null && iap.TryGetProductListing(definition.id, out var fetchedPrice, out available)) price = fetchedPrice;
                list.Add(new ShopProductViewData
                {
                    StoreId = definition.id, Title = settings.Diamond.Name, Icon = settings.Pile(CurrencyType.Diamond, 0),
                    Amount = amounts[definition.id].ToString("N0") + "개", Price = price,
                    CostCurrency = CurrencyType.Cash, CanBuy = available
                });
            }
            return list;
        }
    }
}
