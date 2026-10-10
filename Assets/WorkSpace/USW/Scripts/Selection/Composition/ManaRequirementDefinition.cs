using System;

/// <summary>델탕 필요 공격 횟수 감소 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class ManaRequirementDefinition : SelectionEffectDefinition
{
    /// <summary>감소 비율.</summary>
    public float ReductionRatio;
}
