using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// L10n — Localization 편의 래퍼 (static class).
/// RM.cs 패턴을 따르며, ILocalizationService를 내부적으로 참조합니다.
///
/// === 사용법 ===
/// 1) RootLifetimeScope에서 Inject:
///    builder.RegisterBuildCallback(resolver =>
///    {
///        var locService = resolver.Resolve&lt;ILocalizationService&gt;();
///        L10n.Inject(locService);
///    });
///
/// 2) GameInitializer에서 초기화:
///    await L10n.InitializeAsync();
///
/// 3) 어디서든 호출:
///    string text = L10n.Get(ELocTable.Common, "ui_btn_confirm");
///    string formatted = L10n.GetFormatted(ELocTable.Common, "ui_reward_msg", 100);
/// </summary>
public static class L10n
{
    static ILocalizationService _service;

    /// <summary>
    /// DI에서 ILocalizationService 인스턴스를 주입합니다.
    /// UI_Base.Inject(audio) 패턴과 동일합니다.
    /// </summary>
    public static void Inject(ILocalizationService service)
    {
        _service = service;
    }

    /// <summary>
    /// LocalizationService를 초기화합니다. (테이블 캐시 구축)
    /// GameInitializer.InitializeAsync에서 호출하세요.
    /// </summary>
    public static async UniTask InitializeAsync()
    {
        if (_service == null)
        {
            Debug.LogError("[L10n] ILocalizationService가 Inject되지 않았습니다. RootLifetimeScope에서 L10n.Inject를 호출하세요.");
            return;
        }

        if (_service is LocalizationService concrete)
        {
            await concrete.InitializeAsync();
        }
        else
        {
            Debug.LogWarning("[L10n] InitializeAsync는 LocalizationService 구현체에서만 지원됩니다.");
        }
    }

    // ────────────────────────────────────────────
    // string key 기반 조회
    // ────────────────────────────────────────────

    /// <summary>
    /// 캐시에서 즉시 번역 문자열을 반환합니다.
    /// </summary>
    public static string Get(ELocTable table, string key)
    {
        if (_service == null)
        {
            Debug.LogWarning("[L10n] 서비스가 초기화되지 않았습니다.");
            return key;
        }
        return _service.Get(table, key);
    }

    /// <summary>
    /// 포맷 인자를 포함하여 번역 문자열을 반환합니다.
    /// </summary>
    public static string GetFormatted(ELocTable table, string key, params object[] args)
    {
        if (_service == null)
        {
            Debug.LogWarning("[L10n] 서비스가 초기화되지 않았습니다.");
            return key;
        }
        return _service.GetFormatted(table, key, args);
    }

    // ────────────────────────────────────────────
    // Locale 관리
    // ────────────────────────────────────────────

    /// <summary>
    /// 현재 활성화된 Locale 코드를 반환합니다.
    /// </summary>
    public static string CurrentLocale => _service?.CurrentLocale ?? "unknown";

    /// <summary>
    /// Locale을 변경합니다. (예: "ko", "en", "ja")
    /// </summary>
    public static void ChangeLocale(string localeCode)
    {
        if (_service == null)
        {
            Debug.LogWarning("[L10n] 서비스가 초기화되지 않았습니다.");
            return;
        }
        _service.ChangeLocale(localeCode);
    }

    /// <summary>
    /// Locale 변경 이벤트 구독/해제를 위한 래퍼.
    /// </summary>
    public static event Action<string> OnLocaleChanged
    {
        add
        {
            if (_service != null) _service.OnLocaleChanged += value;
        }
        remove
        {
            if (_service != null) _service.OnLocaleChanged -= value;
        }
    }
}
