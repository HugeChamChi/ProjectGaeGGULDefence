using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization.Settings;
using VContainer.Unity;

/// <summary>Device-local quality/language/notification consent. OS permission is a separate state.</summary>
public sealed class DeviceSettingsService : IStartable, IDisposable
{
    private const string QualityKey = "Settings.Quality.v1", LocaleKey = "Settings.Locale.v1", PushKey = "Settings.PushConsent.v1";
    private readonly CancellationTokenSource _lifetime = new();
    /// <summary>Raised after the local reminder consent changes.</summary>
    public event Action<bool> OnPushConsentChanged;
    /// <summary>Consent on this installation, independent from OS permission.</summary>
    public bool PushConsent => PlayerPrefs.GetInt(PushKey, 0) == 1;
    /// <summary>Current quality preset from the actual project list.</summary>
    public string Quality => QualitySettings.names[QualitySettings.GetQualityLevel()];
    /// <summary>Selected locale or a loading indicator.</summary>
    public string Language => LocalizationSettings.InitializationOperation.IsDone ? LocalizationSettings.SelectedLocale?.LocaleName ?? "언어 데이터 없음" : "불러오는 중";
    /// <inheritdoc />
    public void Start()
    {
        int index = Array.IndexOf(QualitySettings.names, PlayerPrefs.GetString(QualityKey, ""));
        if (index >= 0) QualitySettings.SetQualityLevel(index, true);
        RestoreLanguageAsync(_lifetime.Token).Forget();
    }
    private static async UniTaskVoid RestoreLanguageAsync(CancellationToken token)
    {
        try
        {
            await UniTask.WaitUntil(() => LocalizationSettings.InitializationOperation.IsDone, cancellationToken: token);
            var locale = LocalizationSettings.AvailableLocales.GetLocale(PlayerPrefs.GetString(LocaleKey, ""));
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Debug.LogWarning("[Settings] Language: " + error.GetType().Name); }
    }
    /// <summary>Cycles authored presets and persists the stable preset name.</summary>
    public void NextQuality()
    {
        int next = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
        QualitySettings.SetQualityLevel(next, true); PlayerPrefs.SetString(QualityKey, QualitySettings.names[next]); PlayerPrefs.Save();
    }
    /// <summary>Changes only to an installed locale; no invented translations.</summary>
    public string NextLanguage()
    {
        if (!LocalizationSettings.InitializationOperation.IsDone) return "언어 데이터를 불러오는 중입니다.";
        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (locales.Count < 2) return "추가 언어 데이터가 아직 등록되지 않았습니다.";
        int next = (locales.IndexOf(LocalizationSettings.SelectedLocale) + 1) % locales.Count;
        LocalizationSettings.SelectedLocale = locales[next];
        PlayerPrefs.SetString(LocaleKey, locales[next].Identifier.Code); PlayerPrefs.Save();
        return "언어를 변경했습니다. 번역이 등록된 화면부터 적용됩니다.";
    }
    /// <summary>Stores consent and requests Android 13+ permission only after a user action.</summary>
    public void SetPushConsent(bool consent)
    {
        PlayerPrefs.SetInt(PushKey, consent ? 1 : 0); PlayerPrefs.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (consent && !UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            if (version.GetStatic<int>("SDK_INT") >= 33) UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
        }
#endif
        OnPushConsentChanged?.Invoke(consent);
    }
    /// <summary>Actual OS notification permission; Editor has no device notification delivery.</summary>
    public static bool NotificationPermissionGranted
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
            using var manager = activity.Call<AndroidJavaObject>("getSystemService", "notification");
            return manager.Call<bool>("areNotificationsEnabled");
#else
            return false;
#endif
        }
    }
    /// <summary>Actual OS notification state, separate from local reminder consent.</summary>
    public string PushStatus
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return NotificationPermissionGranted ? (PushConsent ? "휴대폰 알림 허용됨, 스태미나와 출석 알림 ON" : "휴대폰 알림 허용됨, 게임 알림 OFF") : "휴대폰 설정에서 이 앱 알림이 꺼져 있어요";
#else
            return "";
#endif
        }
    }
    /// <inheritdoc />
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); }
}
