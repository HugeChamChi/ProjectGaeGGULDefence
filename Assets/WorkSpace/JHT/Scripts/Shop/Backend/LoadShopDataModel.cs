using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadShopDataModel
{
    public readonly string SHOP_GOODS_ID = "236872";
    public readonly string SHOP_DAILY_ID = "236899";
    public readonly string SHOP_WEEKLY_ID = "236902";
    public readonly string SHOP_MONTHLY_ID = "236901";
    
    public readonly int TOTAL_CHART_COUNT = 4;


    public List<ShopData> shopDataList = new List<ShopData>();
    public List<ShopGoodsData> shopGoodsDataList = new List<ShopGoodsData>();
    public List<ShopData> resetList = new List<ShopData>();

}
