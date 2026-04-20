using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopGoodsItemPrefab : MonoBehaviour
{
    public MoneyType moneyType;
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI priceText;

    private int price;
    public ShopGoodsData shopGoodsData;

    CancellationTokenSource token;

    public void Init(ShopGoodsData _shopGoodsData)
    {
        token = new CancellationTokenSource();

        shopGoodsData = _shopGoodsData;
        price = _shopGoodsData.price;
        priceText.text = price.ToString();
        moneyType = _shopGoodsData.moneyType;

        buyButton.onClick.AddListener(ClickAnswer);
    }

    public void Outit()
    {
        if (token != null)
        {
            token.Cancel();
            token.Dispose();
            token = null;
        }

    }
    private void ClickAnswer()
    {
        GetAnswer(price, moneyType);
    }
    private void GetAnswer(int price, MoneyType type)
    {
        switch (type)
        {
            case MoneyType.Gold:
                MCalculate(price, type,ref BackendShopData.Instance.gold);
                break;
            case MoneyType.Dia:
                MCalculate(price, type, ref BackendShopData.Instance.dia);
                break;
        }
    }

    private void MCalculate(int price, MoneyType type,ref int userGoods)
    {
        if (userGoods >= price)
        {
            userGoods -= price;

            Debug.Log($"Buy Goods gold : {BackendShopData.Instance.gold}");
            Debug.Log($"Buy Goods Dia : {BackendShopData.Instance.dia}");
            Debug.Log($"Buy Goods Cash : {BackendShopData.Instance.cash}");

            
        }
        else
        {
            Debug.Log("º¸»ó È¹µæ ½ÇÆÐ");
        }
    }

    

}

