using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_ShopItemSlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Image currencyIcon;
    [SerializeField] private GameObject soldOutObject;
    [SerializeField] private GameObject priceObject;
    [SerializeField] private GameObject freeObject;
    [SerializeField] private Button buyButton;

    private ShopItemData _data;

    public void Init(ShopItemData data)
    {
        _data = data;
        UpdateUI();
        RefreshStatus();
    }

    private void UpdateUI()
    {
        if (_data == null) return;

        itemIcon.sprite = _data.GetIcon();

        var currencySprite = _data.GetCurrencyIcon();

        if (currencySprite == null && _data.CurrencyType == CurrencyType.Free)
        {
            priceObject.gameObject.SetActive(false);
            freeObject.gameObject.SetActive(true);
        }
        else
        {
            priceObject.gameObject.SetActive(true);
            freeObject.gameObject.SetActive(false);
            currencyIcon.sprite = currencySprite;
        }

        // 아이템 정보 설정 (아이콘, 이름 등은 데이터 시트나 어드레서블 등을 통해 가져와야 함)
        // 여기서는 간단하게 텍스트로 표시
        itemNameText.text = $"{_data.Type.ToString()} {_data.ItemID.ToString()}";
        amountText.text = $"x{_data.Amount.ToString()}";
        priceText.text = _data.Price.ToString();

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    public void RefreshStatus()
    {
        bool isSoldOut = ShopManager.Instance.IsSoldOut(_data.ShopID);
        soldOutObject.SetActive(isSoldOut);
        buyButton.interactable = !isSoldOut;
    }

    private async void OnBuyClicked()
    {
        bool success = await ShopManager.Instance.BuyItem(_data.ShopID);
        if (success)
        {
            RefreshStatus();
            Debug.Log("Purchase Successful!");
        }
        else
        {
            Debug.Log("Purchase Failed (Insufficient currency or already bought)");
        }
    }
}
