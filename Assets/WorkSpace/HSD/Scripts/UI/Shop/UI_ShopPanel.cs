using UnityEngine;
using Cysharp.Threading.Tasks;

public class UI_ShopPanel : UI_Base
{
    [SerializeField] private UI_ShopListController dailyShopController;

    private async void OnEnable()
    {
        await InitializeShop();
    }

    private async UniTask InitializeShop()
    {
        // 일일 상점 UI 설정
        dailyShopController.Setup(Player.Shop.DailyItems);
    }

    public void RefreshUI()
    {
        dailyShopController.Refresh();
    }
}
