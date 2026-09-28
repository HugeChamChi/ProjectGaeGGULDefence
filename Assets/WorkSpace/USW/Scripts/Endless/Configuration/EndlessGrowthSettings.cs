using System;
using UnityEngine;

/// <summary>보스 성장의 명시적 입력. 미정 상태를 0% 성장 확정과 구분한다.</summary>
[Serializable]
public sealed class EndlessGrowthSettings
{
    [SerializeField] private bool _isConfigured;
    [SerializeField] private int _firstGrowthRound;
    [SerializeField] private int _intervalRounds;
    [SerializeField] private double _hpPercentPerStep;
    [SerializeField] private double _defenseAddedPerStep;
    [SerializeField] private double _expPercentPerStep;

    /// <summary>성장 간격과 모든 수치가 명시적으로 확정되었는지 여부.</summary>
    public bool IsConfigured => _isConfigured;
    /// <summary>첫 성장 1회가 적용되는 1 기반 라운드.</summary>
    public int FirstGrowthRound => _firstGrowthRound;
    /// <summary>추가 성장 1회 사이의 라운드 수.</summary>
    public int IntervalRounds => _intervalRounds;
    /// <summary>성장 1회당 HP 증가율. 10은 10%이며 곱누적한다.</summary>
    public double HpPercentPerStep => _hpPercentPerStep;
    /// <summary>성장 1회당 방어력 고정 증가량. 기존 로그 방어식은 유지한다.</summary>
    public double DefenseAddedPerStep => _defenseAddedPerStep;
    /// <summary>성장 1회당 총 EXP 증가율. HP 증가와 독립적이며 0도 명시적 설정이다.</summary>
    public double ExpPercentPerStep => _expPercentPerStep;
}
