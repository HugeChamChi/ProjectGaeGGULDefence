using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>보스 처치 수에 따른 범위 프리셋. 짝수 크기 범위의 중심도 오프셋으로 명시한다.</summary>
[Serializable]
public sealed class TotemGrowthStage
{
    /// <summary>이 범위를 사용하는 최소 처치 수.</summary>
    [Min(0)] public int RequiredKills;
    /// <summary>토템 위치 기준 셀 오프셋. 토템 회전을 따른다.</summary>
    public List<Vector2Int> Offsets = new List<Vector2Int>();
}
