using System;
using UnityEngine;

/// <summary>유닛 등급별 디버프 연결. 현재 등급을 공유 SO에 저장하지 않는다.</summary>
[Serializable]
public class PerTierDebuffBinding
{
    [SerializeField] private DebuffBinding _normal;
    [SerializeField] private DebuffBinding _rare;
    [SerializeField] private DebuffBinding _epic;
    [SerializeField] private DebuffBinding _legend;
    /// <summary>지정 등급의 값 복사.</summary>
    public DebuffBinding Get(Tier tier) => tier switch
    {
        Tier.Rare => _rare, Tier.Epic => _epic, Tier.Legend => _legend, _ => _normal
    };
    /// <summary>전투 시작 전 시트의 검증된 값을 반영한다.</summary>
    public void Set(Tier tier, DebuffBinding binding)
    {
        switch (tier)
        {
            case Tier.Rare: _rare = binding; break;
            case Tier.Epic: _epic = binding; break;
            case Tier.Legend: _legend = binding; break;
            default: _normal = binding; break;
        }
    }
}
