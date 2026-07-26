using System;

/// <summary>등급별로 다른 값을 반환한다.</summary>
[Serializable]
[KoreanName("등급별값")]
public class PerTierInt : IScaledInt
{
    public int normal;
    public int rare;
    public int epic;
    public int legend;

    public int Get(Tier tier) => tier switch
    {
        Tier.Rare => rare,
        Tier.Epic => epic,
        Tier.Legend => legend,
        _ => normal,
    };
}
