using DG.Tweening;
using UnityEngine;

/// <summary>
/// 드론 호버링 애니메이션 — 드론 프리팹에 부착.
/// DOTween으로 상하 부유 + 미세 회전을 반복해 "떠다니는" 느낌을 연출한다.
/// </summary>
public class DroneHoverAnimation : MonoBehaviour
{
    [Header("부유 (상하)")]
    [Tooltip("월드 단위(Unit) 이동 폭")]
    [SerializeField] private float _hoverAmplitude = 0.08f;
    [SerializeField] private float _hoverDuration  = 1.8f; // 위→아래 한 번 시간(초)

    [Header("회전 흔들림")]
    [SerializeField] private float _tiltAngle    = 2f;   // 최대 기울기(도)
    [SerializeField] private float _tiltDuration = 2.2f;

    [Header("위상 랜덤 오프셋")]
    [Tooltip("드론마다 타이밍을 다르게 해 동기화 어색함 방지")]
    [SerializeField] private bool _randomPhase = true;

    private Transform _tr;
    private Tween         _hoverTween;
    private Tween         _tiltTween;

    private void Awake()
    {
        _tr = transform;
    }

    private void OnEnable()
    {
        PlayHover();
    }

    /// <summary>Initialize 등 외부에서 위치 확정 후 명시적으로 호출 — basePos를 현재 위치로 리셋.</summary>
    public void Play()
    {
        PlayHover();
    }

    private void OnDisable()
    {
        _hoverTween?.Kill();
        _tiltTween?.Kill();
    }

    private void PlayHover()
    {
        if (_tr == null) return;

        _hoverTween?.Kill();
        _tiltTween?.Kill();

        float phaseDelay = _randomPhase ? Random.Range(0f, _hoverDuration) : 0f;
        Vector3 basePos  = _tr.localPosition;

        // DOTween Sequence 안에서는 SetLoops(-1) 금지 — 두 트윈을 독립 실행
        _hoverTween = _tr.DOLocalMoveY(basePos.y + _hoverAmplitude, _hoverDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetDelay(phaseDelay)
            .SetLink(gameObject);

        _tiltTween = _tr.DOLocalRotate(new Vector3(0f, 0f, _tiltAngle), _tiltDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .From(new Vector3(0f, 0f, -_tiltAngle))
            .SetDelay(phaseDelay * 0.5f)
            .SetLink(gameObject);
    }

    private void OnDestroy()
    {
        _hoverTween?.Kill();
        _tiltTween?.Kill();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && _tr != null)
            PlayHover();
    }
#endif
}
