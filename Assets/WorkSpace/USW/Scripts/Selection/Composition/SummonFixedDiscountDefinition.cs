using System;

/// <summary>소환 고정 할인 지속 설정.</summary>
[Serializable]
public sealed class SummonFixedDiscountDefinition : SelectionEffectDefinition
{
    /// <summary>Amount 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float Amount;
}
