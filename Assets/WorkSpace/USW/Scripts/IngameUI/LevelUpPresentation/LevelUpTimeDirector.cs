using System;
using DG.Tweening;
using UnityEngine;
using VContainer;

/// <summary>
/// 레벨업 시간 연출 — 멈출 때는 슬로우모션으로 서서히 정지, 선택 후에는 서서히 1배속 복귀.
/// (LevelUpUI와 같은 GameObject에 부착. 없으면 LevelUpUI가 즉시 정지/재개한다.)
/// Time.timeScale을 직접 쓰지 않고 TimeScaleService에 이 컴포넌트 명의로 속도를 요청한다.
/// 다른 시스템(설정창 등)이 정지 요청 중이면 서비스가 더 느린 쪽을 적용하므로 서로 덮어쓰지 않는다.
/// </summary>
public class LevelUpTimeDirector : MonoBehaviour
{
    [Inject] private TimeScaleService _timeScale;

    [Tooltip("레벨업 순간 1배속 → 정지까지 서서히 느려지는 시간 (슬로우모션). 0이면 즉시 정지.")]
    [SerializeField, Min(0f)] private float _slowDownDuration = 0.35f;
    [SerializeField] private Ease _slowDownEase = Ease.OutQuad;
    [Tooltip("선택 후 정지 → 1배속까지 다시 빨라지는 시간. 0이면 즉시 재개.")]
    [SerializeField, Min(0f)] private float _speedUpDuration = 0.3f;
    [SerializeField] private Ease _speedUpEase = Ease.InQuad;

    private Tween _ramp;
    private float _requested = 1f; // 이 컴포넌트가 요청 중인 속도 (요청 없음 = 1)

    /// <summary>현재 요청 속도에서 0까지 서서히 낮추고, 정지하면 onStopped를 호출한다.</summary>
    public void SlowDownTime(Action onStopped)
    {
        KillRamp();
        if (_slowDownDuration <= 0f || _requested <= 0f)
        {
            SetRequest(0f);
            onStopped?.Invoke();
            return;
        }
        _ramp = DOTween.To(() => _requested, SetRequest, 0f, _slowDownDuration)
            .SetEase(_slowDownEase).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() => { _ramp = null; SetRequest(0f); onStopped?.Invoke(); });
    }

    /// <summary>즉시 정지 (리롤 등 연출 없이 멈춘 상태를 유지할 때).</summary>
    public void PauseNow()
    {
        KillRamp();
        SetRequest(0f);
    }

    /// <summary>정지 → 1배속까지 서서히 올린 뒤 요청을 해제한다.</summary>
    public void SpeedUpTime()
    {
        KillRamp();
        if (_speedUpDuration <= 0f) { ReleaseRequest(); return; }
        _ramp = DOTween.To(() => _requested, SetRequest, 1f, _speedUpDuration)
            .SetEase(_speedUpEase).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() => { _ramp = null; ReleaseRequest(); });
    }

    private void SetRequest(float value)
    {
        _requested = value;
        _timeScale?.Request(this, value);
    }

    private void ReleaseRequest()
    {
        _requested = 1f;
        _timeScale?.Release(this);
    }

    private void KillRamp()
    {
        if (_ramp != null && _ramp.IsActive()) _ramp.Kill();
        _ramp = null;
    }

    private void OnDestroy()
    {
        KillRamp();
        _timeScale?.Release(this);
    }
}
