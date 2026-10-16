using System;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>In-app Google account selection using Android Credential Manager; separate from Play Games identity.</summary>
public class GoogleIdentityAuth
{
    private const string BridgeClass = "com.gaeggul.identity.GoogleIdentityBridge";
    private const int AuthenticationTimeoutSeconds = 120;
    private const int NonceBytes = 32;
    /// <summary>Whether native Google identity can be used in this build.</summary>
    public virtual bool IsAvailable => !Application.isEditor && Application.platform == RuntimePlatform.Android && GoogleAccountAuth.IsConfigured;

    /// <summary>Returns a short-lived ID token, or null when selection fails/cancels. Never changes backend sessions.</summary>
    public virtual async UniTask<string> RequestIdTokenAsync(bool interactive, CancellationToken token)
    {
        if (!IsAvailable) return null;
#if UNITY_ANDROID
        var completion = new UniTaskCompletionSource<string>();
        var callback = new IdentityCallback(completion);
        byte[] bytes = new byte[NonceBytes];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
        using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
        using var bridge = new AndroidJavaClass(BridgeClass);
        using var operation = bridge.CallStatic<AndroidJavaObject>("signIn", activity,
            GooglePlayGames.GameInfo.WebClientId, Convert.ToBase64String(bytes), interactive, callback);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(AuthenticationTimeoutSeconds), DelayType.Realtime);
        try
        {
            string value = await completion.Task.AttachExternalCancellation(timeout.Token);
            await UniTask.SwitchToMainThread(token);
            return value;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
        finally { operation.Call("cancel"); GC.KeepAlive(callback); }
#else
        await UniTask.CompletedTask; return null;
#endif
    }
#if UNITY_ANDROID
    private sealed class IdentityCallback : AndroidJavaProxy
    {
        private readonly UniTaskCompletionSource<string> _completion;
        public IdentityCallback(UniTaskCompletionSource<string> completion) : base(BridgeClass + "$Callback") => _completion = completion;
        [UnityEngine.Scripting.Preserve] public void onSuccess(string value) => _completion.TrySetResult(value);
        [UnityEngine.Scripting.Preserve] public void onError(string _) => _completion.TrySetResult(null);
    }
#endif
}
