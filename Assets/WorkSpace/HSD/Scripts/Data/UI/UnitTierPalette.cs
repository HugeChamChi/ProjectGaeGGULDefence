using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 프로젝트 전체의 등급 비주얼을 중앙 관리하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "UnitTierPalette", menuName = "UI/TierPalette")]
public class UnitTierPalette : ScriptableObject
{
    [SerializeField]
    private List<UnitTierUIInfo> tierInfos = new List<UnitTierUIInfo>();

    [Header("Fallback")]
    [SerializeField] private UnitTierUIInfo defaultInfo;

    private Dictionary<Tier, UnitTierUIInfo> _cache;

    /// <summary>
    /// 특정 등급의 UI 정보를 가져옵니다. 설정이 없을 경우 defaultInfo를 반환합니다.
    /// </summary>
    public UnitTierUIInfo GetInfo(Tier tier)
    {
        InitializeCache();

        if (_cache.TryGetValue(tier, out var info))
            return info;

        Debug.LogWarning($"[UnitTierPalette] {tier} 등급 설정이 누락되었습니다. 기본 설정을 반환합니다.");
        return defaultInfo;
    }

    private void InitializeCache()
    {
        if (_cache != null && _cache.Count == tierInfos.Count) return;

        _cache = new Dictionary<Tier, UnitTierUIInfo>();
        foreach (var info in tierInfos)
        {
            if (info == null) continue;
            if (_cache.ContainsKey(info.tier)) continue;
            _cache.Add(info.tier, info);
        }
    }

    private void OnValidate()
    {
        _cache = null;
    }
}

/// <summary>
/// 유닛 등급(UnitTier)에 따른 비주얼 설정 데이터 클래스
/// 필드가 늘어나더라도 UnitTierPalette를 사용하는 코드는 수정할 필요가 없으므로 유연합니다.
/// </summary>
[Serializable]
public class UnitTierUIInfo
{
    public Tier tier;
    public string tierName;          // 등급명 (전시용)
    public Sprite frameSprite;       // 카드/슬롯 테두리
    public Color bgColor = Color.white;
    public Color textColor = Color.white;
}