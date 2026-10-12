using UnityEngine;

/// <summary>Authored appearance and visibility of the rotation badge shared by totem screens.</summary>
[CreateAssetMenu(fileName = "TotemRotateBadgeSettings", menuName = "USW/UI/Totem Rotate Badge Settings")]
public sealed class TotemRotateBadgeSettings : ScriptableObject
{
    /// <summary>Rotation artwork.</summary>
    public Sprite RotationIcon;
    /// <summary>Filled circular sprite for the ring and background.</summary>
    public Sprite Circle;
    /// <summary>Show the badge even when the totem has no effect or disabled cells.</summary>
    public bool ShowWithoutRange = true;
    /// <summary>Overview diameter in root canvas units.</summary>
    [Min(1f)] public float OverviewDiameter = 78f;
    /// <summary>Reward detail diameter in root canvas units.</summary>
    [Min(1f)] public float DetailDiameter = 80f;
    /// <summary>Placed information diameter in root canvas units.</summary>
    [Min(1f)] public float InfoDiameter = 56f;
    /// <summary>Inset from the top right corner as a fraction of the radius.</summary>
    [Range(0f, 1f)] public float CornerInset = 0.45f;
    /// <summary>Available ring color.</summary>
    public Color AvailableRing = new Color(0.45f, 1f, 0.85f, 1f);
    /// <summary>Unavailable ring color.</summary>
    public Color UnavailableRing = new Color(0.68f, 0.68f, 0.74f, 0.6f);
    /// <summary>Inner circle color.</summary>
    public Color Background = new Color(0.08f, 0.1f, 0.17f, 0.94f);
    /// <summary>Available icon color.</summary>
    public Color AvailableIcon = Color.white;
    /// <summary>Unavailable icon color.</summary>
    public Color UnavailableIcon = new Color(0.62f, 0.62f, 0.68f, 0.85f);
    /// <summary>Unavailable slash color.</summary>
    public Color SlashColor = new Color(1f, 0.32f, 0.3f, 1f);
    /// <summary>Icon size as a fraction of the diameter.</summary>
    [Range(0f, 1f)] public float IconRatio = 0.66f;
    /// <summary>Inner circle diameter reduction as a fraction of the diameter.</summary>
    [Range(0f, 1f)] public float RingInsetRatio = 0.1f;
    /// <summary>Minimum inner circle diameter reduction.</summary>
    [Min(0f)] public float MinimumRingInset = 3f;
    /// <summary>Slash length as a fraction of the diameter.</summary>
    [Range(0f, 1f)] public float SlashLengthRatio = 0.86f;
    /// <summary>Slash thickness as a fraction of the diameter.</summary>
    [Range(0f, 1f)] public float SlashThicknessRatio = 0.09f;
    /// <summary>Minimum slash thickness.</summary>
    [Min(0f)] public float MinimumSlashThickness = 3f;
    /// <summary>Slash angle in degrees.</summary>
    public float SlashAngle = 45f;

    /// <summary>Uses the same authored preview offsets as the range display.</summary>
    public bool ShouldShow(TotemData data) => data != null && (ShowWithoutRange ||
        data.GetEffectPreviewOffsets().Exists(offset => offset != Vector2Int.zero) ||
        data.GetAttackDisabledPreviewOffsets().Exists(offset => offset != Vector2Int.zero));
}
