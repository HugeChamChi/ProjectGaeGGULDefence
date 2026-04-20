using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class ShopDiaGold : ShopPanel
{
    [Header("Upper")]
    [SerializeField] private TextMeshProUGUI shopTypeText;

    [Header("Body")]
    [SerializeField] private Transform shopGoodsItemParent;

    public List<ShopGoodsData> shopGoodsDataList;
    
    protected override void GetData()
    {
        Debug.Log("µ•¿Ã≈Õ ª¿‘");
    }

    public override void Init(ShopChartManager _shopChartManager)
    {
        shopTypeText.text = shopType.ToString();

        SetItemData(_shopChartManager);

        int serverData = shopGoodsDataList.Count;
        int count = Mathf.Min(shopGoodsItemParent.childCount, serverData);

        for (int i = 0; i < count; i++)
        {
            ShopGoodsItemPrefab data = shopGoodsItemParent.GetChild(i).GetComponent<ShopGoodsItemPrefab>();
            data.Init(shopGoodsDataList[i]);
        }
    }

    private void SetItemData(ShopChartManager _shopChartManager)
    {
        shopGoodsDataList = new List<ShopGoodsData>();
        
        shopGoodsDataList = BackendShopData.Instance.shopDataSetting.FindShopGoodsData(_shopChartManager,shopType).ToList();
    }
}
