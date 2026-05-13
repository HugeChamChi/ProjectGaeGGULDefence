using UnityEngine;
using System;
using System.Collections.Generic;

public enum UnitStatType
{
    Atk,
    SkillAtk,
    SkillCooldown,
    FoodPerTick
}

[CreateAssetMenu(fileName = "UnitStatPalette", menuName = "UI/StatPalette")]
public class UnitStatPalette : ScriptableObject
{
    [SerializeField] private List<UnitStatVisualInfo> statInfos;

    private Dictionary<UnitStatType, UnitStatVisualInfo> _cache;

    public UnitStatVisualInfo GetInfo(UnitStatType type)
    {
        if (_cache == null || _cache.Count != statInfos.Count)
        {
            _cache = new Dictionary<UnitStatType, UnitStatVisualInfo>();
            foreach (var info in statInfos)
            {
                if (!_cache.ContainsKey(info.statType))
                    _cache.Add(info.statType, info);
            }
        }

        return _cache.TryGetValue(type, out var visualInfo) ? visualInfo : null;
    }
}

[Serializable]
public class UnitStatVisualInfo
{
    public UnitStatType statType;
    public Sprite icon;           // 스텟 아이콘
    public Color bgColor = Color.white; // 슬롯 배경색
}