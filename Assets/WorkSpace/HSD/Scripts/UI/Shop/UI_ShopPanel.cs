using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class ShopItemList : UI_ListBase<ShopItemData, UI_ShopItemSlot> { }

public class UI_ShopPanel : UI_Base
{
    [SerializeField] private ShopItemList dailyShopList;
    [SerializeField] private bool _weekly;
    private UI_ShopPresenter _presenter;
    private System.Threading.CancellationTokenSource _refreshCancellation;
    public GaeGGUL.Shop.DailyShopManager Shop => _weekly ? Player.Shop.Weekly : Player.Shop.Daily;

    protected override void Awake()
    {
        base.Awake();
        _presenter = new UI_ShopPresenter(this);
    }

    private void OnEnable()
    {
        _presenter.Initialize();
        _refreshCancellation = new System.Threading.CancellationTokenSource();
        RefreshPeriodAsync().Forget();
    }

    private void OnDisable()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;
    }

    private async UniTaskVoid RefreshPeriodAsync()
    {
        // Cancellation on disable prevents multiple loops when reopening the panel.
        var token = _refreshCancellation.Token;
        while (!token.IsCancellationRequested)
        {
            if (await Shop.RefreshIfNeededAsync() && !token.IsCancellationRequested) _presenter.Initialize();
            if (!Shop.IsReady) return;
            if (await UniTask.Delay(1000, cancellationToken: token).SuppressCancellationThrow()) return;
        }
    }

    public void SetupShopList(List<ShopItemData> items)
    {
        dailyShopList.Render(items, (data, slot) => 
        {
            slot.SetShop(Shop);
            slot.SetCallback((d) => _presenter.BuyItem(d).Forget());
        });
    }

    public void RefreshList()
    {
        dailyShopList.RefreshAll();
    }
}
