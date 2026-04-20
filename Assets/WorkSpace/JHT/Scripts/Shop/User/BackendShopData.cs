using BackEnd;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VersionControl;
using UnityEngine;

internal class BackendShopData
{
    private static BackendShopData instance = null;
    internal static BackendShopData Instance
    {
        get
        {
            if (instance == null)
                instance = new BackendShopData();

            return instance;
        }
    }

    //테스트용
    public int gold = 10000;
    public int dia = 10000;
    public int cash = 10000;

    internal static UserShopData userShopData;
    private ShopChartManager shopChartManager;
    internal BackendShopDataSetting shopDataSetting;

    private string gameDataRowInDate = string.Empty;

    public bool isShopItemLoaded;

    public Action<ShopChartManager> OnShopDataLoaded;

    public void ShopDataInsert(ShopChartManager _shopChartManager)
    {
        if (userShopData == null)
        {
            userShopData = new UserShopData();
        }

        if (shopChartManager == null)
            shopChartManager = _shopChartManager;

        if(shopDataSetting == null)
            shopDataSetting = new BackendShopDataSetting();

        isShopItemLoaded = false;

        if (FindDataManagaer.Instance == null)
            return;

        //임시
        while (!FindDataManagaer.Instance.isItemListLoaded)
        {
            Debug.Log("FindDataManager 데이터를 위한 대기");
        }

        foreach (var data in shopChartManager.shopDataModel.shopDataList)
        {
            var inst = FindDataManagaer.Instance;

            switch (data.shopType)
            {
                case ShopType.Daily:
                    shopDataSetting.RandomSet(data, inst.dailyItemList, userShopData.daily_Shop);
                    break;
                case ShopType.Weekly:
                    shopDataSetting.RandomSet(data, inst.weeklyItemList, userShopData.weekly_Shop);
                    break;
                case ShopType.Monthly:
                    shopDataSetting.RandomSet(data, inst.monthlyItemList, userShopData.monthly_Shop);
                    break;
            }
            userShopData.all_Shop.Add(data.shopID, data.itemCount);
        }


        Param param = new Param();
        param.Add("daily_Shop", userShopData.daily_Shop);
        param.Add("weekly_Shop", userShopData.weekly_Shop);
        param.Add("monthly_Shop", userShopData.monthly_Shop);
        param.Add("all_Shop", userShopData.all_Shop);
        param.Add("all_ItemID", userShopData.all_ItemID);

        // 생성한 테이블에 삽입요청
        BackendReturnObject bro = Backend.GameData.Insert("SHOP_USE_DATA", param);

        if (bro.IsSuccess())
        {
            //삽입한 게임 정보의 고유값입니다. 
            gameDataRowInDate = bro.GetInDate();
            isShopItemLoaded = true;
        }
        else
        {
            Debug.LogError("게임 정보 데이터 삽입 실패 : " + bro);
        }
    }

    public void ShopDataGet(ShopChartManager _shopChartManager)
    {
        if (shopChartManager == null)
            shopChartManager = _shopChartManager;

        if (shopDataSetting == null)
            shopDataSetting = new BackendShopDataSetting();

        var bro = Backend.GameData.GetMyData("SHOP_USE_DATA", new Where());

        if (bro.IsSuccess())
        {
            LitJson.JsonData gameJsonData = bro.FlattenRows();

            if (gameJsonData.Count <= 0)
            {
                Debug.LogWarning("데이터가 존재하지 않습니다");
            }
            else
            {
                //불러온 게임 정보의 고유값
                gameDataRowInDate = gameJsonData[0]["inDate"].ToString();

                userShopData = new UserShopData();

                for (int i = 0; i < gameJsonData[0]["daily_Shop"].Count; i++)
                {
                    userShopData.daily_Shop.Add(int.Parse(gameJsonData[0]["daily_Shop"][i].ToString()));
                }
                for (int i = 0; i < gameJsonData[0]["weekly_Shop"].Count; i++)
                {
                    userShopData.weekly_Shop.Add(int.Parse(gameJsonData[0]["weekly_Shop"][i].ToString()));
                }
                for (int i = 0; i < gameJsonData[0]["monthly_Shop"].Count; i++)
                {
                    userShopData.monthly_Shop.Add(int.Parse(gameJsonData[0]["monthly_Shop"][i].ToString()));
                }
                foreach (string itemKey in gameJsonData[0]["all_Shop"].Keys)
                {
                    userShopData.all_Shop.Add(int.Parse(itemKey), int.Parse(gameJsonData[0]["all_Shop"][itemKey].ToString()));
                }
                foreach(string itemKey in gameJsonData[0]["all_ItemID"].Keys)
                {
                    userShopData.all_ItemID.Add(int.Parse(itemKey), int.Parse(gameJsonData[0]["all_ItemID"][itemKey].ToString()));
                }

                isShopItemLoaded = true;
            }

        }
        else
        {
            Debug.LogError("게임 정보 조회 실패 : " + bro);
        }
    }

    public void ShopDataUpdate()
    {
        if (userShopData == null)
        {
            Debug.LogError("서버에서 다운받거나 새로 삽입한 데이터가 존재하지 않습니다. Insert 혹은 Get을 통해 데이터를 생성해주세요.");
            return;
        }

        Param param = new Param();
        param.Add("daily_Shop", userShopData.daily_Shop);
        param.Add("weekly_Shop", userShopData.weekly_Shop);
        param.Add("monthly_Shop", userShopData.monthly_Shop);
        param.Add("all_Shop", userShopData.all_Shop);
        param.Add("all_ItemID", userShopData.all_ItemID);

        BackendReturnObject bro = null;

        if (string.IsNullOrEmpty(gameDataRowInDate))
        {
            bro = Backend.GameData.Update("SHOP_USE_DATA", new Where(), param);
        }
        else
        {
            bro = Backend.GameData.UpdateV2("SHOP_USE_DATA", gameDataRowInDate, Backend.UserInDate, param);
        }

    }

    public void ResetItemData(ShopType _shopType)
    {
        if (shopDataSetting == null)
            shopDataSetting = new BackendShopDataSetting();

        if (shopChartManager.shopDataModel.resetList.Count > 0)
            shopChartManager.shopDataModel.resetList.Clear();

        shopDataSetting.RemoveData(shopChartManager, _shopType);
        shopChartManager.ResetItem(_shopType);

        if (userShopData == null)
        {
            userShopData = new UserShopData();
        }

        if (FindDataManagaer.Instance == null)
            return;

        foreach (var data in shopChartManager.shopDataModel.resetList)
        {
            var inst = FindDataManagaer.Instance;

            switch (data.shopType)
            {
                case ShopType.Daily:
                    shopDataSetting.RandomSet(data, inst.dailyItemList, userShopData.daily_Shop);
                    break;
                case ShopType.Weekly:
                    shopDataSetting.RandomSet(data, inst.weeklyItemList, userShopData.weekly_Shop);
                    break;
                case ShopType.Monthly:
                    shopDataSetting.RandomSet(data, inst.monthlyItemList, userShopData.monthly_Shop);
                    break;
            }
            shopChartManager.shopDataModel.shopDataList.Add(data);
            userShopData.all_Shop[data.shopID] = data.itemCount;
        }

        ShopDataUpdate();
        OnShopDataLoaded?.Invoke(shopChartManager);
    }

}
