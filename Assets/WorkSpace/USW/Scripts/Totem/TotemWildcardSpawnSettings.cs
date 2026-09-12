using System;
using UnityEngine;

/// <summary>TD1004의 생성 주기와 조커 유닛 정의. 인구수는 사용하지 않는다.</summary>
[Serializable]
public sealed class TotemWildcardSpawnSettings
{
    /// <summary>전투 진행 시간 기준 생성 주기.</summary>
    [Min(0.01f)] public float IntervalSeconds = 40f;
    /// <summary>WildcardUnit 프리팹을 사용하는 스탯 0의 노말 유닛 SO.</summary>
    public UnitData Unit;
}
