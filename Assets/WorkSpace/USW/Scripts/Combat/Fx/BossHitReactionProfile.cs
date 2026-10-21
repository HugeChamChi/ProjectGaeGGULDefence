using UnityEngine;

/// <summary>Authored visual settings for Crocus's accepted hit reaction.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Boss Hit Reaction Profile")]
public sealed class BossHitReactionProfile : ScriptableObject
{
    /// <summary>Time to reach the heavy impact pose.</summary>
    [Min(.001f)] public float AttackSeconds = .075f;
    /// <summary>Time spent bracing at peak impact.</summary>
    [Min(0f)] public float HoldSeconds = .09f;
    /// <summary>Time to recover the normal animated pose.</summary>
    [Min(.001f)] public float RecoverSeconds = .42f;
    /// <summary>Compression along Crocus body's vertical local X axis.</summary>
    public float BodyCompression = .15f;
    /// <summary>Expansion along the body's horizontal local Y axis.</summary>
    public float BodyExpansion = .035f;
    /// <summary>Body rotation at peak impact.</summary>
    public float BodyTilt = 3f;
    /// <summary>Head counterrotation at peak impact.</summary>
    public float HeadTilt = 9f;
    /// <summary>Arm bracing angle at peak impact.</summary>
    public float ArmBrace = 15f;
    /// <summary>Multiplicative warm hit color; original alpha is preserved.</summary>
    public Color HitTint = new Color(1f, .42f, .38f, 1f);
    /// <summary>Maximum tint mix, without accumulation across hits.</summary>
    [Range(0f, 1f)] public float HitTintStrength = .22f;
    /// <summary>Initial tint hold duration.</summary>
    [Min(0f)] public float HitTintHoldSeconds = .025f;
    /// <summary>Tint return duration.</summary>
    [Min(.001f)] public float HitTintFadeSeconds = .135f;
    /// <summary>Minimum pulse interval, allowing the original color to show during dense attacks.</summary>
    [Min(0f)] public float HitTintIntervalSeconds = .2f;
}
