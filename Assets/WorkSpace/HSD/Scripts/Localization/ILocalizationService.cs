using System;

/// <summary>
/// 로컬라이제이션 서비스 인터페이스.
/// Dictionary 캐시 기반 즉시 조회, Locale 변경, 변경 이벤트를 제공합니다.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// 캐시된 Dictionary에서 key에 해당하는 번역 문자열을 즉시 반환합니다.
    /// </summary>
    string Get(ELocTable table, string key);

    /// <summary>
    /// 포맷 인자를 포함하여 번역 문자열을 반환합니다.
    /// 내부적으로 string.Format을 사용합니다.
    /// </summary>
    string GetFormatted(ELocTable table, string key, params object[] args);

    /// <summary>
    /// Locale을 변경합니다. (예: "ko", "en", "ja")
    /// 변경 후 캐시가 갱신되며 OnLocaleChanged 이벤트가 발생합니다.
    /// </summary>
    void ChangeLocale(string localeCode);

    /// <summary>
    /// 현재 활성화된 Locale 코드를 반환합니다.
    /// </summary>
    string CurrentLocale { get; }

    /// <summary>
    /// Locale 변경 시 호출되는 이벤트.
    /// 인자: 변경된 localeCode
    /// </summary>
    event Action<string> OnLocaleChanged;
}
