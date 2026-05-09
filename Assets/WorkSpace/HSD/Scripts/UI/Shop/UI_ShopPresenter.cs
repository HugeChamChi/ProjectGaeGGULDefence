using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class UI_ShopPresenter
{
    private readonly UI_ShopPanel _view;

    public UI_ShopPresenter(UI_ShopPanel view)
    {
        _view = view;
    }

    public void Initialize()
    {
        _view.SetupShopList(Player.Shop.Daily.DailyItems);
    }

    public async UniTask BuyItem(ShopItemData data)
    {
        bool success = await Player.Shop.Daily.BuyItem(data.ShopID);
        if (success)
        {
            _view.RefreshList();
            UnityEngine.Debug.Log("Purchase Successful!");
        }
        else
        {
            UnityEngine.Debug.Log("Purchase Failed (Insufficient currency or already bought)");
        }
    }
}
