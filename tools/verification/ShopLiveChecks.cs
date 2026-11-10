using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BackEnd;
using LitJson;
using UnityEditor;
using UnityEngine;
using GaeGGUL.Shop;
using GaeGGUL.Shop.UI;

// Read master data and execute stock draws only. Never load/save PlayerShopData or grant purchases.
public static class ShopLiveChecks
{
    static List<string> report;
    static int checks;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++; report.Add("PASS " + name); Save();
    }
    static void Save()
    {
        Directory.CreateDirectory("outputs/shop-live");
        File.WriteAllLines("outputs/shop-live/RESULT.txt", report);
    }
    public static async Task<string> Run()
    {
        report = new List<string>(); checks = 0;
        try
        {
            Check(Application.isPlaying, "Play Mode");
            var init = Backend.Initialize(); Check(init.IsSuccess(), "SDK initialization");
            if (!Backend.IsLogin)
            {
                var login = Backend.BMember.CustomLogin("testuser", "testpass");
                Check(login.IsSuccess(), "existing Editor test account login");
            }
            await Chart.InitializeAsync();
            var expected = JsonMapper.ToObject(File.ReadAllText("Assets/WorkSpace/USW/Shop/Data/ShopCatalogEditorFixture.json"));
            var combined = new JsonData(); combined.SetJsonType(JsonType.Array);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            foreach (string name in new[] { "ShopDailyList", "ShopWeeklyList", "ShopCatalog" })
            {
                var rows = await GGD.BackendCharts.BackendChartReader.ReadAsync(name, timeout.Token);
                Check(rows != null && rows.IsArray, name + " applied CDN file");
                var scope = name == "ShopDailyList" ? new[] { "Daily" } : name == "ShopWeeklyList" ? new[] { "Weekly" } : new[] { "Gold", "InApp" };
                var local = new Dictionary<int, JsonData>();
                for (int i = 0; i < expected.Count; i++) if (scope.Contains(expected[i]["Section"].ToString())) local.Add(expected[i].GetInt("ShopID"), expected[i]);
                Check(rows.Count == local.Count, name + " row count " + rows.Count);
                var seen = new HashSet<int>();
                for (int i = 0; i < rows.Count; i++)
                {
                    int id = rows[i].GetInt("ShopID");
                    if (!seen.Add(id) || !local.TryGetValue(id, out var source)) throw new Exception("Unexpected/duplicate live product: " + id);
                    foreach (string key in source.Keys)
                        if (!rows[i].ContainsKey(key) || rows[i][key].ToString() != source[key].ToString()) throw new Exception(name + " differs from exported data: " + id + "/" + key);
                    combined.Add(rows[i]);
                }
                Check(true, name + " all columns match planner export");
            }
            var products = ShopCatalogTable.Parse(combined);
            Check(products.Count == 25, "live master parsing: 25 products");
            var table = new ShopCatalogTable(); await table.InitializeAsync();
            Check(table.IsReady && Enum.GetValues(typeof(ShopCategory)).Cast<ShopCategory>().Sum(s => table.Products(s).Count()) == products.Count, "production catalog loader reads all three charts");
            var selected = new List<ShopCatalogProduct>();
            foreach (var section in new[] { ShopCategory.Daily, ShopCategory.Weekly })
            {
                string name = ShopStockSelection.ProbabilityName(section);
                string id = Chart.GetProbabilityIdByName(name);
                Check(!string.IsNullOrEmpty(id) && id != "0", name + " applied file ID");
                var response = Backend.Probability.GetProbabilityContents(id);
                Check(response.IsSuccess(), name + " read contents");
                var rows = response.FlattenRows();
                var pool = products.Where(p => p.Section == section).ToArray();
                var candidates = pool.Where(p => !p.FixedFree).ToDictionary(p => p.Item.ShopID);
                Check(rows.Count == candidates.Count, name + " candidate count " + rows.Count);
                decimal sum = 0; var seen = new HashSet<int>();
                for (int i = 0; i < rows.Count; i++)
                {
                    int shopId = rows[i].GetInt("ShopID");
                    if (!seen.Add(shopId) || !candidates.TryGetValue(shopId, out var product)) throw new Exception(name + " unknown/duplicate/fixed-free ID: " + shopId);
                    decimal percent = decimal.Parse(rows[i]["percent"].ToString(), CultureInfo.InvariantCulture);
                    decimal target = 100m * product.Weight / candidates.Values.Sum(p => (decimal)p.Weight);
                    if (Math.Abs(percent - target) > .0000001m) throw new Exception(name + " weight mismatch: " + shopId);
                    sum += percent;
                }
                Check(sum == 100m, name + " valid ID joins and percent sum 100");
                var stock = await ShopStockSelection.DrawAsync(pool, section, timeout.Token);
                Check(stock.Count == 6 && stock.Select(p => p.ShopID).Distinct().Count() == 6, section + " live draw: six unique slots");
                Check(stock[0].CurrencyType == CurrencyType.Free && pool.Single(p => p.FixedFree).Item.ShopID == stock[0].ShopID, section + " fixed free first");
                Check(stock.Skip(1).All(p => candidates.ContainsKey(p.ShopID)), section + " drawn IDs belong to correct list");
                report.Add(section + " live IDs: " + string.Join(",", stock.Select(p => p.ShopID)));
                foreach (var item in stock) selected.Add(pool.Single(p => p.Item.ShopID == item.ShopID));
            }
            selected.AddRange(products.Where(p => p.Section == ShopCategory.Gold || p.Section == ShopCategory.InApp));
            var page = UnityEngine.Object.FindFirstObjectByType<ShopPageView>();
            Check(page != null, "shop page exists");
            var settings = AssetDatabase.LoadAssetAtPath<ShopCatalogSettings>("Assets/WorkSpace/USW/Shop/Data/ShopCatalogSettings.asset");
            page.SetSource(new ChartPreviewShopProductSource(selected, settings));
            foreach (var section in page.sections)
            {
                var expectedIds = selected.Where(p => (int)p.Section == (int)section.Kind).Select(p => p.Item.ShopID).ToArray();
                Check(section.Slots.Where(s => s.gameObject.activeSelf).Select(s => s.product.ShopId).SequenceEqual(expectedIds), section.Kind + " live stock rendered with matching IDs");
            }
            report.Add("PASS: " + checks + " live checks. No account shop writes, purchases, balance changes or store calls."); Save();
            return string.Join("\n", report);
        }
        catch (Exception e) { report.Add("FAIL " + e.Message); Save(); throw; }
    }
}
