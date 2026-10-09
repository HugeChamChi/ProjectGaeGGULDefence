using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>전투 시간에 따른 TD1002 범위와 공격력. 짝수 범위 중심은 저작 오프셋으로 정한다.</summary>
[Serializable]
public sealed class TotemGrowthStage
{
    /// <summary>단계 표시명.</summary>
    public string Name;
    /// <summary>이 단계에 도달하는 누적 전투 시간(초).</summary>
    [Min(0)] public float RequiredSeconds;
    /// <summary>범위 안 공격력 가산 비율.</summary>
    [Min(0)] public float AttackBonus;
    /// <summary>회전 전 셀 오프셋.</summary>
    public List<Vector2Int> Offsets = new List<Vector2Int>();
}
