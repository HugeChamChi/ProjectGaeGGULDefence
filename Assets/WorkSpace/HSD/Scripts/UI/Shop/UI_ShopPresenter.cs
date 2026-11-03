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
        _view.SetupShopList(_view.Shop.DailyItems);
    }

    public async UniTask BuyItem(ShopItemData data)
    {
        bool success = await _view.Shop.BuyItem(data.ShopID);
        _view.SetupShopList(_view.Shop.DailyItems);
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
