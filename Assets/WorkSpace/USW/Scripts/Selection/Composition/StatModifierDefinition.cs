using System;

/// <summary>항목의 합산 규칙은 계산기가 결정한다. 설정이 임의 연산을 지시하지 않는다.</summary>
[Serializable]
public sealed class StatModifierDefinition : SelectionEffectDefinition
{
    /// <summary>보정 항목.</summary>
    public SelectionStat Stat;
    /// <summary>비율(0.2 = 20%). 가감산 모두 허용하며 유효 범위는 항목별 검증한다.</summary>
    public float Ratio;
}
