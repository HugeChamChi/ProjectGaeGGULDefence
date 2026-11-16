using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GGD.BackendCharts;
using LitJson;

namespace GaeGGUL.Shop
{
    /// <summary>Chart section. Reserved values are kept so saved data and old rows never renumber.</summary>
    public enum ShopCategory { Daily, Weekly, Gold, InApp, Package, Essence }

    /// <summary>One extra reward shown inside a package banner.</summary>
    public sealed class ShopReward
    {
        public CurrencyType Currency;
        public int Amount;
    }

    public sealed class ShopCatalogProduct
    {
        public ShopCategory Section;
        public string Name;
        public int SortOrder;
        public string StoreId;
        /// <summary>Purchases allowed per period (daily rows: per day). 0 means unlimited.</summary>
        public int Limit;
        /// <summary>Display tag such as "계정당 1회" (packages).</summary>
        public string Tag;
        /// <summary>Package limit window: Account (once ever) or Weekly. Empty for other sections.</summary>
        public string LimitPeriod;
        /// <summary>Main reward first, then package extras, in display order.</summary>
        public List<ShopReward> Rewards = new List<ShopReward>();
        public ShopItemData Item;
    }

    /// <summary>
    /// Shop stock from two applied backend charts: ShopDailyList (fixed daily products) and
    /// ShopCatalog (packages, gold, essence, in-app diamonds). Store prices and receipts remain with Unity IAP.
    /// </summary>
    public sealed class ShopCatalogTable
    {
        public const string ChartName = ChartEnvironment.Prefix + "ShopCatalog";
        public const string DailyChartName = ChartEnvironment.Prefix + "ShopDailyList";
        /// <summary>Products shown per section; the lobby shop layout has exactly these slots.</summary>
        public static int Capacity(ShopCategory section) => section == ShopCategory.Package ? 2 : section == ShopCategory.Weekly ? 0 : 3;

        private List<ShopCatalogProduct> _products = new List<ShopCatalogProduct>();
        public bool IsReady { get; private set; }
        public IEnumerable<ShopCatalogProduct> Products(ShopCategory section) => _products.Where(p => p.Section == section);

        public async UniTask InitializeAsync()
        {
            IsReady = false;
            _products.Clear();
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(20), DelayType.Realtime);
            try
            {
                var combined = new JsonData(); combined.SetJsonType(JsonType.Array);
                var names = new[] { DailyChartName, ChartName };
                var charts = await BackendChartReader.ReadManyAsync(names, timeout.Token);
                foreach (string name in names)
                {
                    var rows = charts[name];
                    for (int i = 0; i < rows.Count; i++)
                    {
                        bool daily = rows[i]["Section"].ToString() == nameof(ShopCategory.Daily);
                        if (name == DailyChartName != daily) throw new FormatException("Wrong shop section in chart: " + name);
                        combined.Add(rows[i]);
                    }
                }
                _products = Parse(combined);
                IsReady = true;
                UnityEngine.Debug.Log($"[ShopCatalog] Applied chart: {_products.Count} enabled products.");
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[ShopCatalog] Shop unavailable: " + e.Message); }
        }

        /// <summary>Validates every row (enabled or not) and returns enabled products in display order.</summary>
        public static List<ShopCatalogProduct> Parse(JsonData rows)
        {
            var result = new List<ShopCatalogProduct>();
            var ids = new HashSet<int>();
            var stores = new HashSet<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string Text(string key) => r.ContainsKey(key) && r[key] != null ? r[key].ToString().Trim() : "";
                int Number(string key) => int.Parse(Text(key), NumberStyles.Integer, CultureInfo.InvariantCulture);
                T Choice<T>(string key) where T : struct
                {
                    string value = Text(key);
                    if (!Enum.GetNames(typeof(T)).Contains(value)) throw new FormatException("Invalid " + key + ": " + value);
                    return (T)Enum.Parse(typeof(T), value);
                }
                int id = Number("ShopID");
                int enabled = Number("Enabled"), amount = Number("Amount"), price = Number("Price"), order = Number("SortOrder"), limit = Number("Limit");
                var section = Choice<ShopCategory>("Section");
                var reward = Choice<CurrencyType>("RewardCurrency");
                var cost = Choice<CurrencyType>("CurrencyType");
                string store = Text("StoreID"), tag = Text("Tag"), name = Text("Name"), period = Text("LimitPeriod");
                string Fail(string why) => $"Invalid shop product {id}: {why}";

                if (id <= 0 || !ids.Add(id)) throw new FormatException(Fail("duplicate or non-positive ShopID"));
                if ((enabled != 0 && enabled != 1) || amount <= 0 || price < 0 || order < 0 || limit < 0) throw new FormatException(Fail("range"));
                if (reward != CurrencyType.Gold && reward != CurrencyType.Diamond && reward != CurrencyType.Essence) throw new FormatException(Fail("reward currency"));
                if (store != "" && !stores.Add(store)) throw new FormatException(Fail("duplicate StoreID"));
                bool freeLike = cost == CurrencyType.Free || cost == CurrencyType.Ad;
                switch (section)
                {
                    case ShopCategory.Daily:
                        if (store != "" || cost == CurrencyType.Cash || cost == CurrencyType.Essence || (freeLike ? price != 0 : price <= 0) || limit < 1)
                            throw new FormatException(Fail("daily rows need Free/Ad (price 0) or Gold/Diamond cost and Limit >= 1"));
                        break;
                    case ShopCategory.Gold:
                    case ShopCategory.Essence:
                        var expected = section == ShopCategory.Gold ? CurrencyType.Gold : CurrencyType.Essence;
                        if (reward != expected || cost != CurrencyType.Diamond || price <= 0 || store != "" || limit != 0)
                            throw new FormatException(Fail(section + " exchange requires " + expected + " for diamonds"));
                        break;
                    case ShopCategory.InApp:
                        if (reward != CurrencyType.Diamond || cost != CurrencyType.Cash || price != 0 || store == "" || limit != 0)
                            throw new FormatException(Fail("in-app rows grant diamonds through a StoreID"));
                        break;
                    case ShopCategory.Package:
                        // Price is the displayed KRW amount until the store product exists (purchase not connected).
                        if (cost != CurrencyType.Cash || price <= 0 || limit < 1 || tag == "" || (period != "Account" && period != "Weekly"))
                            throw new FormatException(Fail("package rows need Cash price, Limit, Tag and LimitPeriod Account/Weekly"));
                        break;
                    default: throw new FormatException(Fail("section " + section + " is no longer sold"));
                }
                var product = new ShopCatalogProduct
                {
                    Section = section, Name = name, SortOrder = order, StoreId = store, Limit = limit, Tag = tag, LimitPeriod = period,
                    Item = new ShopItemData(id, ItemType.Currency, (int)reward, amount, cost, price)
                };
                product.Rewards.Add(new ShopReward { Currency = reward, Amount = amount });
                product.Rewards.AddRange(ParseExtras(Text("ExtraRewards"), id));
                if (section != ShopCategory.Package && (product.Rewards.Count > 1 || period != "")) throw new FormatException(Fail("ExtraRewards and LimitPeriod are for packages only"));
                if (product.Rewards.Count > 3) throw new FormatException(Fail("a package shows at most three rewards"));
                if (enabled == 1) result.Add(product);
            }
            foreach (ShopCategory section in Enum.GetValues(typeof(ShopCategory)))
                if (result.Count(p => p.Section == section) > Capacity(section)) throw new FormatException($"More than {Capacity(section)} enabled products: {section}");
            return result.OrderBy(p => p.Section).ThenBy(p => p.SortOrder).ThenBy(p => p.Item.ShopID).ToList();
        }

        /// <summary>Parses "Essence:60;Gold:20000".</summary>
        private static IEnumerable<ShopReward> ParseExtras(string text, int id)
        {
            if (text == "") yield break;
            foreach (string part in text.Split(';'))
            {
                var pair = part.Split(':');
                if (pair.Length != 2 || !Enum.TryParse(pair[0].Trim(), out CurrencyType currency) ||
                    (currency != CurrencyType.Gold && currency != CurrencyType.Diamond && currency != CurrencyType.Essence) ||
                    !int.TryParse(pair[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount <= 0)
                    throw new FormatException($"Invalid ExtraRewards in shop product {id}: {text}");
                yield return new ShopReward { Currency = currency, Amount = amount };
            }
        }
    }
}
