using UnityEngine;

/// <summary>Authored recommended combo copied from FxLab_DisigmanGlitchLink; presentation only.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Disigman Glitch Link")]
public sealed class DisigmanGlitchLinkSettings : ScriptableObject
{
    /// <summary>Alternating colors copied from the selected lab combo.</summary>
    public Color[] LinkPalette = { Color.white, new Color(1f, .353f, .47f), new Color(.05f, .55f, 1f), new Color(1f, .353f, .47f) };
    /// <summary>Cyan ghost color shared with the timer glitch.</summary>
    public Color Cyan = Color.cyan;
    /// <summary>Pink ghost color shared with the timer glitch.</summary>
    public Color Hot = Color.magenta;
    /// <summary>Data shards emitted at impact.</summary>
    public int ShardCount = 22;
    /// <summary>Initial scatter speed range in canvas pixels per second.</summary>
    public Vector2 ShardSpeed = new Vector2(160f, 380f);
    /// <summary>Scatter duration before homing begins.</summary>
    public float ShardScatterSeconds = .07f;
    /// <summary>Homing travel duration in scaled seconds.</summary>
    public float ShardTravel = .2f;
    /// <summary>Shard radius range in canvas pixels.</summary>
    public Vector2 ShardSize = new Vector2(4f, 7.5f);
    /// <summary>Maximum horizontal curve in canvas pixels.</summary>
    public float ShardCurve = 90f;
    /// <summary>Boss distortion duration in scaled seconds.</summary>
    public float BossGlitchSeconds = .3f;
    /// <summary>Maximum horizontal ghost jitter in world units.</summary>
    public float BossJitter = .12f;
    /// <summary>RGB ghost offset in world units.</summary>
    public float BossGhostOffset = .16f;
    /// <summary>Peak AllIn1 shader distortion amount.</summary>
    public float BossGlitchAmount = 8f;
    /// <summary>Blocks distributed along the fired beam.</summary>
    public int BeamBlocks = 22;
    /// <summary>Square blocks emitted at the impact point.</summary>
    public int ImpactBlocks = 16;
    /// <summary>Small blocks emitted near the charged muzzle.</summary>
    public int ChargeBlocks = 6;
    /// <summary>Horizontal tears emitted at impact.</summary>
    public int TearLines = 3;
    /// <summary>World block sorting order matching the lab.</summary>
    public int BlockSortingOrder = 38;
    /// <summary>Build-referenced material retaining the GLITCH_ON shader variant.</summary>
    [Tooltip("References the enabled GLITCH_ON variant so it is included in player builds.")]
    public Material BossGlitchMaterial;
}
