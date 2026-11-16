using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Purchasing;

namespace GaeGGUL.IAP
{
    /// <summary>What one package purchase grants (from the IAP catalog payouts).</summary>
    public sealed class PackagePayout
    {
        public int Diamond, Gold, Essence;
    }

    /// <summary>Consumable packages ("package_" ids) granting diamonds plus gold/essence.</summary>
    public static class PackageIapCatalog
    {
        public const string Prefix = "package_";
        public static bool IsPackage(string productId) => productId != null && productId.StartsWith(Prefix, StringComparison.Ordinal);

        public static List<ProductDefinition> Build(ProductCatalog catalog, string store, out Dictionary<string, PackagePayout> payouts)
        {
            payouts = new Dictionary<string, PackagePayout>();
            var definitions = new List<ProductDefinition>();
            foreach (var item in catalog.allProducts.Where(p => IsPackage(p.id)))
            {
                if (item.type != ProductType.Consumable || payouts.ContainsKey(item.id)) throw new InvalidOperationException("Invalid package product: " + item.id);
                var payout = new PackagePayout();
                foreach (var p in item.Payouts)
                {
                    if (p.type != ProductCatalogPayout.ProductCatalogPayoutType.Currency || double.IsNaN(p.quantity) || p.quantity < 1
                        || p.quantity > int.MaxValue || p.quantity != Math.Truncate(p.quantity)) throw new InvalidOperationException("Invalid package payout: " + item.id);
                    int q = (int)p.quantity;
                    switch (p.subtype)
                    {
                        case "Diamond": payout.Diamond += q; break;
                        case "Gold": payout.Gold += q; break;
                        case "Essence": payout.Essence += q; break;
                        default: throw new InvalidOperationException("Unsupported package currency: " + p.subtype);
                    }
                }
                // The account receipt history requires a diamond grant on every store receipt.
                if (payout.Diamond <= 0) throw new InvalidOperationException("Package must include diamonds: " + item.id);
                payouts.Add(item.id, payout);
                definitions.Add(new ProductDefinition(item.id, item.GetStoreID(store) ?? item.id, item.type));
            }
            return definitions;
        }
    }
}
