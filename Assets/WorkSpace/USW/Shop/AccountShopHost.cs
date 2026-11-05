using System;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
using GaeGGUL.IAP;
using TMPro;
using UnityEngine;

namespace GaeGGUL.Shop.UI
{
    public sealed class AccountShopHost : MonoBehaviour
    {
        public ShopPageView page;
        public ShopCatalogSettings settings;
        public TMP_Text feedback;
        private IAPManager _iap;
        private bool _buying;
        private bool _purchaseUncertain;
        private CancellationTokenSource _visible;
        private float _nextStockRetry;
        private TMP_Text _storeDiagnostics;

        private void OnEnable()
        {
            page.PurchaseRequested += Purchase;
            if (_iap != null) _iap.ProductsChanged += Refresh;
            _visible = new CancellationTokenSource();
            RefreshLoop(_visible.Token).Forget();
        }
        private void OnDisable()
        {
            page.PurchaseRequested -= Purchase;
            _visible?.Cancel(); _visible?.Dispose(); _visible = null;
            if (_iap != null) _iap.ProductsChanged -= Refresh;
        }
        private async UniTaskVoid RefreshLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (Player.PlayerData.IsReady)
                    {
                        if (_iap == null)
                        {
                            _iap = GetComponent<IAPManager>();
                            if (_iap == null) _iap = gameObject.AddComponent<IAPManager>();
                            _iap.ProductsChanged += Refresh;
                        }
                        if (!_buying && Player.Shop.IsInitialized &&
                            (Time.realtimeSinceStartup >= _nextStockRetry || Player.Shop.Daily.IsReady && Player.Shop.Weekly.IsReady))
                        {
                            await Player.Shop.Daily.RefreshIfNeededAsync();
                            if (token.IsCancellationRequested) return;
                            await Player.Shop.Weekly.RefreshIfNeededAsync();
                            _nextStockRetry = Time.realtimeSinceStartup + 30f;
                        }
                    }
                    if (token.IsCancellationRequested) return;
                    Refresh();
                    await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);
                }
            }
            catch (OperationCanceledException) { }
        }
        public void Refresh()
        {
            if (this == null || !isActiveAndEnabled) return;
            page.SetSource(new AccountShopProductSource(settings, _iap));
            RefreshStoreDiagnostics();
            if (_buying || _purchaseUncertain)
                foreach (var section in page.sections)
                    foreach (var slot in section.Slots) slot.buyButton.interactable = false;
        }
        private void RefreshStoreDiagnostics()
        {
            var section = Array.Find(page.sections, s => s.Kind == ShopSection.Diamond);
            if (_storeDiagnostics == null)
            {
                var go = new GameObject("StoreDiagnostics", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement), typeof(TextMeshProUGUI));
                go.transform.SetParent(section.Root, false); go.transform.SetSiblingIndex(1);
                _storeDiagnostics = go.GetComponent<TMP_Text>();
                _storeDiagnostics.font = section.Slots[0].price.font;
                _storeDiagnostics.fontSize = 23; _storeDiagnostics.color = new Color(1f, .82f, .45f);
                _storeDiagnostics.richText = false; _storeDiagnostics.raycastTarget = false;
                _storeDiagnostics.textWrappingMode = TextWrappingModes.Normal;
            }
            string details = _iap != null ? _iap.GetListingDiagnostics() : "Google Play: 스토어 초기화 대기\n계정·재화 데이터 로드를 기다리고 있습니다.";
            _storeDiagnostics.gameObject.SetActive(!string.IsNullOrEmpty(details));
            if (_storeDiagnostics.text == details) return;
            _storeDiagnostics.text = details;
            float width = Mathf.Max(300, section.Root.rect.width - 24);
            _storeDiagnostics.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = _storeDiagnostics.GetPreferredValues(details, width, 0).y + 24;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(page.scroll.content);
        }
        private void Purchase(ShopSection section, ShopProductSlot slot) => PurchaseAsync(section, slot.product).Forget();
        private async UniTaskVoid PurchaseAsync(ShopSection section, ShopProductViewData product)
        {
            if (_buying || _purchaseUncertain || !Player.PlayerData.IsReady) return;
            if (section == ShopSection.Diamond)
            {
                _iap?.PurchaseProduct(product.StoreId);
                return;
            }
            _buying = true; Refresh();
            if (feedback != null) feedback.text = "구매 확인 중…";
            try
            {
                bool success;
                if (section == ShopSection.Gold)
                {
                    var item = Table.Shop.Catalog.Products(ShopCategory.Gold).FirstOrDefault(p => p.Item.ShopID == product.ShopId);
                    success = item != null && Player.PlayerData.TryPurchaseShopItem(item.Item, "exchange", repeatable: true);
                    if (success)
                    {
                        success = await Player.PlayerData.TrySaveAsync();
                        _purchaseUncertain = !success;
                    }
                }
                else success = await (section == ShopSection.Daily ? Player.Shop.Daily : Player.Shop.Weekly).BuyItem(product.ShopId);
                bool uncertain = _purchaseUncertain ||
                    section == ShopSection.Daily && !Player.Shop.Daily.IsReady ||
                    section == ShopSection.Weekly && !Player.Shop.Weekly.IsReady;
                if (this != null && feedback != null) feedback.text = success ? "구매 완료" : uncertain
                    ? "구매 결과를 확인해야 합니다. 재접속해 주세요."
                    : "구매하지 못했어요. 잔액을 확인해 주세요.";
            }
            catch (Exception error)
            {
                Debug.LogWarning("Shop purchase failed: " + error.GetType().Name);
                if (this != null && feedback != null) feedback.text = "구매 결과를 확인해야 합니다. 재접속해 주세요.";
            }
            finally { _buying = false; if (this != null) Refresh(); }
        }
    }
}
