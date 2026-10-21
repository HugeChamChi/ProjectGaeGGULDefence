using UnityEngine;

/// <summary>Lizard-specific Spine pose timings and healing, alongside the shared scheduling/effect values.</summary>
[CreateAssetMenu(fileName = "LizardPattern", menuName = "Game/Boss/Lizard Pattern")]
public sealed class LizardPatternData : BossPatternData
{
    [Header("Installed Spine animation")]
    /// <summary>Exact animation name in the installed SkeletonDataAsset.</summary>
    public string SpineAnimation;
    /// <summary>Native clip length captured from the actual Spine asset by the authoring tool.</summary>
    [Min(0f)] public float NativeDuration;
    /// <summary>Native time of the pose held during the effect window.</summary>
    [Min(0f)] public float HoldAt;
    /// <summary>Native time where the unmodified exit animation resumes.</summary>
    [Min(0f)] public float ResumeAt;
    /// <summary>When the held pose is released; derived from the single authored gameplay duration.</summary>
    public float ReleaseAt => patternType == BossPatternType.LizardThornBarrier ? ImpactTimeSec + duration : duration;
    /// <inheritdoc/>
    public override float CastDuration => patternType == BossPatternType.LizardThreat ? NativeDuration
        : ReleaseAt + Mathf.Max(0f,NativeDuration-ResumeAt);
    /// <summary>Hides the submerged rig while the underground pose is held.</summary>
    public bool HideWhileHeld;
    [Header("Burrow recovery")]
    /// <summary>Combat seconds from entry to the one recovery and its visual feedback.</summary>
    [Min(0f)] public float HealDelay;
    /// <summary>Total maximum-HP fraction restored by one burrow.</summary>
    [Range(0f, 1f)] public float HealFraction;
}
