using System;

/// <summary>감망 스택당 스킬 주기 감소 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class StackSkillFrequencyDefinition : SelectionEffectDefinition
{
    /// <summary>스택당 감소 비율.</summary>
    public float ReductionPerStack;
    /// <summary>최대 감소 비율.</summary>
    public float MaximumReduction;
}
