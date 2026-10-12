using System;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>원본 Spine 모션에 저작된 발사 프레임을 이벤트 타임라인으로 추가한다.</summary>
[CreateAssetMenu(menuName = "USW/Spine/Skill Launch Event")]
public sealed class SpineSkillLaunchEvent : SkeletonDataModifierAsset
{
    [SerializeField, SpineAnimation] private string _animationName = "skill";
    [SerializeField] private string _eventName = "self_destruct_launch";
    [SerializeField, Min(0)] private int _frame;
    [SerializeField, Min(1)] private int _framesPerSecond = 30;

    /// <summary>Spine에서 전달할 발사 이벤트 이름.</summary>
    public string EventName => _eventName;
    /// <summary>저작 프레임의 애니메이션 시간. 배속/스턴은 Spine 시계를 따른다.</summary>
    public float AnimationTime => (float)_frame / _framesPerSecond;
    /// <summary>해당 애니메이션에 발사 이벤트가 있는지 확인한다.</summary>
    public bool ContainsEvent(Spine.Animation animation)
    {
        if (animation == null) return false;
        foreach (var timeline in animation.Timelines)
            if (timeline is EventTimeline events)
                foreach (var e in events.Events)
                    if (e.Data.Name == _eventName) return true;
        return false;
    }
    /// <summary>Spine 원본에 같은 이벤트가 이미 있으면 그 타임라인을 우선한다.</summary>
    public override void Apply(SkeletonData skeletonData)
    {
        var animation = skeletonData?.FindAnimation(_animationName);
        if (animation == null || ContainsEvent(animation)) return;
        if (_framesPerSecond <= 0 || _frame < 0 || AnimationTime > animation.Duration || string.IsNullOrWhiteSpace(_eventName))
            throw new InvalidOperationException("Skill launch event must be authored within the Spine animation.");
        var definition = skeletonData.FindEvent(_eventName);
        if (definition == null) { definition = new EventData(_eventName); skeletonData.Events.Add(definition); }
        var timeline = new EventTimeline(1);
        timeline.SetFrame(0, new Spine.Event(AnimationTime, definition));
        animation.Timelines.Add(timeline);
        animation.SetTimelines(animation.Timelines, animation.Bones);
    }
}
