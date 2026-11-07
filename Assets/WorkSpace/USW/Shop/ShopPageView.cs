using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Shop.UI
{
    public enum ShopSection { Daily, Weekly, Gold, Diamond }

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
        public event Action<ShopSection, ShopProductSlot> PurchaseRequested;

        private void OnEnable() => BindAuthoredProducts();

        public void SetSource(IShopProductSource source)
        {
            // Obtain a complete snapshot before touching the view; malformed data cannot leave half a refresh.
            var products = new IReadOnlyList<ShopProductViewData>[sections.Length];
            for (int i = 0; i < sections.Length; i++)
            {
                products[i] = source.GetProducts(sections[i].Kind);
                if (products[i].Count > 6) throw new InvalidOperationException("A shop section supports at most six products.");
            }
            for (int i = 0; i < sections.Length; i++) SetProducts(sections[i].Kind, products[i]);
        }

        public void ScrollToSection(ShopSection kind)
        {
            var section = Array.Find(sections, value => value.Kind == kind);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            float range = scroll.content.rect.height - scroll.viewport.rect.height;
            float top = scroll.content.rect.yMax - scroll.content.InverseTransformPoint(
                section.Root.TransformPoint(new Vector3(0, section.Root.rect.yMax, 0))).y;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = range > 0 ? 1f - Mathf.Clamp01(top / range) : 1f;
        }

        public void BindAuthoredProducts()
        {
            foreach (var section in sections)
                foreach (var slot in section.Slots)
                    if (slot.product != null) slot.Bind(slot.product, clicked => PurchaseRequested?.Invoke(section.Kind, clicked));
        }

        public void SetProducts(ShopSection kind, IReadOnlyList<ShopProductViewData> products)
        {
            if (products.Count > 6) throw new ArgumentOutOfRangeException(nameof(products), "A shop section supports at most six products.");
            var section = Array.Find(sections, value => value.Kind == kind);
            for (int i = 0; i < section.Slots.Length; i++)
            {
                var slot = section.Slots[i]; slot.gameObject.SetActive(i < products.Count);
                if (i < products.Count) slot.Bind(products[i], clicked => PurchaseRequested?.Invoke(kind, clicked));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)scroll.content);
        }
    }
}
