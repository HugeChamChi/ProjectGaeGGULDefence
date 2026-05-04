using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class ShopTableContainer
{
    public DailyShopTable Daily { get; private set; } = new DailyShopTable();
    public DynamicShopTable Dynamic { get; private set; } = new DynamicShopTable();

    public async UniTask InitializeAsync()
    {
        Dynamic.Initialize();
        await Daily.InitializeAsync();
    }
}
