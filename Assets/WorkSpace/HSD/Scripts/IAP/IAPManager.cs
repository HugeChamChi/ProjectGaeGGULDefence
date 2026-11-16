using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

namespace GaeGGUL.IAP
{
    public class IAPManager : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI diamondText;
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private Transform productListContent;
        [SerializeField] private Button productButtonTemplate;
        [SerializeField] private Button restoreButton;

        private StoreController _store;
        private readonly Dictionary<string, int> _diamondPayouts = new();
        private readonly Dictionary<string, PackagePayout> _packagePayouts = new();
        /// <summary>What the store grants for a package (catalog payouts), for display consistency checks.</summary>
        public bool TryGetPackagePayout(string productId, out PackagePayout payout) => _packagePayouts.TryGetValue(productId, out payout);
        private bool _sellsRemoveAds;
        private bool Sells(string productId) => _diamondPayouts.ContainsKey(productId) || _packagePayouts.ContainsKey(productId)
            || (_sellsRemoveAds && RemoveAdsIap.IsRemoveAds(productId));
        private readonly List<Button> _spawnedButtons = new();
        private DiamondPurchaseService _purchases;
        private string _owner;
        private bool _busy;
        private readonly Dictionary<string, PendingOrder> _pending = new();
        private bool _connected;
        public event Action ProductsChanged;
        public string ListingStatus { get; private set; } = "스토어 초기화 대기";
        private string _listingDetails = "IAP 초기화를 기다리고 있습니다.";
        private float _listingStarted;

        private void SetListingStatus(string status, string details)
        {
            ListingStatus = status; _listingDetails = details; _listingStarted = Time.realtimeSinceStartup;
            ProductsChanged?.Invoke();
        }

        public string GetListingDiagnostics()
        {
            var missing = _diamondPayouts.Keys.Where(id =>
            {
                var product = _store?.GetProductById(id);
                return product == null || string.IsNullOrEmpty(product.metadata.localizedPriceString);
            }).ToArray();
            if (_connected && _diamondPayouts.Count > 0 && missing.Length == 0) return "";
            string waiting = ListingStatus == "스토어 연결 중" || ListingStatus == "가격 조회 중"
                ? $" · {(int)(Time.realtimeSinceStartup - _listingStarted)}초 경과" : "";
            return $"Google Play: {ListingStatus}{waiting}\n{_listingDetails}\n패키지: {Application.identifier}" +
                (missing.Length == 0 ? "" : "\n가격 미수신: " + string.Join(", ", missing));
        }

        public string GetMissingPriceLabel(string productId)
        {
            if (!_connected || _store == null || _diamondPayouts.Count == 0) return ListingStatus;
            if (ListingStatus == "가격 조회 중") return ListingStatus;
            return _store.GetProductById(productId) == null ? "상품 정보 없음" : "가격 정보 없음";
        }

        /// <summary>Read-only listing for the shared shop page. Never connects or starts a purchase.</summary>
        public bool TryGetProductListing(string productId, out string price, out bool available)
        {
            price = null; available = false;
            if (!_connected || _store == null || !Sells(productId)) return false;
            var product = _store.GetProductById(productId);
            if (product == null || string.IsNullOrEmpty(product.metadata.localizedPriceString)) return false;
            price = product.metadata.localizedPriceString;
            available = IsCurrentAccount() && product.availableToPurchase && !_busy && _pending.Count == 0;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor only: Unity IAP's fake store (purchase dialog, "$0.01" prices) stands in for Google Play so
        /// package, diamond and remove-ads grants can be checked on the DEV_ test account. Receipts are not verified.
        /// </summary>
        private static bool EditorFakeStore => Application.isEditor;
#else
        private static bool EditorFakeStore => false;
#endif

        private void Awake()
        {
            productButtonTemplate?.gameObject.SetActive(false);
            restoreButton?.onClick.AddListener(OnRestoreClicked);
            if (!EditorFakeStore && (Application.platform != RuntimePlatform.Android || Application.isEditor))
            {
                SetListingStatus("기기 테스트 필요", "에디터 또는 Android 이외 환경에서는 Google Play 연결을 실행하지 않습니다.");
                SetStatus("Google Play 기기 테스트 필요");
                return;
            }
            if (!Player.PlayerData.IsReady) { SetListingStatus("로그인 대기", "계정·재화 초기화가 완료되지 않아 스토어 연결을 시작하지 않았습니다."); SetStatus("로그인과 재화 조회 후 이용 가능"); return; }
            try { InitializeStore(); }
            catch (Exception error)
            {
                SetListingStatus("스토어 초기화 실패", error.GetType().Name + ": " + error.Message);
                Debug.LogWarning("[IAPManager] " + _listingDetails);
            }
        }

        private void InitializeStore()
        {
            _owner = BackEnd.Backend.UserInDate;
            _purchases = EditorFakeStore
                ? new DiamondPurchaseService(Player.PlayerData, (_, _) => UniTask.FromResult(true))
                : new DiamondPurchaseService(Player.PlayerData);

            _store = UnityIAPServices.StoreController();
            _store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(DiamondPurchaseService.AccountHash(_owner));

            _store.OnStoreConnected += OnStoreConnected;
            _store.OnStoreDisconnected += OnStoreDisconnected;
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += OnProductsFetchFailed;
            _store.OnPurchasePending += OnPurchasePending;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _store.OnPurchaseFailed += OnPurchaseFailed;
            _store.OnPurchaseDeferred += OnPurchaseDeferred;
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;

            RefreshDiamondText();
            SetStatus("Connecting...");
            SetListingStatus("스토어 연결 중", "Google Play Billing 연결 응답 대기");
            Log("스토어 연결 시도...");
            ConnectStoreAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>Connects the store; the result arrives through OnStoreConnected/OnStoreDisconnected.</summary>
        private async UniTaskVoid ConnectStoreAsync(CancellationToken token)
        {
            try { await _store.Connect().AsUniTask().AttachExternalCancellation(token); }
            catch (OperationCanceledException) { }
            catch (Exception error) { SetListingStatus("스토어 연결 실패", error.GetType().Name + ": " + error.Message); SetStatus("스토어 연결 실패"); }
        }

        private void OnStoreConnected()
        {
            _connected = true;
            SetStatus("Connected");
            Log("스토어 연결 성공");

            try
            {
                var catalog = ProductCatalog.LoadDefaultCatalog();
                var definitions = DiamondIapCatalog.Build(catalog, GooglePlay.Name, out var amounts);
                _diamondPayouts.Clear();
                foreach (var entry in amounts) _diamondPayouts.Add(entry.Key, entry.Value);
                definitions.AddRange(PackageIapCatalog.Build(catalog, GooglePlay.Name, out var packages));
                _packagePayouts.Clear();
                foreach (var entry in packages) _packagePayouts.Add(entry.Key, entry.Value);
                var removeAds = catalog.allProducts.FirstOrDefault(p => RemoveAdsIap.IsRemoveAds(p.id));
                _sellsRemoveAds = removeAds != null;
                if (_sellsRemoveAds)
                {
                    if (removeAds.type != ProductType.NonConsumable) throw new InvalidOperationException("remove_ads must be non-consumable.");
                    definitions.Add(new ProductDefinition(removeAds.id, removeAds.GetStoreID(GooglePlay.Name) ?? removeAds.id, ProductType.NonConsumable));
                }
                SetListingStatus("가격 조회 중", "연결 성공 · Google Play 상품/현지 가격 응답 대기");
                _store.FetchProducts(definitions);
            }
            catch (Exception error) { SetListingStatus("상품 설정 오류", error.GetType().Name + ": " + error.Message); SetStatus("상품 설정 확인 필요"); Debug.LogWarning(error.Message); }
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            _connected = false;
            SetListingStatus("스토어 연결 실패", $"SDK 오류: {failure.Message}\n재시도 가능: {failure.IsRetryable}");
            RefreshButtons();
            SetStatus("Disconnected");
            Log($"스토어 연결 실패: {failure.Message}");
        }

        private void OnProductsFetched(List<Product> products)
        {
            if (ListingStatus != "상품 조회 실패") SetListingStatus("상품 응답 수신", $"상품 {products.Count}개 응답 수신");
            Log($"상품 {products.Count}개 로드 완료");
            BuildProductButtons(products);
            _store.FetchPurchases();
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            SetListingStatus("상품 조회 실패", "SDK 오류: " + failure.FailureReason + "\n실패 ID: " +
                string.Join(", ", failure.FailedFetchProducts.Select(p => p.id)));
            Log($"상품 로드 실패: {failure.FailureReason}");
        }

        private void BuildProductButtons(List<Product> products)
        {
            if (productButtonTemplate == null || productListContent == null) { RefreshButtons(); return; }
            foreach (var button in _spawnedButtons)
            {
                if (button != null) Destroy(button.gameObject);
            }
            _spawnedButtons.Clear();

            foreach (var product in products)
            {
                var button = Instantiate(productButtonTemplate, productListContent);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = _diamondPayouts.TryGetValue(product.definition.id, out int dia)
                    ? $"다이아 {dia:N0}개 - {product.metadata.localizedPriceString}" : $"{product.definition.id} - {product.metadata.localizedPriceString}";

                string productId = product.definition.id;
                button.onClick.AddListener(() => PurchaseProduct(productId));

                _spawnedButtons.Add(button);
            }
            RefreshButtons();
        }

        public void PurchaseProduct(string productId)
        {
            if (!_connected || _store == null || !IsCurrentAccount() || _busy || _pending.Count > 0 || !Sells(productId)) return;
            var product = _store.GetProductById(productId);
            if (product == null || !product.availableToPurchase)
            {
                Log($"구매 실패: 상품을 찾을 수 없음 ({productId})");
                return;
            }

            Log($"구매 시작: {productId}");
            _busy = true; RefreshButtons();
            try { _store.PurchaseProduct(product); }
            catch (Exception) { _busy = false; RefreshButtons(); SetStatus("구매 요청 실패"); }
        }

        private void OnPurchasePending(PendingOrder order)
        {
            HandlePendingAsync(order).Forget();
        }

        private async UniTaskVoid HandlePendingAsync(PendingOrder order)
        {
            while (_busyProcessing && IsCurrentAccount()) await UniTask.Yield();
            if (!IsCurrentAccount()) return;
            _busyProcessing = true; _busy = true; RefreshButtons();
            try
            {
                var items = order?.CartOrdered?.Items().ToArray();
                if (items == null || items.Length != 1 || items[0].Quantity != 1) throw new InvalidOperationException("Unexpected purchase cart.");
                var product = items[0].Product;
                bool isPackage = _packagePayouts.TryGetValue(product.definition.id, out var package);
                bool isRemoveAds = _sellsRemoveAds && RemoveAdsIap.IsRemoveAds(product.definition.id);
                if (!isPackage && !isRemoveAds && !_diamondPayouts.ContainsKey(product.definition.id)) throw new InvalidOperationException("Unknown store product.");
                string id = EditorFakeStore
                    ? "Editor:" + order.Info.TransactionID
                    : DiamondPurchaseService.GoogleReceiptId(order.Info.Receipt, product.definition.storeSpecificId,
                        order.Info.TransactionID, DiamondPurchaseService.AccountHash(_owner), Application.identifier);
                _pending[id] = order;
                // A paid order is always granted, even past a shop display limit; limits are enforced before purchase.
                bool granted = isRemoveAds
                    ? await _purchases.FulfillRemoveAdsAsync(id, order.Info.Receipt)
                    : isPackage
                    ? await _purchases.FulfillPackageAsync(id, product.definition.id, package, PackagePeriodKey(), order.Info.Receipt)
                    : await _purchases.FulfillAsync(id, product.definition.id, _diamondPayouts[product.definition.id], order.Info.Receipt);
                if (!granted || !IsCurrentAccount())
                { SetStatus("지급 확인 대기 · 구매 복구로 재시도"); return; }
                RefreshDiamondText();
                _store.ConfirmPurchase(order);
                SetStatus(isRemoveAds ? "광고 제거 저장 완료" : isPackage ? "패키지 지급 저장 완료" : "다이아 지급 저장 완료");
            }
            catch (Exception) { SetStatus("결제 확인 대기 · 자동 지급하지 않았습니다"); }
            finally { _busyProcessing = false; _busy = false; RefreshButtons(); }
        }
        private bool _busyProcessing;

        /// <summary>Weekly period key stamped on package receipts ("주마다 1회").</summary>
        public static string PackagePeriodKey() => GaeGGUL.Shop.DailyShopManager.GetPeriodKey(ServerClock.UtcNow, weekly: true);

        private void OnPurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case ConfirmedOrder confirmed:
                    foreach (var key in _pending.Where(pair => pair.Value.Info.TransactionID == confirmed.Info.TransactionID).Select(pair => pair.Key).ToArray()) _pending.Remove(key);
                    string id = confirmed.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
                    Log($"구매 확정: {id}");
                    break;
                case FailedOrder failed:
                    Log($"구매 확정 실패: {failed.FailureReason} - {failed.Details}");
                    break;
            }
            RefreshButtons();
        }

        private void OnPurchaseFailed(FailedOrder failedOrder)
        {
            _busy = false; RefreshButtons();
            SetStatus("구매 취소 또는 실패");
        }

        private void OnPurchaseDeferred(DeferredOrder deferredOrder)
        {
            Log("구매 보류됨 (승인 대기 중, 예: Ask-to-Buy)");
            _busy = false; RefreshButtons();
        }

        private void OnRestoreClicked()
        {
            if (_store == null || !IsCurrentAccount() || _busyProcessing) return;
            Log("구매 복원 요청...");
            _store.RestoreTransactions((success, error) =>
            {
                Log(success ? "구매 복원 완료" : $"구매 복원 실패: {error}");
                if (success) _store.FetchPurchases();
            });
        }

        private void OnPurchasesFetched(Orders orders)
        {
            Log($"기존 구매 {orders.PendingOrders.Count}건 확인");
            foreach (var order in orders.PendingOrders) HandlePendingAsync(order).Forget();
        }

        private bool IsCurrentAccount() => this != null && !string.IsNullOrEmpty(_owner)
            && _owner == BackEnd.Backend.UserInDate && Player.PlayerData.IsReady;

        private void RefreshButtons()
        {
            foreach (var button in _spawnedButtons) if (button != null) button.interactable = _connected && IsCurrentAccount() && !_busy && _pending.Count == 0;
            ProductsChanged?.Invoke();
        }

        private void OnDestroy()
        {
            restoreButton?.onClick.RemoveListener(OnRestoreClicked);
            if (_store == null) return;
            _store.OnStoreConnected -= OnStoreConnected;
            _store.OnStoreDisconnected -= OnStoreDisconnected;
            _store.OnProductsFetched -= OnProductsFetched;
            _store.OnProductsFetchFailed -= OnProductsFetchFailed;
            _store.OnPurchasePending -= OnPurchasePending;
            _store.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            _store.OnPurchaseFailed -= OnPurchaseFailed;
            _store.OnPurchaseDeferred -= OnPurchaseDeferred;
            _store.OnPurchasesFetched -= OnPurchasesFetched;
            _store.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            Log($"구매 내역 조회 실패: {failure.Message}");
        }

        private void RefreshDiamondText()
        {
            if (diamondText != null) diamondText.text = $"Diamond: {Player.PlayerData.Data.Diamond}";
        }

        private void SetStatus(string status)
        {
            if (statusText != null) statusText.text = $"Store: {status}";
        }

        private void Log(string message)
        {
            Debug.Log($"[IAPManager] {message}");
            if (logText != null) logText.text += message + "\n";
        }
    }
}
