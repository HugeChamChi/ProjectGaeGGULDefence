using UnityEngine;
using Cysharp.Threading.Tasks;

public class UI_ShopPanel : UI_Base
{
    [SerializeField] private UI_ShopListController dailyShopController;

    private void OnEnable()
    {
        dailyShopController.Setup(Player.Shop.DailyItems);
    }

    public void RefreshUI()
    {
        dailyShopController.Refresh();
    }
}
