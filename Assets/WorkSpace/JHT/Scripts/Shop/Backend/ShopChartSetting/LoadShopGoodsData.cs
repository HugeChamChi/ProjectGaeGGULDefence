using BackEnd;
using LitJson;
using UnityEngine;

public class LoadShopGoodsData : LoadShopData
{
    public sealed override void GetShopChart(string chart_ID, ShopChartManager _shopChartManager, UserShopData userShopData = null)
    {
        Backend.Chart.GetChartContents(chart_ID, callback =>
        {
            if (callback.IsSuccess())
            {
                try
                {
                    JsonData jsonData = callback.FlattenRows();

                    if (jsonData.Count <= 0)
                    {
                        Debug.LogWarning("데이터가 없음");
                    }
                    else
                    {
                        for (int i = 0; i < jsonData.Count; i++)
                        {
                            ShopGoodsData newChart = new ShopGoodsData(jsonData[i]);
                            _shopChartManager.shopDataModel.shopGoodsDataList.Add(newChart);
                        }
                        OnCallGetChart?.Invoke();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
            else
            {
                Debug.LogError($"차트 불러오기 에러 발생 : {callback}");
            }
        });
    }
}
