using System.Collections.Generic;
using System.Linq;
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

        private async void Awake()
        {
            productButtonTemplate.gameObject.SetActive(false);
            restoreButton.onClick.AddListener(OnRestoreClicked);

            // 테스트 씬은 백엔드 로그인 없이 단독으로 실행되므로, 로컬 더미 PlayerData로 지급을 확인한다.
            if (Player.PlayerData.Data == null)
            {
                Player.PlayerData.RefreshUI(new PlayerData());
            }

            _store = UnityIAPServices.StoreController();

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
            await _store.Connect();
        }

        private void OnStoreConnected()
        {
            SetStatus("Connected");
            Log("스토어 연결 성공");

            var catalog = ProductCatalog.LoadDefaultCatalog();
            var definitions = new List<ProductDefinition>();
            _diamondPayouts.Clear();

            foreach (var item in catalog.allValidProducts)
            {
                definitions.Add(new ProductDefinition(item.id, item.type));

                var payout = item.Payouts.FirstOrDefault(p =>
                    p.type == ProductCatalogPayout.ProductCatalogPayoutType.Currency &&
                    p.subtype == "Diamond");
                if (payout != null)
                {
                    _diamondPayouts[item.id] = (int)payout.quantity;
                }
            }

            _store.FetchProducts(definitions);
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
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            Log($"상품 로드 실패: {failure.FailureReason}");
        }

        private void BuildProductButtons(List<Product> products)
        {
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
                string title = string.IsNullOrEmpty(product.metadata.localizedTitle)
                    ? product.definition.id
                    : product.metadata.localizedTitle;
                if (label != null) label.text = $"{title} - {product.metadata.localizedPriceString}";

                string productId = product.definition.id;
                button.onClick.AddListener(() => PurchaseProduct(productId));

                _spawnedButtons.Add(button);
            }
        }

        private void PurchaseProduct(string productId)
        {
            var product = _store.GetProductById(productId);
            if (product == null)
            {
                Log($"구매 실패: 상품을 찾을 수 없음 ({productId})");
                return;
            }

            Log($"구매 시작: {productId}");
            _store.PurchaseProduct(product);
        }

        private void OnPurchasePending(PendingOrder order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            if (product == null)
            {
                Log("구매 보류: 상품 정보 없음");
                _store.ConfirmPurchase(order);
                return;
            }

            string productId = product.definition.id;
            if (_diamondPayouts.TryGetValue(productId, out int amount))
            {
                Player.PlayerData.AddDiamond(amount);
                RefreshDiamondText();
                Log($"구매 보류 처리 → 다이아 {amount}개 지급: {productId}");
            }
            else
            {
                Log($"구매 보류: 지급 정보 없음 ({productId})");
            }

            _store.ConfirmPurchase(order);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case ConfirmedOrder confirmed:
                    string id = confirmed.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
                    Log($"구매 확정: {id}");
                    break;
                case FailedOrder failed:
                    Log($"구매 확정 실패: {failed.FailureReason} - {failed.Details}");
                    break;
            }
        }

        private void OnPurchaseFailed(FailedOrder failedOrder)
        {
            Log($"구매 실패: {failedOrder.FailureReason} - {failedOrder.Details}");
        }

        private void OnPurchaseDeferred(DeferredOrder deferredOrder)
        {
            Log("구매 보류됨 (승인 대기 중, 예: Ask-to-Buy)");
        }

        private void OnRestoreClicked()
        {
            Log("구매 복원 요청...");
            _store.RestoreTransactions((success, error) =>
            {
                Log(success ? "구매 복원 완료" : $"구매 복원 실패: {error}");
            });
        }

        private void OnPurchasesFetched(Orders orders)
        {
            Log($"기존 구매 {orders.PendingOrders.Count}건 확인");
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
