using System;

/// <summary>소환 비율 할인 지속 설정.</summary>
[Serializable]
public sealed class SummonDiscountDefinition : SelectionEffectDefinition
{
    /// <summary>Ratio 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float Ratio;
}
