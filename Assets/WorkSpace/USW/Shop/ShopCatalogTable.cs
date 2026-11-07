using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GGD.BackendCharts;
using LitJson;

namespace GaeGGUL.Shop
{
    public enum ShopCategory { Daily, Weekly, Gold, InApp }

    public sealed class ShopCatalogProduct
    {
        public ShopCategory Section;
        public int SortOrder;
        public bool FixedFree;
        public int Weight;
        public string StoreId;
        public ShopItemData Item;
    }

    /// <summary>One applied backend chart owns stock. Store prices and receipts remain with Unity IAP.</summary>
    public sealed class ShopCatalogTable
    {
        public const string ChartName = "ShopCatalog";
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
                foreach (string name in new[] { "ShopDailyList", "ShopWeeklyList", ChartName })
                {
                    var rows = await BackendChartReader.ReadAsync(name, timeout.Token);
                    for (int i = 0; i < rows.Count; i++)
                    {
                        string section = rows[i]["Section"].ToString();
                        if (name == "ShopDailyList" ? section != "Daily" : name == "ShopWeeklyList" ? section != "Weekly" : section != "Gold" && section != "InApp")
                            throw new FormatException("Wrong shop section in chart: " + name);
                        combined.Add(rows[i]);
                    }
                }
                _products = Parse(combined);
                IsReady = true;
                UnityEngine.Debug.Log($"[ShopCatalog] Applied chart: {_products.Count} enabled products.");
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[ShopCatalog] Shop unavailable: " + e.Message); }
        }

        public static List<ShopCatalogProduct> Parse(JsonData rows)
        {
            var result = new List<ShopCatalogProduct>();
            var ids = new HashSet<int>();
            var stores = new HashSet<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int Number(string key) => int.Parse(r[key].ToString(), System.Globalization.CultureInfo.InvariantCulture);
                T Choice<T>(string key) where T : struct
                {
                    string value = r[key].ToString();
                    if (!Enum.GetNames(typeof(T)).Contains(value)) throw new FormatException("Invalid " + key + ": " + value);
                    return (T)Enum.Parse(typeof(T), value);
                }
                int fixedFree = Number("FixedFree");
                int weight = Number("Weight");
                int enabled = Number("Enabled"), id = Number("ShopID"), amount = Number("Amount"), price = Number("Price"), order = Number("SortOrder");
                var section = Choice<ShopCategory>("Section");
                var reward = Choice<CurrencyType>("RewardCurrency");
                var cost = Choice<CurrencyType>("CurrencyType");
                bool candidate = (section == ShopCategory.Daily || section == ShopCategory.Weekly) && fixedFree == 0;
                if (weight < 0 || weight > 1000000 || (candidate ? weight == 0 : weight != 0)) throw new FormatException("Invalid shop weight: " + id);
                string store = r["StoreID"].ToString();
                if ((enabled != 0 && enabled != 1) || id <= 0 || !ids.Add(id) || amount <= 0 || price < 0 || order < 0)
                    throw new FormatException("Invalid shop product: " + id);
                if ((fixedFree != 0 && fixedFree != 1) || (fixedFree == 1 && ((section != ShopCategory.Daily && section != ShopCategory.Weekly) || cost != CurrencyType.Free || price != 0)))
                    throw new FormatException("Invalid fixed free product: " + id);
                if (reward != CurrencyType.Gold && reward != CurrencyType.Diamond) throw new FormatException("Unsupported reward currency: " + reward);
                if (section == ShopCategory.InApp)
                {
                    if (reward != CurrencyType.Diamond || cost != CurrencyType.Cash || price != 0 || string.IsNullOrWhiteSpace(store) || !stores.Add(store))
                        throw new FormatException("Invalid IAP product: " + id);
                }
                else if (store != "" || cost == CurrencyType.Cash || (cost == CurrencyType.Free ? price != 0 : price <= 0))
                    throw new FormatException("Invalid shop price: " + id);
                if (section == ShopCategory.Gold && (reward != CurrencyType.Gold || cost != CurrencyType.Diamond))
                    throw new FormatException("Gold exchange requires diamonds: " + id);
                if (enabled == 1) result.Add(new ShopCatalogProduct { Section = section, FixedFree = fixedFree == 1, Weight = weight, SortOrder = order, StoreId = store,
                    Item = new ShopItemData(id, ItemType.Currency, (int)reward, amount, cost, price) });
            }
            foreach (var section in new[] { ShopCategory.Gold, ShopCategory.InApp })
                if (result.Count(p => p.Section == section) > 6) throw new FormatException("More than six products: " + section);
            foreach (var section in new[] { ShopCategory.Daily, ShopCategory.Weekly })
                if (result.Count(p => p.Section == section && p.FixedFree) > 1) throw new FormatException("Duplicate fixed free product: " + section);
            return result.OrderBy(p => p.Section).ThenByDescending(p => p.FixedFree).ThenBy(p => p.SortOrder).ThenBy(p => p.Item.ShopID).ToList();
        }
    }
}
