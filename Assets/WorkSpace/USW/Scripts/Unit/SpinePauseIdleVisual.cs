using Spine.Unity;
using UnityEngine;

/// <summary>
/// Spine 외형 유닛용 선택 화면 대기 모션 (유닛 Spine 전환 대비, 2026-09-30). 유닛 프리팹의 SkeletonAnimation 옆에 붙이면 된다.
/// 일시정지 중: 실제 시간으로 재생. 공격처럼 반복하지 않는 모션 도중이면 끝까지 재생한 뒤 대기 모션으로, 그 외에는 바로 대기 모션.
/// 재개: 원래 시간 설정만 되돌린다 (다음 공격부터는 전투 코드가 평소대로 애니메이션을 지정).
/// </summary>
public class SpinePauseIdleVisual : MonoBehaviour, IPauseIdleVisual
{
    [SerializeField] private SkeletonAnimation _skeleton;
    [SerializeField] private string _idleAnimation = "idle";
    [SerializeField] private int _track;

    private bool _inPauseIdle;
    private bool _unscaledBeforePause;

    private void Reset() => _skeleton = GetComponentInChildren<SkeletonAnimation>();

    /// <inheritdoc />
    public void SetPauseIdle(bool on)
    {
        if (_skeleton == null || on == _inPauseIdle) return;
        _inPauseIdle = on;
        if (!on) { _skeleton.UnscaledTime = _unscaledBeforePause; return; }

        _unscaledBeforePause = _skeleton.UnscaledTime;
        _skeleton.UnscaledTime = true;
        var state = _skeleton.AnimationState;
        var data = _skeleton.SkeletonDataAsset != null ? _skeleton.SkeletonDataAsset.GetSkeletonData(true) : null;
        if (state == null || data == null || data.FindAnimation(_idleAnimation) == null) return;

        var current = state.GetTrack(_track);
        if (current == null || current.Animation == null) { state.SetAnimation(_track, _idleAnimation, true); return; }
        if (current.Animation.Name == _idleAnimation) return;
        if (current.Loop) state.SetAnimation(_track, _idleAnimation, true);
        else if (current.Next == null) state.AddAnimation(_track, _idleAnimation, true, 0f); // 공격 모션 끝나고 대기로
    }
}
