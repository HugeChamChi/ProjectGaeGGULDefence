using UnityEngine;
//using UnityEngine.Purchasing;

public class ShopChartManager : MonoBehaviour
{
    public LoadShopDataModel shopDataModel;
    LoadShopItemData loadShopItemChart;
    LoadShopGoodsData loadShopGoodsChart;

    public int loadedChartCount = 0;

    public void InitShopChart()
    {
        if(shopDataModel == null)
            shopDataModel = new LoadShopDataModel();

        if(loadShopGoodsChart == null)
            loadShopGoodsChart = new LoadShopGoodsData();

        if(loadShopItemChart == null)
            loadShopItemChart = new LoadShopItemData();

        loadShopItemChart.OnCallGetChart += OnChartLoaded;
        loadShopGoodsChart.OnCallGetChart += OnChartLoaded;

        BackendShopData.Instance.ShopDataGet(this);

        loadedChartCount = 0;

        loadShopItemChart.GetShopChart(shopDataModel.SHOP_MONTHLY_ID,this, BackendShopData.userShopData);
        loadShopItemChart.GetShopChart(shopDataModel.SHOP_DAILY_ID, this, BackendShopData.userShopData);
        loadShopItemChart.GetShopChart(shopDataModel.SHOP_WEEKLY_ID, this, BackendShopData.userShopData);
        loadShopGoodsChart.GetShopChart(shopDataModel.SHOP_GOODS_ID, this);
    }

    private void OnDisable()
    {
        loadShopItemChart.OnCallGetChart -= OnChartLoaded;
        loadShopGoodsChart.OnCallGetChart -= OnChartLoaded;
    }
    private void OnApplicationQuit()
    {
        BackendShopData.Instance.ShopDataUpdate();
    }

    private void OnChartLoaded()
    {
        loadedChartCount++;

        if (loadedChartCount >= shopDataModel.TOTAL_CHART_COUNT)
        {
            BackendSetting();
            BackendShopData.Instance.OnShopDataLoaded?.Invoke(this);
        }
    }

    private void BackendSetting()
    {
        if (BackendShopData.userShopData == null)
            BackendShopData.Instance.ShopDataInsert(this);
    }


    public void ResetItem(ShopType _shopType)
    {
        switch (_shopType)
        {
            case ShopType.Daily:
                loadShopItemChart.ResetShopData(shopDataModel.SHOP_DAILY_ID, this, BackendShopData.userShopData);
                break;
            case ShopType.Weekly:
                loadShopItemChart.ResetShopData(shopDataModel.SHOP_WEEKLY_ID, this, BackendShopData.userShopData);
                break;
            case ShopType.Monthly:
                loadShopItemChart.ResetShopData(shopDataModel.SHOP_MONTHLY_ID, this, BackendShopData.userShopData);
                break;
        }
    }
}
