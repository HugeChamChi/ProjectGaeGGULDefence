using System.Collections.Generic;
using GaeGGUL.Shop;

public class ShopDataManager
{
    // 기존 Daily Shop 로직
    public DailyShopManager Daily { get; private set; } = new DailyShopManager();
    public DynamicShopDataManager Dynamic { get; private set; } = new DynamicShopDataManager();

    public async Cysharp.Threading.Tasks.UniTask InitializeAsync()
    {
        await Cysharp.Threading.Tasks.UniTask.WhenAll(
            Daily.InitializeAsync(),
            Dynamic.InitializeAsync()
        );
    }
}
