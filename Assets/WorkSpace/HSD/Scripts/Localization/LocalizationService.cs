using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// ILocalizationService 구현체.
/// Unity Localization 패키지를 사용하여 StringTable을 로드하고
/// Dictionary 캐시를 구축합니다.
/// </summary>
public class LocalizationService : ILocalizationService
{
    // ── 캐시: 테이블별 Dictionary<Key, Value> ──
    readonly Dictionary<ELocTable, Dictionary<string, string>> _cache = new();

    // ── 테이블 이름 매핑 ──
    static readonly Dictionary<ELocTable, string> TableNames = new()
    {
        { ELocTable.Common, "Common" },
        { ELocTable.Data, "Data" }
    };

    public string CurrentLocale { get; private set; }
    public event Action<string> OnLocaleChanged;

    // ────────────────────────────────────────────
    // 초기화 — 모든 테이블을 미리 로드하여 캐시 구축
    // ────────────────────────────────────────────
    public async UniTask InitializeAsync()
    {
        // LocalizationSettings 초기화 대기
        await LocalizationSettings.InitializationOperation.Task;

        CurrentLocale = LocalizationSettings.SelectedLocale.Identifier.Code;
        Debug.Log($"<color=cyan>[LocalizationService]</color> 초기화 시작 — Locale: {CurrentLocale}");

        await RebuildCacheAsync();

        Debug.Log($"<color=green>[LocalizationService]</color> 캐시 구축 완료 (테이블 {_cache.Count}개)");
    }

    // ────────────────────────────────────────────
    // 캐시에서 즉시 조회
    // ────────────────────────────────────────────
    public string Get(ELocTable table, string key)
    {
        if (_cache.TryGetValue(table, out var dict) && dict.TryGetValue(key, out var value))
            return value;

        Debug.LogWarning($"[LocalizationService] 키를 찾을 수 없습니다 — Table: {table}, Key: {key}");
        return key; // 키 자체를 반환 (fallback)
    }

    public string GetFormatted(ELocTable table, string key, params object[] args)
    {
        string raw = Get(table, key);
        try
        {
            return string.Format(raw, args);
        }
        catch (FormatException e)
        {
            Debug.LogWarning($"[LocalizationService] Format 오류 — Key: {key}, Error: {e.Message}");
            return raw;
        }
    }

    // ────────────────────────────────────────────
    // Locale 변경
    // ────────────────────────────────────────────
    public void ChangeLocale(string localeCode)
    {
        var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
        if (locale == null)
        {
            Debug.LogWarning($"[LocalizationService] 지원하지 않는 Locale: {localeCode}");
            return;
        }

        LocalizationSettings.SelectedLocale = locale;
        CurrentLocale = localeCode;

        // 캐시 갱신 후 이벤트 발생
        RebuildCacheAsync().ContinueWith(() =>
        {
            Debug.Log($"<color=cyan>[LocalizationService]</color> Locale 변경 완료: {localeCode}");
            OnLocaleChanged?.Invoke(localeCode);
        }).Forget();
    }

    // ────────────────────────────────────────────
    // 내부: StringTable → Dictionary 캐시 구축
    // ────────────────────────────────────────────
    async UniTask RebuildCacheAsync()
    {
        _cache.Clear();

        foreach (var kvp in TableNames)
        {
            var table = kvp.Key;
            var tableName = kvp.Value;

            var dict = new Dictionary<string, string>();

            // StringTable 로드
            var stringTable = await LocalizationSettings.StringDatabase
                .GetTableAsync(tableName).Task;

            if (stringTable == null)
            {
                Debug.LogWarning($"[LocalizationService] StringTable '{tableName}'을 찾을 수 없습니다.");
                _cache[table] = dict;
                continue;
            }

            // 모든 엔트리를 Dictionary로 변환
            foreach (var entry in stringTable)
            {
                if (entry.Value != null && !string.IsNullOrEmpty(entry.Value.Key))
                {
                    dict[entry.Value.Key] = entry.Value.LocalizedValue;
                }
            }

            _cache[table] = dict;
            Debug.Log($"[LocalizationService] '{tableName}' 테이블 로드 완료 — {dict.Count}개 엔트리");
        }
    }
}
