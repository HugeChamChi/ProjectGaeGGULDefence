using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Purchasing;

namespace GaeGGUL.IAP
{
    public static class DiamondIapCatalog
    {
        public static List<ProductDefinition> Build(ProductCatalog catalog, string store, out Dictionary<string, int> amounts)
        {
            amounts = new Dictionary<string, int>();
            var definitions = new List<ProductDefinition>();
            var storeIds = new HashSet<string>();
            foreach (var item in catalog.allProducts)
            {
                var payouts = item.Payouts.ToArray();
                if (string.IsNullOrWhiteSpace(item.id) || item.type != ProductType.Consumable || payouts.Length != 1
                    || payouts[0].type != ProductCatalogPayout.ProductCatalogPayoutType.Currency || payouts[0].subtype != "Diamond"
                    || double.IsNaN(payouts[0].quantity) || payouts[0].quantity < 1 || payouts[0].quantity > int.MaxValue
                    || payouts[0].quantity != Math.Truncate(payouts[0].quantity) || amounts.ContainsKey(item.id))
                    throw new InvalidOperationException("Invalid consumable diamond product: " + item.id);
                string storeId = item.GetStoreID(store) ?? item.id;
                if (!storeIds.Add(storeId)) throw new InvalidOperationException("Duplicate store product ID: " + storeId);
                amounts.Add(item.id, (int)payouts[0].quantity);
                definitions.Add(new ProductDefinition(item.id, storeId, item.type));
            }
            if (definitions.Count == 0) throw new InvalidOperationException("Diamond catalog is empty.");
            return definitions;
        }
    }
}
