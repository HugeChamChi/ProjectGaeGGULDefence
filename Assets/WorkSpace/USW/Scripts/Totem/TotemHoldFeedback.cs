using System;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// 배치된 토템을 누르고 있을 때의 피드백 (씬 수명 엔트리포인트).
/// - 누른 뒤 HoldFeedbackDelaySeconds(실제 시간)가 지나면 게임 속도를 HoldSlowTimeScale로 낮추고, 손을 떼면 되돌린다.
///   라운드 타이머·보스 패턴·유닛 전투가 모두 게임 시간 기준이라 같은 배율로 함께 느려진다.
/// - 모든 토템에 원형 게이지(TotemHoldRingView)를 띄워 이동 가능(MoveHoldSeconds)까지 남은 시간을 보여준다.
///   가득 차면 이동 모드 확정 표시(점 회전). 가득 차기 전에 끌면(회전 또는 무시) 게이지만 숨기고 슬로우는 유지한다.
/// 이동/회전 판정 자체는 DragHandler가 실제 시간으로 하며, 이 클래스는 홀드 상태·진행도·속도 요청만 담당한다.
/// </summary>
public sealed class TotemHoldFeedback : ILateTickable, IDisposable
{
    private readonly TotemInteractionSettings _settings;
    private readonly TimeScaleService _timeScale;
    private readonly TotemHoldRingView _ring;

    private TotemBase _totem;
    private SpriteRenderer _totemSprite;
    private bool _holding;
    private bool _showRing;
    private float _pressedAt;
    private float _armedAt = -1f;
    private float _slowScale = 1f;
    private float _lastProgress;
    private bool _disposed;

    /// <summary>씬 스코프 설정과 게임 속도 서비스를 받는다.</summary>
    public TotemHoldFeedback(TotemInteractionSettings settings, TimeScaleService timeScale)
    {
        _settings = settings;
        _timeScale = timeScale;
        _ring = new TotemHoldRingView(settings != null ? settings.HoldRingMaterial : null,
            settings != null ? settings.HoldRingWorldSize : 1f);
    }

    /// <summary>토템을 누른 순간.</summary>
    public void Begin(TotemBase totem)
    {
        if (_disposed || totem == null) return;
        _totem = totem;
        _totemSprite = totem.GetComponentInChildren<SpriteRenderer>();
        _holding = true;
        _showRing = true;
        _pressedAt = Time.unscaledTime;
        _armedAt = -1f;
        _lastProgress = 0f;
        _ring.ResetAppear();
    }

    /// <summary>끌기가 시작된 순간. 이동이면 가득 찬 게이지가 토템을 따라가고, 회전·무시면 게이지를 숨긴다.</summary>
    public void OnDragStarted(TotemBase totem, bool moving)
    {
        if (totem != _totem) return;
        if (!moving) _showRing = false;
    }

    /// <summary>손을 뗐거나 입력이 취소됐을 때. 속도는 램프로 복구된다.</summary>
    public void End(TotemBase totem)
    {
        if (totem != _totem) return;
        _holding = false;
        _showRing = false;
    }

    /// <inheritdoc />
    public void LateTick()
    {
        if (_disposed || _settings == null) return;
        if (_holding && _totem == null) { _holding = false; _showRing = false; } // 누르는 중 파괴됨

        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        float held = _holding ? now - _pressedAt : 0f;
        float delay = _settings.HoldFeedbackDelaySeconds;
        bool active = _holding && held >= delay;

        UpdateSlowMotion(active, dt);

        // 손을 뗀 뒤 사라지는 동안에는 마지막 채움 상태를 유지한다.
        float span = Mathf.Max(_settings.MoveHoldSeconds - delay, 0.01f);
        float progress = _holding ? Mathf.Clamp01((held - delay) / span) : _lastProgress;
        _lastProgress = progress;
        if (_holding && progress >= 1f && _armedAt < 0f) _armedAt = now;
        float armedAge = _armedAt < 0f ? -1f : now - _armedAt;

        _ring.Tick(active && _showRing, progress, armedAge, _totemSprite, _totem != null ? _totem.transform : null, now, dt);
    }

    private void UpdateSlowMotion(bool active, float dt)
    {
        float target = active ? _settings.HoldSlowTimeScale : 1f;
        float rate = Mathf.Max(1f - _settings.HoldSlowTimeScale, 0.01f) / _settings.HoldSlowRampSeconds;
        _slowScale = Mathf.MoveTowards(_slowScale, target, rate * dt);
        if (_slowScale < 0.999f) _timeScale?.Request(this, _slowScale);
        else { _slowScale = 1f; _timeScale?.Release(this); }
    }

    /// <summary>씬 종료 시 속도 요청을 해제하고 게이지 오브젝트를 정리한다.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timeScale?.Release(this);
        _ring.Destroy();
    }
}
