using System;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
using GaeGGUL.IAP;
using TMPro;
using UnityEngine;

namespace GaeGGUL.Shop.UI
{
    /// <summary>Lobby shop: binds chart/account products to the authored page and runs purchases.</summary>
    public sealed class AccountShopHost : MonoBehaviour
    {
        public ShopPageView page;
        public ShopCatalogSettings settings;
        public TMP_Text feedback;
        [Tooltip("Optional backdrop shown while a feedback message is visible.")]
        public GameObject feedbackRoot;
        [Tooltip("Seconds a feedback message stays visible.")]
        public float feedbackSeconds = 2.5f;
        private float _feedbackUntil;
        [Tooltip("Optional store status text. Shown only in non-editor debug builds while store prices are missing.")]
        public TMP_Text storeDiagnostics;
        private IAPManager _iap;
        private bool _buying;
        private bool _purchaseUncertain;
        private CancellationTokenSource _visible;
        private float _nextStockRetry;

        private void OnEnable()
        {
            page.PurchaseRequested += Purchase;
            page.RemoveAdsRequested += BuyRemoveAds;
            if (_iap != null) _iap.ProductsChanged += Refresh;
            Say("", 0f);
            _visible = new CancellationTokenSource();
            RefreshLoop(_visible.Token).Forget();
        }
        private void OnDisable()
        {
            page.PurchaseRequested -= Purchase;
            page.RemoveAdsRequested -= BuyRemoveAds;
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
                            (Time.realtimeSinceStartup >= _nextStockRetry || Player.Shop.Daily.IsReady))
                        {
                            await Player.Shop.Daily.RefreshIfNeededAsync();
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
            if (ServerClock.IsSynced) page.SetDailyReset(TimeUntilDailyReset(ServerClock.UtcNow));
            RefreshStoreDiagnostics();
            RefreshRemoveAds();
            if (!_buying && _feedbackUntil > 0f && Time.realtimeSinceStartup >= _feedbackUntil) Say("", 0f);
            if (_buying || _purchaseUncertain)
                foreach (var section in page.sections)
                    foreach (var slot in section.Slots) slot.buyButton.interactable = false;
        }

        private void RefreshRemoveAds()
        {
            if (Player.PlayerData.IsReady && Player.PlayerData.HasRemoveAds) { page.SetRemoveAds("광고 제거됨", false); return; }
            if (_iap != null && _iap.TryGetProductListing(RemoveAdsIap.ProductId, out string price, out bool available))
                page.SetRemoveAds("광고 제거 " + price, available && !_buying && !_purchaseUncertain);
            else page.SetRemoveAds("광고 제거", true); // Not listed yet: a tap explains it is not ready.
        }

        private void BuyRemoveAds()
        {
            if (!Player.PlayerData.IsReady || Player.PlayerData.HasRemoveAds || _buying || _purchaseUncertain) return;
            if (_iap == null || !_iap.TryGetProductListing(RemoveAdsIap.ProductId, out _, out bool available) || !available) { Say("준비 중이에요"); return; }
            _iap.PurchaseProduct(RemoveAdsIap.ProductId);
        }

        /// <summary>Daily stock resets at 05:00 KST (DailyShopManager.GetPeriodKey).</summary>
        public static TimeSpan TimeUntilDailyReset(DateTime utc)
        {
            var local = utc.AddHours(9 - 5);
            return local.Date.AddDays(1) - local;
        }

        private void RefreshStoreDiagnostics()
        {
            if (storeDiagnostics == null) return;
            string details = Debug.isDebugBuild && !Application.isEditor && _iap != null ? _iap.GetListingDiagnostics() : "";
            storeDiagnostics.gameObject.SetActive(!string.IsNullOrEmpty(details));
            if (storeDiagnostics.text != details) storeDiagnostics.text = details;
        }

        private void Say(string text) => Say(text, feedbackSeconds);
        private void Say(string text, float seconds)
        {
            if (this == null) return;
            if (feedback != null) feedback.text = text;
            if (feedbackRoot != null) feedbackRoot.SetActive(!string.IsNullOrEmpty(text));
            _feedbackUntil = string.IsNullOrEmpty(text) ? 0f : Time.realtimeSinceStartup + seconds;
        }

        private void Purchase(ShopSection section, ShopProductSlot slot) => PurchaseAsync(section, slot.product).Forget();
        private async UniTaskVoid PurchaseAsync(ShopSection section, ShopProductViewData product)
        {
            if (product.ComingSoon) { Say("준비 중이에요"); return; }
            if (product.Locked) { Say((product.CostCurrency == CurrencyType.Gold ? "골드" : "다이아") + "가 부족해요"); return; }
            if (_buying || _purchaseUncertain || !Player.PlayerData.IsReady) return;
            if (section == ShopSection.Diamond || section == ShopSection.Package)
            {
                _iap?.PurchaseProduct(product.StoreId);
                return;
            }
            _buying = true; Refresh();
            Say("구매 확인 중…");
            try
            {
                bool success;
                if (section == ShopSection.Gold || section == ShopSection.Essence)
                {
                    var category = section == ShopSection.Gold ? ShopCategory.Gold : ShopCategory.Essence;
                    var item = Table.Shop.Catalog.Products(category).FirstOrDefault(p => p.Item.ShopID == product.ShopId);
                    success = item != null && Player.PlayerData.TryPurchaseShopItem(item.Item, "exchange", repeatable: true);
                    if (success)
                    {
                        success = await Player.PlayerData.TrySaveAsync();
                        _purchaseUncertain = !success;
                    }
                }
                else if (section == ShopSection.Daily && product.CostCurrency == CurrencyType.Ad)
                {
                    // Remove-ads owners get the reward without watching; the daily limit still applies.
                    if (!Player.PlayerData.HasRemoveAds)
                    {
                        Say("광고 불러오는 중…");
                        // The reward is granted only after the SDK reports an earned reward.
                        bool watched = await ShopAds.ShowAsync(settings.RewardedAdUnitIdAndroid, this.GetCancellationTokenOnDestroy());
                        if (!watched) { Say("광고를 끝까지 보면 받을 수 있어요"); return; }
                    }
                    success = await Player.Shop.Daily.BuyItem(product.ShopId);
                }
                else if (section == ShopSection.Daily) success = await Player.Shop.Daily.BuyItem(product.ShopId);
                else success = false;
                bool uncertain = _purchaseUncertain || section == ShopSection.Daily && !Player.Shop.Daily.IsReady;
                Say(success ? "구매 완료" : uncertain ? "구매 결과를 확인해야 합니다. 재접속해 주세요." : "구매하지 못했어요. 잔액을 확인해 주세요.");
            }
            catch (Exception error)
            {
                Debug.LogWarning("Shop purchase failed: " + error.GetType().Name);
                Say("구매 결과를 확인해야 합니다. 재접속해 주세요.");
            }
            finally { _buying = false; if (this != null) Refresh(); }
        }
    }
}
