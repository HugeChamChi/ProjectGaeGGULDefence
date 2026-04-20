using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class ShopItemCalculator
{
    /// <summary>
    /// 일반 아이템 계산
    /// </summary>
    /// <param name="type"></param>
    /// <param name="userGoods"></param>
    public void MCalculate(MoneyType type, ShopItemPrefab _prefab, ShopItemModel _model, ref int userGoods)
    {
        if (userGoods >= _prefab.price &&
            BackendShopData.userShopData.all_Shop[_model.shopData.shopID] > 0 &&
            _model.shopData.itemCount > 0)
        {
            // 서버에 저장 및 현재 실행중 처리
            _model.shopData.itemCount--;
            // 현재 실행중 처리할 데이터
            _model.PurchaseCount--;
            // 서버에 저장할 데이터
            BackendShopData.userShopData.all_Shop[_model.shopData.shopID]--;
            userGoods -= _prefab.price;

            GetItem(_model);
        }
        else
        {
            //Debug.Log("보상 획득 실패");
        }
    }

    /// <summary>
    /// 광고 계산
    /// </summary>
    public void ACalculate(ShopItemModel _model)
    {
        if (BackendShopData.userShopData.all_Shop[_model.shopData.shopID] > 0 &&
            _model.shopData.itemCount > 0)
        {
            AdvActive(_model);
        }
        else
        {
            //Debug.Log($"광고 재생불가 all_Shop의 남은횟수 : {BackendShopData.userShopData.all_Shop[model.shopData.shopID]}");
            //Debug.Log($"광고 재생불가 shopData.itemCount : {model.shopData.itemCount}");
        }
    }

    public void AdvActive(ShopItemModel _model)
    {
        GetItem(_model);
        BackendShopData.userShopData.all_Shop[_model.shopData.shopID]--;
        _model.shopData.itemCount--;
        _model.PurchaseCount--;
    }

    private void GetItem(ShopItemModel _model)
    {
        switch (_model.shopData.getItemType)
        {
            case GetItemType.Item:
                break;
            case GetItemType.Dia:
                BackendShopData.Instance.dia += _model.shopData.getCount;
                break;
            case GetItemType.Gold:
                BackendShopData.Instance.gold += _model.shopData.getCount;
                break;
        }
    }
}
