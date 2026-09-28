using DG.Tweening;
using GaeGGUL.Animation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 족장 스킬 버튼 초상화 연출. 충전 완료 시 가림 마스크가 위로 열리며 초상화가 틀 밖으로 튀어나오고
/// 대기(호흡) 모션을 재생한다. 쿨타임 중에는 마스크 윗변이 틀 윗선까지 닫혀 튀어나온 부분이 틀 안으로
/// 들어가고 호흡을 멈춘다. 스킬 판단은 Presenter가 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChiefSkillIconMotion : MonoBehaviour
{
    [Tooltip("움직일 초상화. 비우면 이 오브젝트")]
    [SerializeField] private RectTransform _icon;
    [Tooltip("충전 완료 중에만 재생할 대기 모션. 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private Anim_Breathing _idle;
    [Tooltip("초상화를 가리는 마스크(RectMask2D, 아래쪽 피벗). 비우면 부모에서 찾음. 충전 시 높이 = 씬에 배치한 높이")]
    [SerializeField] private RectTransform _mask;

    [Header("쿨타임 상태")]
    [Tooltip("쿨타임 중 마스크 높이 (틀 윗선까지). 0 이하면 마스크를 움직이지 않음")]
    [SerializeField] private float _cooldownMaskHeight = 134f;
    [Tooltip("쿨타임 중 초상화 추가 이동 (충전 위치 기준, 음수 = 아래). 0이면 제자리")]
    [SerializeField] private float _cooldownOffsetY;

    [Header("연출")]
    [SerializeField, Min(0f)] private float _openDuration = 0.35f;
    [SerializeField, Min(0f)] private float _closeDuration = 0.15f;
    [SerializeField] private Ease _openEase = Ease.OutBack;
    [SerializeField] private Ease _closeEase = Ease.InQuad;
    [Tooltip("충전 완료 순간 초상화가 이만큼 아래에서 튀어 오른다 (0이면 없음)")]
    [SerializeField, Min(0f)] private float _popDistance = 14f;
    [Tooltip("마스크가 닫힐 때 초상화가 눌리는 스케일 반동 (0이면 없음)")]
    [SerializeField, Range(0f, 0.3f)] private float _closePunch = 0.06f;

    private Vector2 _chargedPosition;
    private float _openMaskHeight;
    private bool _initialized;
    private bool _hasState;
    private bool _charged;
    private Sequence _sequence;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        if (_icon == null) _icon = transform as RectTransform;
        if (_idle == null) _idle = GetComponent<Anim_Breathing>();
        if (_mask == null)
        {
            var rectMask = GetComponentInParent<RectMask2D>(true);
            if (rectMask != null) _mask = rectMask.rectTransform;
        }
        if (_icon != null) _chargedPosition = _icon.anchoredPosition;
        if (_mask != null) _openMaskHeight = _mask.sizeDelta.y;
    }

    private bool UsesMask => _mask != null && _cooldownMaskHeight > 0f;

    /// <summary>충전 상태가 바뀔 때만 마스크 열림/닫힘을 재생한다. 게임 시간 정지와 독립적이다.</summary>
    public void SetCharged(bool charged, bool immediate = false)
    {
        Initialize();
        if (_icon == null || (_hasState && _charged == charged && !immediate)) return;
        _hasState = true;
        _charged = charged;
        _sequence?.Kill();
        _sequence = null;
        _idle?.SetStatusPaused(!charged);

        Vector2 iconTarget = charged ? _chargedPosition : _chargedPosition + new Vector2(0f, _cooldownOffsetY);
        float maskTarget = charged ? _openMaskHeight : _cooldownMaskHeight;

        if (immediate || !isActiveAndEnabled)
        {
            _icon.anchoredPosition = iconTarget;
            if (UsesMask) SetMaskHeight(maskTarget);
            return;
        }

        float duration = charged ? _openDuration : _closeDuration;
        Ease ease = charged ? _openEase : _closeEase;
        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        if (charged && _popDistance > 0f)
            _icon.anchoredPosition = iconTarget - new Vector2(0f, _popDistance);
        _sequence.Join(_icon.DOAnchorPos(iconTarget, duration).SetEase(ease));

        if (UsesMask)
            _sequence.Join(DOTween.To(() => _mask.sizeDelta.y, SetMaskHeight, maskTarget, duration).SetEase(ease));

        if (!charged && _closePunch > 0f)
            _sequence.Append(_icon.DOPunchScale(new Vector3(_closePunch, -_closePunch, 0f), 0.2f, 6, 0.5f));
    }

    private void SetMaskHeight(float height)
    {
        Vector2 size = _mask.sizeDelta;
        size.y = height;
        _mask.sizeDelta = size;
    }

    // 같은 대상의 Anim_Breathing이 활성화/비활성화 때 DOKill로 트윈을 끊을 수 있으므로 현재 상태로 맞춘다.
    private void OnEnable()
    {
        if (_hasState) SetCharged(_charged, immediate: true);
    }

    private void OnDestroy() => _sequence?.Kill();

#if UNITY_EDITOR
    // 플레이 중 인스펙터에서 값을 바꾸면 현재 상태에 즉시 반영해 눈으로 보며 조정할 수 있게 한다.
    private void OnValidate()
    {
        if (Application.isPlaying && _hasState && _icon != null) SetCharged(_charged, immediate: true);
    }
#endif
}
