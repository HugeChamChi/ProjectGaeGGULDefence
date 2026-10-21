using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>Synchronizes only idle/basic-attack tracks with their owner's final attack interval.</summary>
[DefaultExecutionOrder(-40)]
public sealed class SpineAttackSpeedSync : MonoBehaviour
{
    /// <summary>User-approved maximum playback rate for an accelerated basic attack.</summary>
    public const float MaximumAttackRate = 2.5f;
    private readonly Dictionary<string, float> _attackDurations = new();
    private SkeletonAnimation _skeleton;
    private UnitBase _unit;
    private DroneUnit _drone;
    private DragHandler _drag;
    private string _idle;
    private float _authoredTimeScale;
    private bool _pauseIdle;

    /// <summary>Connects the existing visual and caches local components; drone ownership is read after pool initialization.</summary>
    public void Initialize(SkeletonAnimation skeleton, UnitBase unit, DroneUnit drone, DragHandler drag, string idle)
    {
        _skeleton = skeleton;
        _unit = unit;
        _drone = drone;
        _drag = drag;
        _idle = idle;
        _authoredTimeScale = skeleton != null ? skeleton.timeScale : 1f;
        if (_skeleton != null) _skeleton.BeforeUpdate += BeforeSpineUpdate;
    }

    /// <summary>Registers one full attack cycle, including all parts of start/loop/end visuals.</summary>
    public void RegisterAttackCycle(params string[] names)
    {
        var data = _skeleton != null ? _skeleton.SkeletonDataAsset?.GetSkeletonData(false) : null;
        if (data == null) return;
        float duration = 0f;
        foreach (string name in names)
            if (!string.IsNullOrEmpty(name)) duration += data.FindAnimation(name)?.Duration ?? 0f;
        foreach (string name in names)
            if (!string.IsNullOrEmpty(name)) _attackDurations[name] = duration;
    }

    /// <summary>Preserves the existing unscaled selection/pause animation contract.</summary>
    public void SetPauseIdle(bool on) => _pauseIdle = on;

    /// <summary>Returns a track multiplier. Slowdown affects idle and attack; acceleration affects only overlapping attacks.</summary>
    public static float CalculateRate(float baseInterval, float currentInterval, float attackDuration, bool attack)
    {
        if (!(baseInterval > 0f) || float.IsInfinity(baseInterval) || !(currentInterval > 0f)) return 1f;
        if (currentInterval > baseInterval) return baseInterval / currentInterval;
        return attack ? Mathf.Clamp(attackDuration / currentInterval, 1f, MaximumAttackRate) : 1f;
    }

    private void BeforeSpineUpdate(ISkeletonAnimation animation) => Refresh();

    // Spine can skip BeforeUpdate while culled. Stun must still own the instance clock.
    private void Update() => RefreshStop();

    private UnitBase Owner => _drone != null ? _drone.Owner : _unit;

    private void RefreshStop()
    {
        if (_skeleton == null) return;
        var owner = Owner;
        _skeleton.timeScale = owner != null && owner.IsStunned ? 0f : _authoredTimeScale;
    }

    private void OnEnable()
    {
        if (_skeleton != null) _skeleton.BeforeUpdate += BeforeSpineUpdate;
    }

    /// <summary>Updates current, queued and mixing tracks before Spine advances, with stun taking priority over pause/drag.</summary>
    public void Refresh()
    {
        if (_skeleton == null) return;
        var owner = Owner;
        RefreshStop();
        var state = _skeleton.AnimationState;
        if (state == null) return;
        bool preview = _pauseIdle || (_drag != null && _drag.IsDragging);
        float baseline = 0f, interval = 0f;
        if (!preview && owner != null && owner.unitData != null && owner.Combat != null)
        {
            baseline = 1f / Mathf.Max(owner.unitData.attackSpeed.Get(owner.currentTier), .01f);
            interval = _drone != null ? _drone.EffectiveAttackInterval : owner.GetCurrentAttackInterval();
        }
        for (var entry = state.GetTrack(0); entry != null; entry = entry.Next)
        {
            Apply(entry, baseline, interval);
            for (var mixing = entry.MixingFrom; mixing != null; mixing = mixing.MixingFrom)
                Apply(mixing, baseline, interval);
        }
    }

    private void Apply(TrackEntry entry, float baseline, float interval)
    {
        string name = entry.Animation.Name;
        bool attack = _attackDurations.TryGetValue(name, out float duration);
        // Skills (notably Betan's event-driven bomb launch), drag and other animations keep authored timing.
        entry.TimeScale = attack || name == _idle
            ? CalculateRate(baseline, interval, _authoredTimeScale > 0f ? duration / _authoredTimeScale : duration, attack)
            : 1f;
    }

    private void OnDisable()
    {
        _pauseIdle = false;
        if (_skeleton == null) return;
        _skeleton.BeforeUpdate -= BeforeSpineUpdate;
        _skeleton.timeScale = _authoredTimeScale;
        var state = _skeleton.AnimationState;
        for (var entry = state?.GetTrack(0); entry != null; entry = entry.Next) entry.TimeScale = 1f;
    }
}
