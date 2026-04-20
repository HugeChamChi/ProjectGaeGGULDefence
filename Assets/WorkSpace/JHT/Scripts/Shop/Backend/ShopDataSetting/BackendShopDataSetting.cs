using BackEnd;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class BackendShopDataSetting
{

    public void RemoveData(ShopChartManager _shopChartMaanger, ShopType _shopType)
    {
        var list = _shopChartMaanger.shopDataModel.shopDataList
            .Where(x => x.shopType == _shopType)
            .ToList();

        for (int i = 0; i < list.Count; i++)
        {
            _shopChartMaanger.shopDataModel.shopDataList.Remove(list[i]);
        }

        switch (_shopType)
        {
            case ShopType.Daily:
                BackendShopData.userShopData.daily_Shop.Clear();
                break;
            case ShopType.Weekly:
                BackendShopData.userShopData.weekly_Shop.Clear();
                break;
            case ShopType.Monthly:
                BackendShopData.userShopData.monthly_Shop.Clear();
                break;
        }

    }


    public void RandomSet(ShopData data, List<Item> _itemList, List<int> _shopList)
    {
        var inst = _itemList
                        .Where(x => !_shopList.Contains(x.id))  // ÀÌ¹Ì »ÌÈù id Á¦¿Ü
                        .OrderBy(_ => UnityEngine.Random.value)
                        .FirstOrDefault();

        data.itemID = inst.id;
        _shopList.Add(inst.id);
        BackendShopData.userShopData.all_ItemID[data.shopID] = inst.id;

    }

    public List<ShopData> FindShopData(ShopChartManager _shopChartManager,ShopType _shopType)
    {
        var list = _shopChartManager.shopDataModel.shopDataList.Where(x => x.shopType == _shopType).ToList();
        if (list == null)
        {
            Debug.LogError("FindShopData - list : null");
        }
        return list;
    }

    public List<ShopGoodsData> FindShopGoodsData(ShopChartManager _shopChartManager,ShopType _shopType)
    {
        var list = _shopChartManager.shopDataModel.shopGoodsDataList.Where(x => x.shopType == _shopType).ToList();
        if (list == null)
        {
            Debug.LogError("FindShopData - list : null");
        }
        return list;
    }

}
