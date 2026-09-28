using System;

/// <summary>유닛/드론/표시 코드가 공유하는 이번 런의 현재 배율. 기본값은 모두 1이다.</summary>
public interface IRunStatModifiers
{
    /// <summary>공격력 배율. 공격력 기반 계산 경로에서 한 번 적용한다.</summary>
    double AttackMultiplier { get; }
    /// <summary>초당 공격 빈도의 배율. 실제 간격에는 나눗셈으로 적용한다.</summary>
    double AttackFrequencyMultiplier { get; }
    /// <summary>배율 활성화/초기화 후 발생. UI 구독자는 수명이 끝날 때 해제한다.</summary>
    event Action OnModifiersChanged;
}
