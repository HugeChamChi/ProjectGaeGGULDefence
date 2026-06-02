using System.Collections.Generic;
using GaeGGUL.Shop;

public class ShopDataManager : Global.IClearable
{
    public bool IsDirty { get; set; }

    public async Cysharp.Threading.Tasks.UniTask SaveAsync()
    {
        if (IsDirty)
        {
            IsDirty = false;
        }
        await Cysharp.Threading.Tasks.UniTask.CompletedTask;
    }

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

    public void Clear()
    {
        Daily.Clear();
        Dynamic.Clear();
    }
}
