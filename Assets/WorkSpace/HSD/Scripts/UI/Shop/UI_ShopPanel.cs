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
        // 샵 매니저 초기화 (이미 되어있을 수도 있지만 안전하게)
        await ShopManager.Instance.InitializeAsync();
        
        // 일일 상점 UI 설정
        dailyShopController.Setup(ShopManager.Instance.DailyItems);
    }

    public void RefreshUI()
    {
        dailyShopController.Refresh();
    }
}
