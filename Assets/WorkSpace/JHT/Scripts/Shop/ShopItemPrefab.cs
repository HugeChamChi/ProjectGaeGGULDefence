using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class ShopItemPrefab : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image limitImg;
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI priceText;

    public int price;
    CancellationTokenSource token;

    ShopItemModel model;
    ShopItemCalculator calculator;
    public void Init(ShopData _shopData,ShopItemCalculator _calculator)
    {
        token = new CancellationTokenSource();
        model = new ShopItemModel();

        model.shopData = _shopData;
        calculator = _calculator;

        if (model.shopData.itemID != -1)
        {
            model.itemSO = FindDataManagaer.Instance.IntDataToItem(model.shopData.itemID);
            price = Mathf.RoundToInt(model.itemSO.price * (100 - model.shopData.sale) / 100);
            icon.sprite = model.itemSO.icon;
        }

        priceText.text = price.ToString();

        model.PurchaseCount = model.shopData.itemCount;

        limitImg.gameObject.SetActive(false);
        buyButton.onClick.AddListener(ClickAnswer);
        model.OnChangePurchase += StopPurchase;

        StopPurchase(model.PurchaseCount);
    }

    public void Outit()
    {
        if (token != null)
        {
            token.Cancel();
            token.Dispose();
            token = null;
        }
        model.OnChangePurchase -= StopPurchase;
    }

    private void ClickAnswer()
    {
        GetAnswer(model.shopData.moneyType);
    }

    /// <summary>
    /// 아이템 받음
    /// </summary>
    /// <param name="price"></param>
    private void GetAnswer(MoneyType type)
    {
        switch (type)
        {
            case MoneyType.Gold:
                calculator.MCalculate(type,this,model,ref BackendShopData.Instance.gold);
                break;
            case MoneyType.Dia:
                calculator.MCalculate(type, this, model, ref BackendShopData.Instance.dia);
                break;
            case MoneyType.Adv:
                calculator.ACalculate(model);
                break;
        }
    }


    public void StopPurchase(int value)
    {
        if (value <= 0)
        {
            limitImg.gameObject.SetActive(true);
        }
    }

}
