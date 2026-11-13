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
    /// <summary>추첨 사용 여부. 비활성 후보의 미정 값은 허용한다. PenaltyBalance 차트에 키가 있으면 차트 값.</summary>
    public bool Enabled => _penalty != null ? GGD.BackendCharts.PenaltyBalance.IsEnabled(_penalty.Key, _enabled) : _enabled;
    /// <summary>에셋에 저작된 사용 여부(차트와 무관). 엑셀 검사와 반영 전용.</summary>
    public bool BundledEnabled => _enabled;
    /// <summary>다른 활성 후보에 대한 상대 가중치. PenaltyBalance 차트에 키가 있으면 차트 값.</summary>
    public double Weight => _penalty != null ? GGD.BackendCharts.PenaltyBalance.WeightOf(_penalty.Key, _weight) : _weight;
    /// <summary>에셋에 저작된 가중치(차트와 무관). 엑셀 검사와 반영 전용.</summary>
    public double BundledWeight => _weight;
    /// <summary>작업안 가중치를 제작 확률로 오인하지 않기 위한 확정 표시.</summary>
    public bool WeightConfirmed => _weightConfirmed || _penalty != null && GGD.BackendCharts.PenaltyBalance.Has(_penalty.Key);
}
