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

        Debug.Log($"{data.Type.ToString()}, {data.ItemID} Setting");
        UpdateUI();
        RefreshStatus();
    }

    private void UpdateUI()
    {
        if (_data == null) return;

        itemIcon.sprite = _data.GetIcon();

        string itemName = "Unknown";

        switch (_data.Type)
        {
            case ItemType.Item:
                itemName = Table.Item.Get(_data.ItemID).Name;
                break;
            case ItemType.Character:
                itemName = Table.Character.GetCharacterData(_data.ItemID).Name;
                break;
            case ItemType.Currency:
                itemName = _data.CurrencyType.ToString();
                break;
        }

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

        itemNameText.text = itemName;
        amountText.text = $"x{_data.Amount.ToString()}";
        priceText.text = _data.Price.ToString();

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    public void RefreshStatus()
    {
        bool isSoldOut = Player.Shop.Daily.IsSoldOut(_data.ShopID);
        soldOutObject.SetActive(isSoldOut);
        buyButton.interactable = !isSoldOut;
    }

    private async void OnBuyClicked()
    {
        bool success = await Player.Shop.Daily.BuyItem(_data.ShopID);
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
