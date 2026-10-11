using System;

/// <summary>판매 식량 가산 지속 설정.</summary>
[Serializable]
public sealed class SellFoodBonusDefinition : SelectionEffectDefinition
{
    /// <summary>Amount 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float Amount;
}
