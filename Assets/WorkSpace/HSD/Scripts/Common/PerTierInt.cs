using System;
using UnityEngine.Scripting.APIUpdating;

/// <summary>등급별로 다른 값을 반환한다.</summary>
[Serializable]
[DisplayName("등급별값")]
[MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PerTierInt")]
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

    public void Set(Tier tier, int value)
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
