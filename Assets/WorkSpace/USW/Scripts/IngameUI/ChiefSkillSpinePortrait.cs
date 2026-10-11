using Spine.Unity;
using UnityEngine;

/// <summary>Plays the chief's HUD idle continuously and a skill animation only after activation succeeds.</summary>
[DisallowMultipleComponent]
public sealed class ChiefSkillSpinePortrait : MonoBehaviour
{
    [SerializeField] private SkeletonGraphic _graphic;
    [SerializeField] private SkeletonAnimation _animation;
    [SerializeField, SpineAnimation] private string _idleAnimation = "idle";
    [SerializeField, SpineAnimation] private string _skillAnimation = "skill";

    private void OnEnable() => ResetToIdle();

    /// <summary>Returns to idle when the selected chief changes or the HUD is enabled.</summary>
    public void ResetToIdle()
    {
        if (_animation == null) return;
        _animation.Initialize(false);
        var state = _animation.AnimationState;
        if (state == null) return;
        state.ClearTracks();
        _animation.Skeleton.SetupPose();
        if (_animation.Skeleton.Data.FindAnimation(_idleAnimation) != null)
            state.SetAnimation(0, _idleAnimation, true);
    }

    /// <summary>Plays one authored skill cycle, then resumes idle without changing combat timing.</summary>
    public void PlaySkill()
    {
        if (_animation == null || _animation.AnimationState == null) return;
        if (_animation.Skeleton.Data.FindAnimation(_skillAnimation) == null) return;
        var state = _animation.AnimationState;
        state.SetAnimation(0, _skillAnimation, false);
        if (_animation.Skeleton.Data.FindAnimation(_idleAnimation) != null)
            state.AddAnimation(0, _idleAnimation, true, 0f);
    }

    /// <summary>Retains the existing unavailable tint while keeping idle animation active.</summary>
    public void SetTint(Color color)
    {
        if (_graphic != null) _graphic.color = color;
    }
}
