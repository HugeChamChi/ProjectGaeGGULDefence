using BackEnd;
using LitJson;
using UnityEngine;

public class LoadShopItemData : LoadShopData
{
    public sealed override void GetShopChart(string chart_ID,ShopChartManager _shopChartManager, UserShopData userShopData = null)
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
                            ShopData newChart = new ShopData(jsonData[i], userShopData);
                            _shopChartManager.shopDataModel.shopDataList.Add(newChart);
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
                Debug.LogError($"{chart_ID}의 차트 불러오기 에러 발생 : {callback}");
            }
        });


    }

    public void ResetShopData(string chart_ID,ShopChartManager _shopChartManager, UserShopData userShopData = null)
    {
        var bro = Backend.Chart.GetChartContents(chart_ID);

        if (bro.IsSuccess())
        {
            try
            {
                JsonData jsonData = bro.FlattenRows();

                if (jsonData.Count <= 0)
                {
                    Debug.LogWarning("데이터가 없음");
                }
                else
                {
                    for (int i = 0; i < jsonData.Count; i++)
                    {
                        ShopData newChart = new ShopData(jsonData[i], userShopData, true);
                        _shopChartManager.shopDataModel.resetList.Add(newChart);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        else
        {
            Debug.LogError($"{chart_ID}의 차트 불러오기 에러 발생 : {bro}");
        }

    }
}
