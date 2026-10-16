using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Google Play Games sign-in shared by account linking and startup login.
/// After a guest account is linked, the device remembers to log in with Google instead of creating a new guest.
/// </summary>
public static class GoogleAccountAuth
{
    private const string LoginMethodKey = "Account.LoginMethod.v1";
    private const string GoogleMethod = "gpgs";
    private const string IdentityMethod = "google";
    private const string SwitchPendingKey = "Account.SwitchPending.v1";
    private const string IdentityOwnerKey = "Account.GoogleOwner.v1";

    /// <summary>Whether this device should log in with the linked Google account.</summary>
    public static bool PrefersGoogleLogin => PlayerPrefs.GetString(LoginMethodKey, "") == GoogleMethod;
    /// <summary>Whether the linked account uses Sign in with Google instead of Play Games identity.</summary>
    public static bool PrefersIdentityLogin => PlayerPrefs.GetString(LoginMethodKey, "") == IdentityMethod;
    /// <summary>Whether an explicit session change must be recovered without creating a new guest.</summary>
    public static bool SwitchPending => PlayerPrefs.GetInt(SwitchPendingKey, 0) == 1;
    /// <summary>Last confirmed linked backend owner; used to reject unintended automatic account changes.</summary>
    public static string IdentityOwner => PlayerPrefs.GetString(IdentityOwnerKey, "");
    /// <summary>Persists a confirmed Google identity login for the next application start.</summary>
    public static void RememberIdentityLogin()
    { PlayerPrefs.SetString(LoginMethodKey, IdentityMethod); PlayerPrefs.SetString(IdentityOwnerKey, BackEnd.Backend.UserInDate); PlayerPrefs.Save(); }
    /// <summary>Marks the explicit transition boundary for restart recovery.</summary>
    public static void SetSwitchPending(bool value)
    { if (value) PlayerPrefs.SetInt(SwitchPendingKey, 1); else PlayerPrefs.DeleteKey(SwitchPendingKey); PlayerPrefs.Save(); }

    /// <summary>Remembers the linked Google login for the next app start.</summary>
    public static void RememberGoogleLogin() { PlayerPrefs.SetString(LoginMethodKey, GoogleMethod); PlayerPrefs.Save(); }

    /// <summary>Forgets the login preference only after confirmed account withdrawal.</summary>
    public static void ForgetGoogleLogin()
    { PlayerPrefs.DeleteKey(LoginMethodKey); PlayerPrefs.DeleteKey(IdentityOwnerKey); PlayerPrefs.DeleteKey(SwitchPendingKey); PlayerPrefs.Save(); }

    /// <summary>Whether Play Games IDs are configured in the project (Window > Google Play Games > Setup).</summary>
    public static bool IsConfigured
    {
        get
        {
#if UNITY_ANDROID && !DISABLE_GPGS
            return GooglePlayGames.GameInfo.ApplicationIdInitialized() && GooglePlayGames.GameInfo.WebClientIdInitialized();
#else
            return false;
#endif
        }
    }

    /// <summary>Why linking is unavailable on this build, or null when it can be attempted.</summary>
    public static string UnavailableReason
    {
        get
        {
#if UNITY_EDITOR
            return "Google 연동은 Android 기기에서 확인할 수 있어요.";
#else
            return IsConfigured ? null : "Google 연동 준비 중이에요. (Play 게임즈 설정 필요)";
#endif
        }
    }

    /// <summary>Signs in to Play Games and returns a one-time server auth code, or null on failure/cancel.</summary>
    public static async UniTask<string> RequestServerAuthCodeAsync(CancellationToken token)
    {
#if UNITY_ANDROID && !UNITY_EDITOR && !DISABLE_GPGS
        if (!IsConfigured) return null;
        var auth = new UniTaskCompletionSource<bool>();
        GooglePlayGames.PlayGamesPlatform.Instance.Authenticate(status => auth.TrySetResult(status == GooglePlayGames.BasicApi.SignInStatus.Success));
        if (!await auth.Task.AttachExternalCancellation(token))
        {
            // Automatic sign-in can fail silently on first use; ask once explicitly.
            var manual = new UniTaskCompletionSource<bool>();
            GooglePlayGames.PlayGamesPlatform.Instance.ManuallyAuthenticate(status => manual.TrySetResult(status == GooglePlayGames.BasicApi.SignInStatus.Success));
            if (!await manual.Task.AttachExternalCancellation(token)) return null;
        }
        var code = new UniTaskCompletionSource<string>();
        GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(true, value => code.TrySetResult(value));
        return await code.Task.AttachExternalCancellation(token);
#else
        await UniTask.CompletedTask;
        return null;
#endif
    }
}
