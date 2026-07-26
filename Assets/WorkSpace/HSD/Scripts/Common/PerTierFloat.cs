using System;

/// <summary>등급별로 다른 값을 반환한다.</summary>
[Serializable]
[DisplayName("등급별값")]
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

    public void Set(Tier tier, float value)
    {
        switch (tier)
        {
            case Tier.Rare: rare = value; break;
            case Tier.Epic: epic = value; break;
            case Tier.Legend: legend = value; break;
            default: normal = value; break;
        }
    }
}
