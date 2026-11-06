using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class ShopTables
{
    public GaeGGUL.Shop.ShopCatalogTable Catalog { get; private set; } = new GaeGGUL.Shop.ShopCatalogTable();
    public DailyShopTable Daily { get; private set; } = new DailyShopTable();
    public DynamicShopTable Dynamic { get; private set; } = new DynamicShopTable();

    public async UniTask InitializeAsync()
    {
        await Catalog.InitializeAsync();
        await Dynamic.InitializeAsync();
        await Daily.InitializeAsync();
    }
}
