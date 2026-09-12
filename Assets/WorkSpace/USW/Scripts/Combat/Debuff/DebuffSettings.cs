using System.Collections.Generic;
using UnityEngine;

/// <summary>루트 수명의 디버프 로딩 설정 및 공통 방어 설정.</summary>
[CreateAssetMenu(menuName = "Game/Debuff Settings", fileName = "DebuffSettings")]
public sealed class DebuffSettings : ScriptableObject
{
    [SerializeField] private string _sheetGid;
    [SerializeField] private double _defenseScale;
    [SerializeField] private DebuffData[] _definitions;
    /// <summary>미설정이면 포함된 SO 정의 사용. 설정되면 시트가 정본.</summary>
    public string SheetGid => _sheetGid;
    /// <summary>공식의 K.</summary>
    public double DefenseScale => _defenseScale;
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
