using System;
using UnityEngine;

/// <summary>추첨 풀의 후보. 효과 정의와 확률 설정을 분리한다.</summary>
[Serializable]
public sealed class RunPenaltyPoolEntry
{
    [SerializeField] private RunPenaltyData _penalty;
    [SerializeField] private bool _enabled;
    [SerializeField] private double _weight;
    [SerializeField] private bool _weightConfirmed;

    /// <summary>효과 정의.</summary>
    public RunPenaltyData Penalty => _penalty;
    /// <summary>추첨 사용 여부. 비활성 후보의 미정 값은 허용한다.</summary>
    public bool Enabled => _enabled;
    /// <summary>다른 활성 후보에 대한 상대 가중치.</summary>
    public double Weight => _weight;
    /// <summary>작업안 가중치를 제작 확률로 오인하지 않기 위한 확정 표시.</summary>
    public bool WeightConfirmed => _weightConfirmed;
}
