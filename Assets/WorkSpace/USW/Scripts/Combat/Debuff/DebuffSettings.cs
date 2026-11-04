using System.Collections.Generic;
using UnityEngine;

/// <summary>루트 수명의 디버프 정의 SO 및 공통 방어 설정.</summary>
[CreateAssetMenu(menuName = "Game/Debuff Settings", fileName = "DebuffSettings")]
public sealed class DebuffSettings : ScriptableObject
{
    [SerializeField] private double _defenseScale;
    [SerializeField] private DebuffData[] _definitions;
    /// <summary>공식의 K.</summary>
    public double DefenseScale => _defenseScale;
    /// <summary>Finds SO presentation data without changing combat definitions.</summary>
    public DebuffData FindPresentation(int id)
    {
        if (_definitions != null)
            foreach (var data in _definitions)
                if (data != null && data.Id == id) return data;
        return null;
    }
    /// <summary>로컬 초기 정의.</summary>
    public IEnumerable<DebuffDefinition> CreateDefinitions()
    {
        if (_definitions == null) yield break;
        foreach (var data in _definitions)
        {
            if (data == null) throw new System.InvalidOperationException("Missing DebuffData.");
            yield return data.CreateDefinition();
        }
    }
}
