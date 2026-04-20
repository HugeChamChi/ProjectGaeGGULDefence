using BackEnd;
using LitJson;
using System;
using UnityEngine;

public abstract class LoadShopData
{
    public Action OnCallGetChart;
    public abstract void GetShopChart(string chart_ID,ShopChartManager _shopChartManager, UserShopData userShopData = null);

}
