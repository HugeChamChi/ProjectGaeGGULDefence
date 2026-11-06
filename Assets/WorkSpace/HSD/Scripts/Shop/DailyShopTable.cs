using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GaeGGUL.Shop;

public class DailyShopTable
{
    public IEnumerable<ShopItemData> AllItems => Table.Shop.Catalog.Products(ShopCategory.Daily).Select(p => p.Item);
    public UniTask InitializeAsync() => UniTask.CompletedTask;
    public ShopItemData GetItem(int id) => AllItems.FirstOrDefault(p => p.ShopID == id);
}
