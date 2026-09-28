using UnityEngine;

/// <summary>한 종류의 패널티 기본 정의. 이번 런의 누적 횟수를 저장하지 않는다.</summary>
[CreateAssetMenu(fileName = "RunPenaltyData", menuName = "Game/Endless/Run Penalty")]
public sealed class RunPenaltyData : ScriptableObject
{
    [SerializeField] private string _key;
    [SerializeField] private string _displayName;
    [SerializeField] private RunPenaltyTarget _target;
    [SerializeField] private RunPenaltyUnit _unit;
    [SerializeField] private double _delta;
    [SerializeField] private bool _isConfigured;

    /// <summary>대소문자를 구분하는 고정 키.</summary>
    public string Key => _key;
    /// <summary>플레이어에게 보여줄 이름.</summary>
    public string DisplayName => _displayName;
    /// <summary>효과 대상.</summary>
    public RunPenaltyTarget Target => _target;
    /// <summary>증감값의 단위.</summary>
    public RunPenaltyUnit Unit => _unit;
    /// <summary>부호를 포함한 1회 증감값. Percent -10은 현재 값의 0.9배.</summary>
    public double Delta => _delta;
    /// <summary>효과 수치와 단위가 확정되었는지 여부.</summary>
    public bool IsConfigured => _isConfigured;
}
