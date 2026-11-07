using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Shop.UI
{
    [Serializable]
    public sealed class ShopProductViewData
    {
        public int ShopId;
        public string StoreId;
        public string Title;
        public string Amount;
        public string Price;
        public Sprite Icon;
        public Sprite CostIcon;
        public CurrencyType CostCurrency;
        public bool CanBuy;
        public bool SoldOut;
    }

    public sealed class ShopProductSlot : MonoBehaviour
    {
        public Image icon, costIcon;
        public TMP_Text title, amount, price;
        public Button buyButton;
        public GameObject soldOut;
        public ShopProductViewData product;
        private Action<ShopProductSlot> _onBuy;
        private bool _wired;

        public void Bind(ShopProductViewData data, Action<ShopProductSlot> onBuy)
        {
            product = data; _onBuy = onBuy;
            if (!_wired) { buyButton.onClick.AddListener(Click); _wired = true; }
            Refresh();
        }

        private void Click() { if (product.CanBuy && !product.SoldOut) _onBuy?.Invoke(this); }

        public void SetSoldOut(bool value) { product.SoldOut = value; Refresh(); }

        public void Refresh()
        {
            if (product == null) return;
            icon.sprite = product.Icon; icon.enabled = product.Icon != null;
            title.text = product.Title; amount.text = product.Amount; price.text = product.Price;
            costIcon.sprite = product.CostIcon; costIcon.gameObject.SetActive(product.CostIcon != null);
            soldOut.SetActive(product.SoldOut);
            buyButton.interactable = product.CanBuy && !product.SoldOut;
        }
    }
}
