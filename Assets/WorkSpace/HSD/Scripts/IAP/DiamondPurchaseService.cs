using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GaeGGUL.IAP
{
    public sealed class DiamondPurchaseService
    {
        private readonly PlayerDataController _balances;
        private readonly Func<string, string, UniTask<bool>> _verify;
        private bool _busy;
        public DiamondPurchaseService(PlayerDataController balances, Func<string, string, UniTask<bool>> verify = null)
        { _balances = balances; _verify = verify ?? VerifyGoogleAsync; }

        public static string AccountHash(string account)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(account))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Check the store, product, purchase state and purchasing game account before verification.</summary>
        public static string GoogleReceiptId(string receipt, string storeProductId, string transactionId, string accountHash, string packageName)
        {
            var unified = JObject.Parse(receipt);
            if ((string)unified["Store"] != "GooglePlay" || string.IsNullOrEmpty(transactionId)
                || (string)unified["TransactionID"] != transactionId) throw new FormatException("Unexpected store transaction.");
            var payload = JObject.Parse((string)unified["Payload"] ?? "");
            var purchase = JObject.Parse((string)payload["json"] ?? "");
            string token = (string)purchase["purchaseToken"];
            if (string.IsNullOrWhiteSpace(token) || (string)purchase["productId"] != storeProductId
                || (string)purchase["packageName"] != packageName
                || (transactionId != token && (string)purchase["orderId"] != transactionId)
                || (int?)purchase["purchaseState"] != 0 || ((int?)purchase["quantity"] ?? 1) != 1
                || string.IsNullOrEmpty(accountHash)
                || (string)(purchase["obfuscatedExternalAccountId"] ?? purchase["obfuscatedAccountId"]) != accountHash)
                throw new FormatException("Receipt does not match this product or account.");
            return "GooglePlay:" + AccountHash(token);
        }

        /// <summary>Confirm the store order only when true is returned. Failed writes retain the durable local grant.</summary>
        public async UniTask<bool> FulfillAsync(string id, string productId, int amount, string receipt)
        {
            if (_busy || !_balances.IsReady || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(productId) || amount <= 0) return false;
            _busy = true;
            var snapshot = _balances.Data;
            try
            {
                var previous = snapshot.DiamondPurchases?.Find(value => value.Id == id);
                if (previous != null)
                {
                    if (previous.ProductId != productId) return false;
                }
                else
                {
                    if (snapshot.Diamond < 0 || snapshot.Diamond > int.MaxValue - amount) return false;
                    if (!await _verify(receipt, productId) || !_balances.IsReady || !ReferenceEquals(snapshot, _balances.Data)) return false;
                    if (!_balances.TryApplyDiamondPurchase(new DiamondPurchaseReceipt { Id = id, ProductId = productId, Diamond = amount })) return false;
                }
                bool saved = await _balances.TrySaveAsync();
                // A save started before our mutation may finish first. Require our revision to be clean as well.
                if (saved && _balances.IsDirty) saved = await _balances.TrySaveAsync();
                return saved && !_balances.IsDirty && _balances.IsReady && ReferenceEquals(snapshot, _balances.Data);
            }
            finally { _busy = false; }
        }

        private static async UniTask<bool> VerifyGoogleAsync(string receipt, string productId)
        {
            if (!Backend.IsLogin) return false;
            string owner = Backend.UserInDate;
            var done = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.Receipt.IsValidateGooglePurchase(receipt, "Diamond:" + productId, false, response => done.TrySetResult(response));
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
            var result = await done.Task.AttachExternalCancellation(timeout.Token);
            // UsedReceipt without a matching durable grant is ambiguous; never manufacture a reward or confirm it.
            return owner == Backend.UserInDate && result.IsSuccess();
        }
    }
}
