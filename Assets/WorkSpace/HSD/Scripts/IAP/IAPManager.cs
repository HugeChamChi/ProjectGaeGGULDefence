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
        private readonly List<Button> _spawnedButtons = new();
        private DiamondPurchaseService _purchases;
        private string _owner;
        private bool _busy;
        private readonly Dictionary<string, PendingOrder> _pending = new();

        private void Awake()
        {
            productButtonTemplate?.gameObject.SetActive(false);
            restoreButton?.onClick.AddListener(OnRestoreClicked);
            if (Application.platform != RuntimePlatform.Android || Application.isEditor)
            {
                SetStatus("Google Play 기기 테스트 필요");
                return;
            }
            if (!Player.PlayerData.IsReady) { SetStatus("로그인과 재화 조회 후 이용 가능"); return; }
            _owner = BackEnd.Backend.UserInDate;
            _purchases = new DiamondPurchaseService(Player.PlayerData);

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
            Log("스토어 연결 시도...");
            ConnectStoreAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>Connects the store; the result arrives through OnStoreConnected/OnStoreDisconnected.</summary>
        private async UniTaskVoid ConnectStoreAsync(CancellationToken token)
        {
            try { await _store.Connect().AsUniTask().AttachExternalCancellation(token); }
            catch (OperationCanceledException) { }
            catch (Exception) { SetStatus("스토어 연결 실패"); }
        }

        private void OnStoreConnected()
        {
            SetStatus("Connected");
            Log("스토어 연결 성공");

            try
            {
                var definitions = DiamondIapCatalog.Build(ProductCatalog.LoadDefaultCatalog(), GooglePlay.Name, out var amounts);
                _diamondPayouts.Clear();
                foreach (var entry in amounts) _diamondPayouts.Add(entry.Key, entry.Value);
                _store.FetchProducts(definitions);
            }
            catch (Exception error) { SetStatus("상품 설정 확인 필요"); Debug.LogWarning(error.Message); }
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            SetStatus("Disconnected");
            Log($"스토어 연결 실패: {failure.Message}");
        }

        private void OnProductsFetched(List<Product> products)
        {
            Log($"상품 {products.Count}개 로드 완료");
            BuildProductButtons(products);
            _store.FetchPurchases();
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            Log($"상품 로드 실패: {failure.FailureReason}");
        }

        private void BuildProductButtons(List<Product> products)
        {
            if (productButtonTemplate == null || productListContent == null) return;
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
                if (label != null) label.text = $"다이아 {_diamondPayouts[product.definition.id]:N0}개 - {product.metadata.localizedPriceString}";

                string productId = product.definition.id;
                button.onClick.AddListener(() => PurchaseProduct(productId));

                _spawnedButtons.Add(button);
            }
            RefreshButtons();
        }

        public void PurchaseProduct(string productId)
        {
            if (_store == null || !IsCurrentAccount() || _busy || _pending.Count > 0 || !_diamondPayouts.ContainsKey(productId)) return;
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
                if (!_diamondPayouts.TryGetValue(product.definition.id, out int amount)) throw new InvalidOperationException("Unknown diamond product.");
                string id = DiamondPurchaseService.GoogleReceiptId(order.Info.Receipt, product.definition.storeSpecificId,
                    order.Info.TransactionID, DiamondPurchaseService.AccountHash(_owner), Application.identifier);
                _pending[id] = order;
                if (!await _purchases.FulfillAsync(id, product.definition.id, amount, order.Info.Receipt) || !IsCurrentAccount())
                { SetStatus("지급 확인 대기 · 구매 복구로 재시도"); return; }
                RefreshDiamondText();
                _store.ConfirmPurchase(order);
                SetStatus("다이아 지급 저장 완료");
            }
            catch (Exception) { SetStatus("결제 확인 대기 · 자동 지급하지 않았습니다"); }
            finally { _busyProcessing = false; _busy = false; RefreshButtons(); }
        }
        private bool _busyProcessing;

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
            foreach (var button in _spawnedButtons) if (button != null) button.interactable = IsCurrentAccount() && !_busy && _pending.Count == 0;
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
