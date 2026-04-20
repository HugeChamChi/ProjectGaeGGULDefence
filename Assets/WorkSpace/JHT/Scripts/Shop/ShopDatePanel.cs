using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class ShopDatePanel : ShopPanel
{
    [Header("Upper")]
    [SerializeField] private TextMeshProUGUI shopTypeText;

    [Header("Body")]
    [SerializeField] private Transform shopItemParent;

    [Header("Bottom")]
    [SerializeField] private Button resetAdvButton;
    [SerializeField] private Button resetMoneyButton;

    public List<ShopData> shopDataList;
    ShopItemCalculator calculator;

    private CancellationTokenSource token;

    protected override void GetData()
    {
        Debug.Log("µ•¿Ã≈Õ πﬁæ∆ø»");
    }

    public override async void Init(ShopChartManager _shopChartManager)
    {
        ResetSetting(_shopChartManager);

        try
        {
            await WaitData();

            int serverData = shopDataList.Count;
            int count = Mathf.Min(shopItemParent.childCount, serverData);

            shopDataList[0].itemID = -1;
            for (int i = 0; i < count; i++)
            {
                ShopItemPrefab data = shopItemParent.GetChild(i).GetComponent<ShopItemPrefab>();

                data.Init(shopDataList[i], calculator);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("WaitData √Îº“µ ");
        }

        resetAdvButton.onClick.AddListener(ResetItem);
    }

    private void ResetSetting(ShopChartManager _shopChartManager)
    {
        token = new CancellationTokenSource();
        shopDataList = new List<ShopData>();
        calculator = new ShopItemCalculator();

        shopTypeText.text = shopType.ToString();
        SetItemData(_shopChartManager);
    }

    private void SetItemData(ShopChartManager _shopChartManager)
    {
        shopDataList = BackendShopData.Instance.shopDataSetting.FindShopData(_shopChartManager,shopType).ToList();
    }

    private void ResetItem()
    {
        if (BackendShopData.Instance.gold >= 1000)
            BackendShopData.Instance.gold -= 1000;

        Debug.Log($"Gold : {BackendShopData.Instance.gold}");

        BackendShopData.Instance.ResetItemData(shopType);
    }

    private async UniTask WaitData()
    {
        await UniTask.WaitUntil(() => BackendShopData.Instance.isShopItemLoaded,
        cancellationToken: token.Token);
    }

}
