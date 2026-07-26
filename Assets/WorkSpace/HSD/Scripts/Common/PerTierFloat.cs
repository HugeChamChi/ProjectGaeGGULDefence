using System;

/// <summary>등급별로 다른 값을 반환한다.</summary>
[Serializable]
[KoreanName("등급별값")]
public class PerTierFloat : IScaledFloat
{
    public float normal;
    public float rare;
    public float epic;
    public float legend;

    public float Get(Tier tier) => tier switch
    {
        Tier.Rare => rare,
        Tier.Epic => epic,
        Tier.Legend => legend,
        _ => normal,
    };
}
