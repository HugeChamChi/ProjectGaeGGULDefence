using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Shop.UI
{
    /// <summary>One reward drawn inside a package banner.</summary>
    [Serializable]
    public sealed class ShopRewardView
    {
        public Sprite Icon;
        public string Amount;
    }

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
        /// <summary>Affordability lock: the player cannot pay the price right now.</summary>
        public bool Locked;
        /// <summary>Shown with its price but not sold yet (packages, ad rewards). Taps report "준비 중".</summary>
        public bool ComingSoon;
        /// <summary>Price text while sold out; null shows "구매완료" (ad rewards show "0/2").</summary>
        public string SoldOutText;
        /// <summary>Package tag such as "계정당 1회".</summary>
        public string Tag;
        public List<ShopRewardView> Rewards = new List<ShopRewardView>();
    }

    public sealed class ShopProductSlot : MonoBehaviour
    {
        public Image icon, costIcon;
        public TMP_Text title, amount, price;
        public Button buyButton;
        public GameObject soldOut;
        [Tooltip("Optional dark cover with a lock, shown when the price cannot be paid.")]
        public GameObject locked;
        [Tooltip("Optional package tag label.")]
        public TMP_Text tag;
        [Tooltip("Optional package reward boxes (icon + amount), in order.")]
        public GameObject[] rewardBoxes = Array.Empty<GameObject>();
        public Image[] rewardIcons = Array.Empty<Image>();
        public TMP_Text[] rewardAmounts = Array.Empty<TMP_Text>();
        [Tooltip("When above zero, the product art keeps its source pixel size times this scale (pile art grows by tier).")]
        public float nativeIconScale;
        [Tooltip("Price color while sold out.")]
        public Color soldOutPriceColor = new Color(.62f, .62f, .62f);
        public ShopProductViewData product;
        private Action<ShopProductSlot> _onBuy;
        private bool _wired;
        private Color _priceColor;
        private bool _colorCaptured;

        public void Bind(ShopProductViewData data, Action<ShopProductSlot> onBuy)
        {
            product = data; _onBuy = onBuy;
            if (!_wired) { buyButton.onClick.AddListener(Click); _wired = true; }
            Refresh();
        }

        private void Click()
        {
            if (product == null || product.SoldOut) return;
            if (product.CanBuy || product.ComingSoon || product.Locked) _onBuy?.Invoke(this);
        }

        public void SetSoldOut(bool value) { product.SoldOut = value; Refresh(); }

        public void Refresh()
        {
            if (product == null) return;
            if (!_colorCaptured && price != null) { _priceColor = price.color; _colorCaptured = true; }
            if (icon != null)
            {
                icon.sprite = product.Icon; icon.enabled = product.Icon != null;
                if (product.Icon != null && nativeIconScale > 0f) icon.rectTransform.sizeDelta = product.Icon.rect.size * nativeIconScale;
            }
            if (title != null) title.text = product.Title;
            if (amount != null) amount.text = product.Amount;
            if (tag != null) tag.text = product.Tag ?? "";
            if (price != null)
            {
                price.text = product.SoldOut ? product.SoldOutText ?? "구매완료" : product.Price;
                price.color = product.SoldOut ? soldOutPriceColor : _priceColor;
            }
            if (costIcon != null)
            {
                costIcon.sprite = product.CostIcon;
                costIcon.gameObject.SetActive(product.CostIcon != null && (!product.SoldOut || product.SoldOutText != null));
            }
            for (int i = 0; i < rewardBoxes.Length; i++)
            {
                bool show = i < product.Rewards.Count;
                rewardBoxes[i].SetActive(show);
                if (!show) continue;
                if (i < rewardIcons.Length) rewardIcons[i].sprite = product.Rewards[i].Icon;
                if (i < rewardAmounts.Length) rewardAmounts[i].text = product.Rewards[i].Amount;
            }
            if (soldOut != null) soldOut.SetActive(product.SoldOut);
            if (locked != null) locked.SetActive(product.Locked && !product.SoldOut);
            buyButton.interactable = !product.SoldOut && (product.CanBuy || product.ComingSoon || product.Locked);
        }
    }
}
