using System;
using UnityEngine;

/// <summary>드론 배치 실험실의 시안 한 개. 인스펙터에서 Play 중에 값을 바꿔 볼 수 있다.</summary>
[Serializable]
public sealed class DroneLayoutVariant
{
    /// <summary>버튼·상태 줄에 쓰는 짧은 이름.</summary>
    public string Label;
    /// <summary>상태 줄에 보이는 한 줄 설명.</summary>
    [TextArea] public string Description;
    /// <summary>배치 방식.</summary>
    public DroneLayoutKind Kind;
    /// <summary>드론 프리팹 크기(0.9)에 곱하는 배율.</summary>
    [Min(0.1f)] public float DroneScale = 1f;
    /// <summary>유닛 프리팹 크기(1.7)에 곱하는 배율.</summary>
    [Min(0.1f)] public float UnitScale = 1f;
    /// <summary>배치 간격 배율 — 방식별 기본 간격에 곱한다.</summary>
    [Min(0.1f)] public float Spread = 1f;
}
