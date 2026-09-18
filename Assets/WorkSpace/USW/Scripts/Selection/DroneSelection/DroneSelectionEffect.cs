using System;
using UnityEngine;

/// <summary>카드에 직렬화하는 드론 효과 설정. DI 및 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class DroneSelectionEffect
{
    public DroneSelectionKind Kind;
    [Min(0)] public float Interval;
    [Tooltip("비율: 0.2 = 20%. 랜덤 효과는 최솟값.")] public float Value;
    [Tooltip("효율 상한 또는 랜덤 최댓값.")] public float MaxValue;
    [Min(0)] public int Count;
}
