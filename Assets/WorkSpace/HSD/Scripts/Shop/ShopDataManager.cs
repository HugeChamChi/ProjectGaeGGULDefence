using System.Collections.Generic;
using GaeGGUL.Shop;

public class ShopDataManager : Global.IClearable
{
    public bool IsInitialized { get; private set; }
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
    public DailyShopManager Weekly { get; private set; } = new DailyShopManager(weekly: true);
    public DynamicShopDataManager Dynamic { get; private set; } = new DynamicShopDataManager();

    public async Cysharp.Threading.Tasks.UniTask InitializeAsync()
    {
        // Both periods share one server row; create/read it in order to avoid duplicate inserts.
        await Daily.InitializeAsync();
        if (Daily.IsReady) await Weekly.InitializeAsync();
        await Dynamic.InitializeAsync();
        IsInitialized = true;
    }

    public void Clear()
    {
        IsInitialized = false;
        Daily.Clear();
        Weekly.Clear();
        Dynamic.Clear();
    }
}
