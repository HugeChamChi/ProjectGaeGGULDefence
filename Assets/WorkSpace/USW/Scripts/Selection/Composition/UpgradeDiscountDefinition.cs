using System;

/// <summary>강화 가격의 지속 할인. 환급은 별도 획득 명령이다.</summary>
[Serializable]
public sealed class UpgradeDiscountDefinition : SelectionEffectDefinition
{
    /// <summary>할인 비율(0~1).</summary>
    public float Ratio;
}
