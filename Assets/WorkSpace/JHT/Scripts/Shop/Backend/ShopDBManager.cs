using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopDBManager
{
    public ShopDBItem shopDBItem;

    #region 날짜단위 아이템 리스트 가져오기
    public void ShopDBGetData(ShopDBItem _shopDBItem)
    {
        if(shopDBItem == null)
            shopDBItem = new ShopDBItem();

        shopDBItem = _shopDBItem;

        var shopType = (ShopType)Enum.Parse(typeof(ShopType), _shopDBItem.ShopType);

        switch (shopType)
        {
            case ShopType.Daily:
                FindListAdd(FindDataManagaer.Instance.dailyItemList);
                break;
            case ShopType.Weekly:
                FindListAdd(FindDataManagaer.Instance.weeklyItemList);
                break;
            case ShopType.Monthly:
                FindListAdd(FindDataManagaer.Instance.monthlyItemList);
                break;
        }
        Debug.Log("DBManager : ServerToInGame");
    }

    public bool ListNullCheck(ShopDBItem _shopDBItem)
    {
        if (_shopDBItem.Shopitem == null || _shopDBItem.Shopitem.Count == 0)
            return false;

        return true;
    }


    private void FindListAdd(List<Item> list)
    {
        foreach (var data in shopDBItem.Shopitem)
        {
            list.Add(FindDataManagaer.Instance.IntDataToItem(data));
        }
    }
    #endregion


    public void ChangeDBItem(ShopType _shopType)
    {
        if (shopDBItem == null)
        {
            Debug.Log("유저정보 없음 재확인 바람");
            shopDBItem = new ShopDBItem();
        }

        FindDataManagaer.Instance.GetRandomItem(_shopType, 8);

    }


}
