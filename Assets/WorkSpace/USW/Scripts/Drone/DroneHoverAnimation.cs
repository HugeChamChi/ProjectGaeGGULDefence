using DG.Tweening;
using UnityEngine;

/// <summary>
/// 드론 호버링 애니메이션 — 드론 프리팹에 부착.
/// DOTween으로 상하 부유 + 미세 회전을 반복해 "떠다니는" 느낌을 연출한다.
/// </summary>
public class DroneHoverAnimation : MonoBehaviour
{
    [Header("부유 (상하)")]
    [SerializeField] private float _hoverAmplitude = 3f;   // px 단위 이동 폭
    [SerializeField] private float _hoverDuration  = 1.8f; // 위→아래 한 번 시간(초)

    [Header("회전 흔들림")]
    [SerializeField] private float _tiltAngle    = 2f;   // 최대 기울기(도)
    [SerializeField] private float _tiltDuration = 2.2f;

    [Header("위상 랜덤 오프셋")]
    [Tooltip("드론마다 타이밍을 다르게 해 동기화 어색함 방지")]
    [SerializeField] private bool _randomPhase = true;

    private RectTransform _rt;
    private Sequence      _seq;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
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
        _seq?.Kill();
    }

    private void PlayHover()
    {
        if (_rt == null) return;

        _seq?.Kill();
        _seq = DOTween.Sequence();

        float phaseDelay = _randomPhase ? Random.Range(0f, _hoverDuration) : 0f;

        // ── 상하 부유 ─────────────────────────────────────────
        Vector2 basePos = _rt.anchoredPosition;

        _seq.Insert(0f,
            _rt.DOAnchorPosY(basePos.y + _hoverAmplitude, _hoverDuration)
               .SetEase(Ease.InOutSine)
               .SetLoops(-1, LoopType.Yoyo)
               .SetDelay(phaseDelay)
        );

        // ── 미세 회전 ─────────────────────────────────────────
        _seq.Insert(0f,
            _rt.DOLocalRotate(new Vector3(0f, 0f, _tiltAngle), _tiltDuration)
               .SetEase(Ease.InOutSine)
               .SetLoops(-1, LoopType.Yoyo)
               .From(new Vector3(0f, 0f, -_tiltAngle))
               .SetDelay(phaseDelay * 0.5f)
        );

        _seq.SetLink(gameObject);
    }

    private void OnDestroy()
    {
        _seq?.Kill();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && _rt != null)
            PlayHover();
    }
#endif
}
