using System;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Spine 외형 보스 연결 — BossBase.OnPatternStarted를 받아 패턴별 Spine 애니메이션을 한 번 재생하고 대기 애니메이션으로 돌아간다.
/// BossBase의 _animator는 비워 둔다 (보스 피격 연출은 없음, 사망은 즉시 제거 경로를 그대로 쓴다).
/// 게임 속도(Time.timeScale)를 따른다. 선택 화면 일시정지 중에는 FieldPauseVisuals가 SetPauseIdle로 대기 모션만 실제 시간으로 돌린다.
/// </summary>
[RequireComponent(typeof(BossBase))]
public class BossSpineAnimator : MonoBehaviour, IPauseIdleVisual
{
    [Serializable]
    private struct PatternAnimation
    {
        [Tooltip("BossPatternData.AnimationTrigger 값 (예: Earthquake)")]
        public string Trigger;
        [Tooltip("재생할 Spine 애니메이션 이름 (예: skill_1)")]
        public string Animation;
    }

    [SerializeField] private SkeletonAnimation _skeleton;
    [SerializeField] private string _idleAnimation = "idle_0";
    [SerializeField] private PatternAnimation[] _patternAnimations;

    private BossBase _boss;
    private bool _pauseIdle;
    private bool _unscaledBeforePause;
    private bool _patternHeld;
    private float _speedBeforeHold;

    /// <summary>대기 이외의 패턴 트랙은 공격 보류 시점부터 진행하지 않는다.</summary>
    public void SetPatternHold(bool on)
    {
        if (_skeleton == null || _patternHeld == on) return;
        if (on)
        {
            var current = _skeleton.AnimationState?.GetTrack(0);
            if (current?.Animation == null || current.Animation.Name == _idleAnimation) return;
            _patternHeld = true;
            _speedBeforeHold = _skeleton.timeScale;
            _skeleton.timeScale = 0f;
        }
        else
        {
            _patternHeld = false;
            _skeleton.timeScale = _speedBeforeHold;
        }
    }

    private void Awake()
    {
        _boss = GetComponent<BossBase>();
        _boss.OnPatternStarted += OnPatternStarted;
    }

    private void Start() => PlayIdle();

    private void OnDestroy()
    {
        SetPauseIdle(false);
        SetPatternHold(false);
        if (_boss != null) _boss.OnPatternStarted -= OnPatternStarted;
    }

    private void PlayIdle()
    {
        var state = _skeleton != null ? _skeleton.AnimationState : null;
        if (state == null || !HasAnimation(_idleAnimation)) return;
        state.SetAnimation(0, _idleAnimation, true);
    }

    private void OnPatternStarted(BossPatternData pattern)
    {
        if (pattern == null || _patternAnimations == null) return;
        var state = _skeleton != null ? _skeleton.AnimationState : null;
        if (state == null) return;

        foreach (var mapping in _patternAnimations)
        {
            if (mapping.Trigger != pattern.AnimationTrigger) continue;
            if (!HasAnimation(mapping.Animation))
            {
                Debug.LogWarning($"[BossSpineAnimator] {name}: Spine 애니메이션 '{mapping.Animation}' 없음");
                return;
            }
            state.SetAnimation(0, mapping.Animation, false);
            if (HasAnimation(_idleAnimation)) state.AddAnimation(0, _idleAnimation, true, 0f);
            return;
        }
    }

    /// <summary>
    /// 선택 화면 일시정지 중 대기 모션 유지 (시각 전용). 대기 모션 중일 때만 실제 시간으로 돌리고,
    /// 패턴 모션 도중이면 그대로 멈춰 둔다 (패턴은 멈춤 — 사용자 결정 2026-09-30).
    /// </summary>
    public void SetPauseIdle(bool on)
    {
        if (_skeleton == null) return;
        if (on)
        {
            if (_pauseIdle) return;
            var current = _skeleton.AnimationState?.GetTrack(0);
            if (current == null || current.Animation == null || current.Animation.Name != _idleAnimation) return;
            _pauseIdle = true;
            _unscaledBeforePause = _skeleton.UnscaledTime;
            _skeleton.UnscaledTime = true;
        }
        else if (_pauseIdle)
        {
            _pauseIdle = false;
            _skeleton.UnscaledTime = _unscaledBeforePause;
        }
    }

    private bool HasAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName) || _skeleton == null || _skeleton.SkeletonDataAsset == null) return false;
        var data = _skeleton.SkeletonDataAsset.GetSkeletonData(true);
        return data != null && data.FindAnimation(animationName) != null;
    }
}
