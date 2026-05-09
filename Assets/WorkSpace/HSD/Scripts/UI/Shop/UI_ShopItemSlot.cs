using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UI_ShopItemSlot : UI_SlotBase<ShopItemData>
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

    private Action<ShopItemData> _onBuyRequest;

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(() => _onBuyRequest?.Invoke(_data));
    }

    public void SetCallback(Action<ShopItemData> onBuyRequest) => _onBuyRequest = onBuyRequest;

    protected override void OnBind()
    {
        if (_data == null) return;

        if (itemIcon != null) itemIcon.sprite = _data.GetIcon();

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
            if (priceObject != null) priceObject.gameObject.SetActive(false);
            if (freeObject != null) freeObject.gameObject.SetActive(true);
        }
        else
        {
            if (priceObject != null) priceObject.gameObject.SetActive(true);
            if (freeObject != null) freeObject.gameObject.SetActive(false);
            if (currencyIcon != null) currencyIcon.sprite = currencySprite;
        }

        if (itemNameText != null) itemNameText.text = itemName;
        if (amountText != null) amountText.text = $"x{_data.Amount}";
        if (priceText != null) priceText.text = _data.Price.ToString();

        RefreshStatus();
    }

    public void RefreshStatus()
    {
        if (_data == null) return;
        bool isSoldOut = Player.Shop.Daily.IsSoldOut(_data.ShopID);
        if (soldOutObject != null) soldOutObject.SetActive(isSoldOut);
        if (buyButton != null) buyButton.interactable = !isSoldOut;
    }
}
