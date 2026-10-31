using System;
using UnityEngine;

/// <summary>
/// Look and timing of the tier tag and skill charge display under each field unit: a small dark tag with the
/// tier number, plus a quiet charge display. Colors, sizes and times live here, not in code or the material.
/// </summary>
[CreateAssetMenu(fileName = "UnitStatusBadgeSettings", menuName = "USW/UI/Unit Status Badge Settings")]
public sealed class UnitStatusBadgeSettings : ScriptableObject
{
    /// <summary>Where the skill charge is drawn.</summary>
    public enum GaugeStyle
    {
        /// <summary>Thin bar to the right of the tag.</summary>
        TagBar = 0,
        /// <summary>The tag's border fills clockwise from the top; no bar.</summary>
        TagBorder = 1,
        /// <summary>The tag's background fills from the bottom behind the number; no bar.</summary>
        TagFill = 2
    }

    /// <summary>What the tag shows for a tier.</summary>
    public enum TierSymbol
    {
        /// <summary>Tier number (1 = Normal ... 4 = Legend).</summary>
        Number = 0,
        /// <summary>Filled star in place of the number (final tier).</summary>
        Star = 1,
        /// <summary>Outlined star in place of the number (final tier).</summary>
        StarOutline = 2
    }

    /// <summary>Animated shader effect on a tag (meant for the final tier).</summary>
    public enum TagEffect
    {
        None = 0,
        /// <summary>A diagonal light band sweeps across the tag.</summary>
        Sweep = 1,
        /// <summary>Small glints twinkle around the symbol in turn.</summary>
        Sparkle = 2,
        /// <summary>A soft rainbow sheen flows over the symbol.</summary>
        Shimmer = 3,
        /// <summary>A point of light runs around the tag border.</summary>
        BorderRun = 4
    }

    /// <summary>Per-tier tag look. The number itself (1 = Normal ... 4 = Legend) carries the tier.</summary>
    [Serializable]
    public struct TierStyle
    {
        /// <summary>Tag plate color.</summary>
        public Color Plate;
        /// <summary>Tier number color; also the rim color.</summary>
        public Color Number;
        /// <summary>Rim thickness in tag half-sizes (0 = none).</summary>
        [Range(0f, 0.4f)] public float RimWidth;
        /// <summary>Number or star.</summary>
        public TierSymbol Symbol;
        /// <summary>Small glint that twinkles now and then at the tag corner.</summary>
        public bool Glint;
        /// <summary>Animated shader effect on this tier's tag.</summary>
        public TagEffect Effect;
    }

    [Header("Style")]
    public GaugeStyle Style = GaugeStyle.TagBar;

    [Header("Layout (prefab UI units)")]
    /// <summary>Frame the tag and bar are laid out in; scaled down to fit the cell.</summary>
    public Vector2 FrameSize = new Vector2(104f, 32f);
    /// <summary>Tag side length.</summary>
    [Min(4f)] public float TagSize = 30f;
    /// <summary>Bar thickness (TagBar).</summary>
    [Min(2f)] public float BarHeight = 11f;
    /// <summary>Bar divisions (TagBar). 0 or 1 = one continuous bar.</summary>
    [Range(0, 10)] public int BarSegments;
    /// <summary>Gap between bar divisions.</summary>
    [Min(0f)] public float BarSegmentGap = 2f;
    /// <summary>Charge border thickness in tag half-sizes (TagBorder).</summary>
    [Range(0.05f, 0.5f)] public float BorderWidth = 0.2f;
    /// <summary>How strongly the charge color shows behind the number (TagFill).</summary>
    [Range(0f, 1f)] public float FillStrength = 0.55f;
    /// <summary>Dark outline around tag and bar, in tag half-sizes.</summary>
    [Range(0f, 0.6f)] public float OutlineWidth = 0.12f;

    [Header("Cell placement")]
    /// <summary>Largest share of the cell width the frame may take.</summary>
    [Range(0.2f, 1f)] public float CellWidthFraction = 0.78f;
    /// <summary>Gap from the cell bottom, in cell heights.</summary>
    [Range(0f, 0.3f)] public float CellBottomInset = 0.04f;
    /// <summary>0 = cell left edge, 0.5 = centered, 1 = right edge.</summary>
    [Range(0f, 1f)] public float HorizontalAnchor = 0.5f;
    /// <summary>Gap from the cell side when anchored left/right, in cell widths.</summary>
    [Range(0f, 0.3f)] public float CellSideInset = 0.04f;

    [Header("Tiers (Normal, Rare, Epic, Legend)")]
    public TierStyle[] Tiers =
    {
        new TierStyle { Plate = new Color(0.10f, 0.11f, 0.14f), Number = new Color(0.97f, 0.97f, 0.95f) },
        new TierStyle { Plate = new Color(0.10f, 0.11f, 0.14f), Number = new Color(0.97f, 0.97f, 0.95f) },
        new TierStyle { Plate = new Color(0.10f, 0.11f, 0.14f), Number = new Color(0.97f, 0.97f, 0.95f) },
        new TierStyle { Plate = new Color(0.10f, 0.11f, 0.14f), Number = new Color(1f, 0.80f, 0.25f), RimWidth = 0.14f },
    };
    /// <summary>Outline color around tag and bar.</summary>
    public Color OutlineColor = new Color(0.05f, 0.07f, 0.08f);
    /// <summary>Glint color and twinkle period (tiers with <see cref="TierStyle.Glint"/>).</summary>
    public Color GlintColor = new Color(1f, 0.98f, 0.88f);
    [Min(0.2f)] public float GlintPeriodSeconds = 2.6f;
    /// <summary>Tag effect cycle (sweep repeat, sparkle and border-run loop).</summary>
    [Min(0.2f)] public float EffectPeriodSeconds = 2.2f;
    /// <summary>Duration of one light sweep within the cycle.</summary>
    [Min(0.05f)] public float EffectSweepSeconds = 0.5f;

    [Header("Charge colors")]
    public Color GaugeFill = new Color(1f, 0.80f, 0.18f);
    /// <summary>Charge color while the skill is fully charged and waiting.</summary>
    public Color GaugeFull = new Color(1f, 0.93f, 0.55f);
    public Color GaugeTrack = new Color(0.16f, 0.16f, 0.19f);
    /// <summary>Passive-only units: track and stripe colors of the disabled charge.</summary>
    public Color PassiveTrack = new Color(0.20f, 0.21f, 0.23f);
    public Color PassiveStripe = new Color(0.27f, 0.28f, 0.30f);

    [Header("Ready moment")]
    /// <summary>Charge at or above which the skill counts as ready.</summary>
    [Range(0.9f, 1f)] public float ReadyThreshold = 0.999f;
    /// <summary>A drop from at least this charge counts as "skill fired" even if full was never seen.</summary>
    [Range(0.5f, 1f)] public float FiredFromCharge = 0.85f;
    /// <summary>Length of the brief white blink when the skill becomes ready.</summary>
    [Min(0.05f)] public float ReadyFlashSeconds = 0.25f;
    /// <summary>Pulse period while full and waiting.</summary>
    [Min(0.1f)] public float FullPulseSeconds = 0.9f;
    /// <summary>Pulse strength while full and waiting.</summary>
    [Range(0f, 1f)] public float FullGlow = 0.15f;
    public float ChargeEaseSeconds = 0.16f;
    public float DischargeEaseSeconds = 0.12f;

    /// <summary>Style for a tier. Non-combat tiers use the top tier style.</summary>
    public TierStyle GetTier(Tier tier)
    {
        if (Tiers == null || Tiers.Length == 0) return default;
        return Tiers[Mathf.Clamp((int)tier, 0, Tiers.Length - 1)];
    }
}
