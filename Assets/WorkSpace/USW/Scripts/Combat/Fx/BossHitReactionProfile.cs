using UnityEngine;

/// <summary>
/// Shared boss hit reaction values (all bosses, Spine and sprite). Defaults reproduce the approved HTML draft
/// "지금 방식" (outputs/damage-floater-drafts-20261114): white flash, squash from the feet, short screen shake on crits.
/// </summary>
[CreateAssetMenu(menuName = "GaeGGUL/Boss/Hit Reaction Profile", fileName = "BossHitReactionProfile")]
public sealed class BossHitReactionProfile : ScriptableObject
{
    /// <summary>Resources path of the shared profile used when a boss has no explicit profile.</summary>
    public const string ResourcePath = "BossHitReactionProfile";

    [Header("Flash")]
    [Tooltip("Spine/Skeleton Fill. Also used for sprite bosses. Referenced here so the shader ships in builds.")]
    public Shader FillShader;
    public Color FlashColor = Color.white;
    [Tooltip("Flash level set by a normal hit (0..1). Draft 0.16")]
    [Range(0f, 1f)] public float NormalFlash = .16f;
    [Tooltip("Flash level set by a critical hit or heavy impact. Draft 0.9")]
    [Range(0f, 1f)] public float CriticalFlash = .9f;
    [Tooltip("Fill amount at flash level 1. Draft 0.55")]
    [Range(0f, 1f)] public float FlashOpacity = .55f;
    [Tooltip("Decay per second while the flash is above the threshold. Draft 4")]
    [Min(0f)] public float FlashFastDecay = 4f;
    [Tooltip("Decay per second below the threshold. Draft 9")]
    [Min(0f)] public float FlashSlowDecay = 9f;
    [Range(0f, 1f)] public float FlashDecayThreshold = .3f;

    [Header("Squash (pivot at the bottom of the boss)")]
    [Tooltip("Height loss by a normal hit. Draft 0.012")]
    [Range(0f, .2f)] public float NormalSquash = .012f;
    [Tooltip("Height loss by a critical hit. Draft 0.045")]
    [Range(0f, .2f)] public float CriticalSquash = .045f;
    [Tooltip("Width gain per unit of height loss. Draft 0.6")]
    [Min(0f)] public float SquashWidthRatio = .6f;
    [Tooltip("Linear decay per second. Draft 0.25")]
    [Min(0f)] public float SquashLinearDecay = .25f;
    [Tooltip("Remaining fraction after one second (exponential). Draft 0.0001")]
    [Range(0.000001f, 1f)] public float SquashRemainPerSecond = .0001f;

    [Header("Critical screen shake")]
    [Tooltip("Shake amplitude as a fraction of screen height. Draft 7 px of 1560")]
    [Range(0f, .05f)] public float CriticalShakeScreenFraction = .0045f;
    [Tooltip("Shake length in seconds. Draft about 0.12")]
    [Min(0f)] public float CriticalShakeSeconds = .12f;

    [Tooltip("Burn ticks are frequent damage over time; the draft only reacts to direct hits.")]
    public bool ReactToBurn;
}
