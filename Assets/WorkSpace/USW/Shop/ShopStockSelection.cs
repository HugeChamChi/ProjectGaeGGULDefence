using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;

namespace GaeGGUL.Shop
{
    public static class ShopStockSelection
    {
        public const int RandomSlots = 5;
        public static string ProbabilityName(ShopCategory section) => GGD.BackendCharts.ChartEnvironment.Prefix + (section == ShopCategory.Daily ? "ShopDailyProbability" : "ShopWeeklyProbability");

        public static List<ShopCatalogProduct> SelectLocal(IEnumerable<ShopCatalogProduct> products, Func<double> next)
        {
            var all = products.ToList();
            var selected = all.Where(p => p.FixedFree).ToList();
            var pool = all.Where(p => !p.FixedFree).ToList();
            while (selected.Count < RandomSlots + (all.Any(p => p.FixedFree) ? 1 : 0) && pool.Count > 0)
            {
                double ticket = next() * pool.Sum(p => (double)p.Weight);
                int index = 0;
                for (; index < pool.Count - 1; index++) { ticket -= pool[index].Weight; if (ticket < 0) break; }
                selected.Add(pool[index]); pool.RemoveAt(index);
            }
            return selected.OrderByDescending(p => p.FixedFree).ThenBy(p => p.SortOrder).ThenBy(p => p.Item.ShopID).ToList();
        }

        // Backend draws with replacement. Reject repeats to obtain the same weighted-without-replacement distribution.
        public static List<ShopCatalogProduct> AcceptDraws(IEnumerable<ShopCatalogProduct> products, IEnumerable<int> draws)
        {
            var all = products.ToList();
            var selected = all.Where(p => p.FixedFree).ToList();
            var pool = all.Where(p => !p.FixedFree).ToDictionary(p => p.Item.ShopID);
            var seen = new HashSet<int>();
            foreach (int id in draws)
            {
                if (!pool.ContainsKey(id)) throw new InvalidOperationException("Probability and ShopCatalog disagree: " + id);
                if (seen.Add(id)) selected.Add(pool[id]);
                if (seen.Count == Math.Min(RandomSlots, pool.Count)) break;
            }
            return selected.OrderByDescending(p => p.FixedFree).ThenBy(p => p.SortOrder).ThenBy(p => p.Item.ShopID).ToList();
        }

        public static async UniTask<List<ShopItemData>> DrawAsync(IEnumerable<ShopCatalogProduct> products, ShopCategory section, CancellationToken token)
        {
            var all = products.ToList();
            int count = all.Count(p => !p.FixedFree);
            if (count == 0) return all.Select(p => p.Item).ToList();
            string fileId = Chart.GetProbabilityIdByName(ProbabilityName(section));
            if (string.IsNullOrEmpty(fileId)) throw new InvalidOperationException("Missing probability table: " + ProbabilityName(section));
            var draws = new List<int>();
            int expected = Math.Min(RandomSlots, count) + all.Count(p => p.FixedFree);
            for (int batch = 0; batch < 3; batch++)
            {
                var completion = new UniTaskCompletionSource<BackendReturnObject>();
                Backend.Probability.GetProbabilitys(fileId, 20, response => completion.TrySetResult(response));
                var response = await completion.Task.AttachExternalCancellation(token);
                if (!response.IsSuccess()) throw new InvalidOperationException("Shop draw failed: " + response.GetStatusCode());
                var rows = response.GetFlattenJSON()["elements"];
                for (int i = 0; i < rows.Count; i++) draws.Add(rows[i].GetInt("ShopID"));
                var selected = AcceptDraws(all, draws);
                if (selected.Count == expected) return selected.Select(p => p.Item).ToList();
            }
            throw new InvalidOperationException("Not enough distinct shop draws; no stock was saved.");
        }
    }
}
