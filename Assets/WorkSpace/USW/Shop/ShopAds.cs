using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using UnityEngine;

namespace GaeGGUL.Shop
{
    /// <summary>
    /// AdMob rewarded ads for shop products. The reward is granted by the caller only after the SDK
    /// reports an earned reward and the ad closes. Server-side verification (SSV) is not connected yet
    /// (design/gdd/rewarded-ads-architecture.md); until then the client grant is trusted.
    /// In the Editor the SDK shows its placeholder ad.
    /// </summary>
    public static class ShopAds
    {
        private const int LoadTimeoutSeconds = 15;

        /// <summary>Google's public rewarded test unit (Android).</summary>
        public const string TestRewardedUnitAndroid = "ca-app-pub-3940256099942544/5224354917";

        /// <summary>
        /// Editor, Development Build and INTERNAL_TEST_BUILD always load Google's test unit (same split as
        /// ChartEnvironment) so testing never requests or clicks real ads; release builds use the given unit.
        /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD || INTERNAL_TEST_BUILD
        public static string ResolveUnitId(string adUnitId) => TestRewardedUnitAndroid;
#else
        public static string ResolveUnitId(string adUnitId) => adUnitId;
#endif
        private static UniTaskCompletionSource _initialized;
        private static bool _showing;

        /// <summary>True on platforms where the AdMob SDK can show a rewarded ad.</summary>
        public static bool IsAvailable => Application.isEditor || Application.platform == RuntimePlatform.Android;

        private static UniTask InitializeAsync()
        {
            if (_initialized != null) return _initialized.Task;
            _initialized = new UniTaskCompletionSource();
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(_ => _initialized.TrySetResult());
            return _initialized.Task;
        }

        /// <summary>Loads and shows one rewarded ad; true only when the reward was earned.</summary>
        public static async UniTask<bool> ShowAsync(string adUnitId, CancellationToken token)
        {
            if (!IsAvailable || string.IsNullOrEmpty(adUnitId) || _showing) return false;
            adUnitId = ResolveUnitId(adUnitId);
            _showing = true;
            RewardedAd ad = null;
            try
            {
                await InitializeAsync().AttachExternalCancellation(token);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                // 타이머를 CTS보다 먼저 정리한다. 남겨 두면 제한 시간 뒤 이미 정리된 CTS를 취소하려다 ObjectDisposedException이 난다.
                using (timeout.CancelAfterSlim(TimeSpan.FromSeconds(LoadTimeoutSeconds), DelayType.Realtime))
                {
                    var loaded = new UniTaskCompletionSource<RewardedAd>();
                    RewardedAd.Load(adUnitId, new AdRequest(), (result, error) =>
                    {
                        if (error != null) Debug.LogWarning("[ShopAds] Load failed: " + error.GetMessage());
                        loaded.TrySetResult(error == null ? result : null);
                    });
                    ad = await loaded.Task.AttachExternalCancellation(timeout.Token);
                }
                if (ad == null || !ad.CanShowAd()) return false;

                bool earned = false;
                var closed = new UniTaskCompletionSource();
                ad.OnAdFullScreenContentClosed += () => closed.TrySetResult();
                ad.OnAdFullScreenContentFailed += error => { Debug.LogWarning("[ShopAds] Show failed: " + error.GetMessage()); closed.TrySetResult(); };
                ad.Show(_ => earned = true);
                await closed.Task.AttachExternalCancellation(token);
                return earned;
            }
            catch (OperationCanceledException) { return false; }
            finally
            {
                ad?.Destroy();
                _showing = false;
            }
        }
    }
}
