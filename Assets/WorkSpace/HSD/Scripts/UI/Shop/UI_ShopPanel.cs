using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class ShopItemList : UI_ListBase<ShopItemData, UI_ShopItemSlot> { }

public class UI_ShopPanel : UI_Base
{
    [SerializeField] private ShopItemList dailyShopList;
    private UI_ShopPresenter _presenter;

    protected override void Awake()
    {
        base.Awake();
        _presenter = new UI_ShopPresenter(this);
    }

    private void OnEnable()
    {
        _presenter.Initialize();
    }

    public void SetupShopList(List<ShopItemData> items)
    {
        dailyShopList.Render(items, (data, slot) => 
        {
            slot.SetCallback((d) => _presenter.BuyItem(d).Forget());
        });
    }

    public void RefreshList()
    {
        dailyShopList.RefreshAll();
    }
}
