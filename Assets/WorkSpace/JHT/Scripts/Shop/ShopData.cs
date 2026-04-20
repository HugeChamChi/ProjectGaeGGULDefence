using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 어떤 재화로 구매할것인지
public enum MoneyType
{
    Gold,
    Dia,
    Adv,
    Cash
}

// 구매할 아이템의 종류
public enum ShopType
{
    Daily,
    Weekly,
    Monthly,
    Gold,
    Dia,
    Package
}

//받을 아이템 종류
public enum GetItemType
{
    Item,
    Gold,
    Dia
}

[Serializable]
public class ShopData
{
    public int shopID;
    public int itemID;

    public ShopType shopType;
    public MoneyType moneyType;
    public GetItemType getItemType;

    public int sale;
    public int itemCount;
    public int getCount;

    public ShopData(LitJson.JsonData jsonData, UserShopData userShopData = null, bool isReset = false)
    {
        shopID = int.Parse(jsonData["ShopID"].ToString());

        shopType = (ShopType)Enum.Parse(typeof(ShopType),jsonData["ShopType"].ToString());
        moneyType = (MoneyType)Enum.Parse(typeof(MoneyType), jsonData["MoneyType"].ToString());
        getItemType = (GetItemType)Enum.Parse(typeof(GetItemType), jsonData["GetItemType"].ToString());

        sale = int.Parse(jsonData["Sale"].ToString());

        if (BackendShopData.userShopData == null || isReset)
        {
            itemCount = int.Parse(jsonData["ItemCount"].ToString());
            itemID = int.Parse(jsonData["ItemID"].ToString());
        }
        else
        {
            itemCount = userShopData.all_Shop[int.Parse($"{shopID}")];
            itemID = userShopData.all_ItemID[int.Parse($"{shopID}")];
        }

        getCount = int.Parse(jsonData["GetCount"].ToString());
    }


    public override string ToString()
    {
        return $"ShopID : {shopID}\n" +
        $"ItemID : {itemID}\n" +
        $"ShopType : {shopType}\n" +
        $"Sale : {sale}\n" +
        $"ItemCount : {itemCount}\n" +
        $"GetCount : {getCount}\n" +
        $"MoneyType : {moneyType}" +
        $"GetItemType: {getItemType}";
    }
}

[Serializable]
public class ShopGoodsData
{
    public int shopID;
    public ShopType shopType;
    public MoneyType moneyType;
    public int price;
    public int getCount;

    public ShopGoodsData(LitJson.JsonData jsonData)
    {
        shopID = int.Parse(jsonData["ShopID"].ToString());
        shopType = (ShopType)Enum.Parse(typeof(ShopType), jsonData["ShopType"].ToString());
        price = int.Parse(jsonData["Price"].ToString());
        getCount = int.Parse(jsonData["GetCount"].ToString());
        moneyType = (MoneyType)Enum.Parse(typeof(MoneyType), jsonData["MoneyType"].ToString());
    }

    public override string ToString()
    {
        return $"ShopID : {shopID}\n" +
        $"ShopType : {shopType}\n" +
        $"Price : {price}\n" +
        $"GetCount : {getCount}\n" +
        $"MoneyType : {moneyType}";
    }
}
