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

    private Spine.TrackEntry _skillEntry;

    /// <summary>True until the complete authored skill cycle finishes or is interrupted.</summary>
    public bool IsPlayingSkill => _skillEntry != null;

    private void OnEnable() => ResetToIdle();
    private void OnDisable() => ClearSkill();

    private void ClearSkill()
    {
        if (_skillEntry == null) return;
        _skillEntry.Complete -= OnSkillFinished;
        _skillEntry.Interrupt -= OnSkillFinished;
        _skillEntry.End -= OnSkillFinished;
        _skillEntry = null;
    }

    private void OnSkillFinished(Spine.TrackEntry entry)
    {
        if (ReferenceEquals(entry, _skillEntry)) ClearSkill();
    }

    /// <summary>Returns to idle when the selected chief changes or the HUD is enabled.</summary>
    public void ResetToIdle()
    {
        ClearSkill();
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
        ClearSkill();
        _skillEntry = state.SetAnimation(0, _skillAnimation, false);
        _skillEntry.Complete += OnSkillFinished;
        _skillEntry.Interrupt += OnSkillFinished;
        _skillEntry.End += OnSkillFinished;
        if (_animation.Skeleton.Data.FindAnimation(_idleAnimation) != null)
        {
            var idle = state.AddAnimation(0, _idleAnimation, true, 0f);
            // Spine otherwise subtracts mix duration and cuts off the end of the skill.
            idle.Delay = _skillEntry.TrackComplete;
        }
    }

    /// <summary>Retains the existing unavailable tint while keeping idle animation active.</summary>
    public void SetTint(Color color)
    {
        if (_graphic != null) _graphic.color = color;
    }
}
