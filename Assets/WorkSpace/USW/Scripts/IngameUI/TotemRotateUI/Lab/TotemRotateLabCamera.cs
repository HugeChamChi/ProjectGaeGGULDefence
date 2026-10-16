using UnityEngine;

/// <summary>
/// 회전 모드 카메라 연출(사용자 10-15 확정: 포커스 줌 또는 스포트라이트). 인게임과 같은 원근 카메라를 움직인다.
/// - 포커스 줌: 토템을 고정점으로 카메라를 토템 쪽으로 당긴다(토템이 화면에서 안 움직여 손가락 밑에 그대로). 확정 시 아주 짧게 흔들림.
/// - 스포트라이트: 카메라는 그대로 두고 실험실이 화면 어둠 오버레이만 띄운다.
/// 실제 시간(unscaled)으로 움직인다.
/// </summary>
public sealed class TotemRotateLabCamera
{
    /// <summary>연출 종류(실험실 버튼 순서).</summary>
    public const int Focus = 0, Spotlight = 1;
    private const float Stiffness = 120f;
    private const float Damping = 22f;
    private const float MaxStep = 1f / 120f;
    private const float MaxStepTotal = 0.1f;

    private readonly Camera _camera;
    private readonly Vector3 _authoredPosition;
    private readonly Quaternion _authoredRotation;
    private float _zoom = 1f, _zoomVelocity;
    private float _shake;

    /// <summary>포커스 줌 배율.</summary>
    public float FocusZoom { get; set; } = 1.3f;
    /// <summary>확정 흔들림 세기(월드 단위).</summary>
    public float CommitShake { get; set; } = 0.02f;

    /// <summary>씬에 놓인 카메라 위치·회전을 기준으로 삼는다.</summary>
    public TotemRotateLabCamera(Camera camera)
    {
        _camera = camera;
        _authoredPosition = camera.transform.position;
        _authoredRotation = camera.transform.rotation;
    }

    /// <summary>회전을 확정한 순간.</summary>
    public void OnCommitted(int mode)
    {
        if (mode == Focus) _shake = 1f;
    }

    /// <summary>매 프레임. active면 mode의 목표로, 아니면 원위치로 스프링 이동한다.</summary>
    public void Tick(int mode, bool active, Vector3 pivot, float dt)
    {
        float target = active && mode == Focus ? FocusZoom : 1f;
        // 프레임이 길게 끊겨도(에디터 멈춤·캡처) 스프링이 튀지 않게 잘게 나눠 적분한다.
        float remaining = Mathf.Min(dt, MaxStepTotal);
        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, MaxStep);
            _zoomVelocity += (Stiffness * (target - _zoom) - Damping * _zoomVelocity) * step;
            _zoom += _zoomVelocity * step;
            remaining -= step;
        }
        _zoom = Mathf.Clamp(_zoom, 1f, Mathf.Max(1f, FocusZoom) * 1.2f);
        _shake = Mathf.Max(0f, _shake - dt * 7f);
        var offset = (_authoredPosition - pivot) / Mathf.Max(0.01f, _zoom);
        var shake = _shake > 0f ? (Vector3)(Random.insideUnitCircle * CommitShake * _shake) : Vector3.zero;
        _camera.transform.SetPositionAndRotation(pivot + offset + shake, _authoredRotation);
    }

    /// <summary>원래 위치로 즉시 되돌린다.</summary>
    public void Restore() => _camera.transform.SetPositionAndRotation(_authoredPosition, _authoredRotation);
}
