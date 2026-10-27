using System;

/// <summary>전투 드론 수에 따른 감망의 해킹 스택 추가 피해 보정. 직렬화 타입 이름은 유지한다.</summary>
[Serializable]
public sealed class StackSkillFrequencyDefinition : SelectionEffectDefinition
{
    /// <summary>전투 드론 1기당 스택 추가 피해 증가 비율.</summary>
    public float ReductionPerStack;
    /// <summary>최대 증가 비율. 0이면 상한 없이 적용한다.</summary>
    public float MaximumReduction;
}
