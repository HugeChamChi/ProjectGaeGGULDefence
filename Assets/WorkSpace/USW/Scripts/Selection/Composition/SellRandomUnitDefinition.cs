using System;

/// <summary>판매 시 유닛 획득 확률 지속 설정.</summary>
[Serializable]
public sealed class SellRandomUnitDefinition : SelectionEffectDefinition
{
    /// <summary>Chance 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float Chance;
}
