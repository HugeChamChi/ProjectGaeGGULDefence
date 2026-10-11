using System;
using UnityEngine;

/// <summary>실험실에서 같은 종류 여러 개체의 피해를 발생시키는 예시 데이터.</summary>
[Serializable]
public sealed class DamageChartSample
{
    /// <summary>이 종류의 실제 유닛 정의.</summary>
    public UnitData Unit;
    /// <summary>실험실 표시명. 비우면 실제 유닛 데이터의 이름을 쓴다.</summary>
    public string DisplayNameOverride;
    /// <summary>비교에 사용할 초상화.</summary>
    public Sprite Portrait;
    /// <summary>같은 종류의 배치 개체 수.</summary>
    [Min(0)] public int Count = 2;
    /// <summary>개체당 예시 DPS. 실제 밸런스 값이 아니다.</summary>
    [Min(0f)] public float DemoDamagePerSecond = 1000f;
    /// <summary>이 종류를 식별하는 막대/링 색.</summary>
    public Color Color = Color.cyan;
}
