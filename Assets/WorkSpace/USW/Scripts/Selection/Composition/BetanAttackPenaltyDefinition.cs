using System;

/// <summary>베탕 공격력 자체의 감소 비율. 모든 공격력 기반 피해와 표시가 공유한다.</summary>
[Serializable]
public sealed class BetanAttackPenaltyDefinition : SelectionEffectDefinition
{
    /// <summary>베탕 공격력 자체의 감소 비율. 모든 공격력 기반 피해와 표시가 공유한다.</summary>
    public float ReductionRatio;
}
