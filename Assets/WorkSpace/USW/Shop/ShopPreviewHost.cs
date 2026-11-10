using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Shop.UI
{
    /// <summary>Test scene only: demonstrates local sold-out feedback without account or store access.</summary>
    public sealed class ShopPreviewHost : MonoBehaviour
    {
        public ShopPageView page;
        public TMP_Text feedback;
        public Button resetButton;
        public ShopCatalogSettings catalogSettings;
        public ShopPreviewCatalog previewCatalog;
        [Tooltip("Optional local chart rows exported by the planner. This scene never contacts the server.")]
        public TextAsset chartPreview;
        private IShopProductSource _source;
        private void LoadSource()
        {
            _source = chartPreview != null
                ? new ChartPreviewShopProductSource(ShopCatalogTable.Parse(LitJson.JsonMapper.ToObject(chartPreview.text)), catalogSettings)
                : new PreviewShopProductSource(catalogSettings, previewCatalog);
        }
        private void OnEnable()
        {
            LoadSource();
            page.SetSource(_source);
            page.PurchaseRequested += PreviewPurchase;
            resetButton.onClick.AddListener(ResetPreview);
        }
        private void OnDisable()
        {
            page.PurchaseRequested -= PreviewPurchase;
            resetButton.onClick.RemoveListener(ResetPreview);
        }
        private void PreviewPurchase(ShopSection section, ShopProductSlot slot)
        {
            if (section == ShopSection.Daily || section == ShopSection.Weekly)
            {
                slot.SetSoldOut(true);
                feedback.text = "미리보기에서 수령했어요";
            }
            else feedback.text = "다이아 상품 미리보기";
        }
        public void ResetPreview()
        {
            LoadSource(); page.SetSource(_source);
            feedback.text = "미리보기 · 실제 재화는 사용되지 않아요";
            page.scroll.verticalNormalizedPosition = 1;
        }
    }
}
