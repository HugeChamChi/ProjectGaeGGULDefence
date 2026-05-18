using UnityEngine;
using System;

[CreateAssetMenu(fileName = "UIOutlineSettings", menuName = "Game/UIOutlineSettings")]
public class UIOutlineSettings : ScriptableObject
{
    [Serializable]
    public struct TierOutline
    {
        public Tier tier;
        public Color color;
        public float outerWidth;
        public float innerWidth;
    }

    public TierOutline[] tierOutlines;

    public TierOutline GetSettings(Tier tier)
    {
        if (tierOutlines == null) return default;
        foreach (var outline in tierOutlines)
        {
            if (outline.tier == tier) return outline;
        }
        return default;
    }
}
