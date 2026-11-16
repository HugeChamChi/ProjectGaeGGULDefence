using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace GaeGGUL.Shop.UI
{
    /// <summary>Lobby shop sections. Weekly is reserved (no longer sold) so authored values never renumber.</summary>
    public enum ShopSection { Daily, Weekly, Gold, Diamond, Package, Essence }

    [Serializable]
    public sealed class ShopSectionView
    {
        public ShopSection Kind;
        public RectTransform Root;
        public GridLayoutGroup Grid;
        public ShopProductSlot[] Slots;
    }

    /// <summary>Canvas-independent authored shop body; the embedding host owns purchases and balances.</summary>
    public sealed class ShopPageView : MonoBehaviour
    {
        public ScrollRect scroll;
        public ShopSectionView[] sections;
        [Tooltip("Optional label for the time left until the daily products reset.")]
        public TMP_Text dailyResetLabel;
        public event Action<ShopSection, ShopProductSlot> PurchaseRequested;
        [Tooltip("Optional small button beside the daily products that buys the remove-ads store product.")]
        public Button removeAdsButton;
        public TMP_Text removeAdsLabel;
        public event Action RemoveAdsRequested;

        private void OnEnable()
        {
            BindAuthoredProducts();
            if (removeAdsButton != null) { removeAdsButton.onClick.RemoveListener(OnRemoveAdsClicked); removeAdsButton.onClick.AddListener(OnRemoveAdsClicked); }
        }
        private void OnDisable() { if (removeAdsButton != null) removeAdsButton.onClick.RemoveListener(OnRemoveAdsClicked); }
        private void OnRemoveAdsClicked() => RemoveAdsRequested?.Invoke();

        /// <summary>Shows the remove-ads button text, e.g. "광고 제거 ₩5,500" or "광고 제거됨".</summary>
        public void SetRemoveAds(string text, bool interactable)
        {
            if (removeAdsButton == null) return;
            removeAdsButton.interactable = interactable;
            if (removeAdsLabel != null && removeAdsLabel.text != text) removeAdsLabel.text = text;
        }

        public void SetSource(IShopProductSource source)
        {
            // Obtain a complete snapshot before touching the view; malformed data cannot leave half a refresh.
            var products = new IReadOnlyList<ShopProductViewData>[sections.Length];
            for (int i = 0; i < sections.Length; i++)
            {
                products[i] = source.GetProducts(sections[i].Kind);
                if (products[i].Count > sections[i].Slots.Length)
                    throw new InvalidOperationException($"Shop section {sections[i].Kind} has {sections[i].Slots.Length} slots, got {products[i].Count} products.");
            }
            for (int i = 0; i < sections.Length; i++) SetProducts(sections[i].Kind, products[i]);
        }

        public void ScrollToSection(ShopSection kind)
        {
            var section = Array.Find(sections, value => value.Kind == kind);
            if (section == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            float range = scroll.content.rect.height - scroll.viewport.rect.height;
            float top = scroll.content.rect.yMax - scroll.content.InverseTransformPoint(
                section.Root.TransformPoint(new Vector3(0, section.Root.rect.yMax, 0))).y;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = range > 0 ? 1f - Mathf.Clamp01(top / range) : 1f;
        }

        /// <summary>Shows the time left until the daily reset, e.g. "12시간 30분".</summary>
        public void SetDailyReset(TimeSpan left)
        {
            if (dailyResetLabel == null) return;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            string text = left.TotalHours >= 1 ? $"{(int)left.TotalHours}시간 {left.Minutes}분" : $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))}분";
            if (dailyResetLabel.text != text) dailyResetLabel.text = text;
        }

        public void BindAuthoredProducts()
        {
            foreach (var section in sections)
                foreach (var slot in section.Slots)
                    if (slot.product != null) slot.Bind(slot.product, clicked => PurchaseRequested?.Invoke(section.Kind, clicked));
        }

        public void SetProducts(ShopSection kind, IReadOnlyList<ShopProductViewData> products)
        {
            var section = Array.Find(sections, value => value.Kind == kind);
            if (section == null) return;
            if (products.Count > section.Slots.Length) throw new ArgumentOutOfRangeException(nameof(products), "More products than authored slots: " + kind);
            for (int i = 0; i < section.Slots.Length; i++)
            {
                var slot = section.Slots[i]; slot.gameObject.SetActive(i < products.Count);
                if (i < products.Count) slot.Bind(products[i], clicked => PurchaseRequested?.Invoke(kind, clicked));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)scroll.content);
        }
    }
}
