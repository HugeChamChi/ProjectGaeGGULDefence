using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// UI를 누르고 있을 때 특정 CanvasGroup의 Alpha를 조절하여 
/// 배경(필드)을 확인할 수 있게 하는 유틸리티 컴포넌트입니다.
/// </summary>
public class UI_Peekthrough : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Settings")]
    [SerializeField] private CanvasGroup _targetGroup;      // 투명도를 조절할 대상 (미지정 시 부모에서 탐색)
    [SerializeField] private float _peekAlpha = 0.2f;      // 눌렀을 때 목표 Alpha
    [SerializeField] private float _fadeDuration = 0.2f;    // 페이드 시간

    private Tween _fadeTween;
    private object _peekOwner;
    private int? _pointerId;

    /// <summary>한 입력만 필드 보기를 소유하도록 하고 일시정지 중에도 투명도를 낮춘다.</summary>
    public bool TryBeginPeek(object owner)
    {
        if (_targetGroup == null || owner == null || _peekOwner != null) return false;
        _peekOwner = owner;
        KillTween();
        _fadeTween = _targetGroup.DOFade(_peekAlpha, _fadeDuration).SetUpdate(true);
        return true;
    }

    /// <summary>필드 보기를 시작한 입력이 끝나면 원래 표시로 복구한다.</summary>
    public void EndPeek(object owner)
    {
        if (!ReferenceEquals(_peekOwner, owner)) return;
        _peekOwner = null;
        KillTween();
        if (_targetGroup != null)
            _fadeTween = _targetGroup.DOFade(1f, _fadeDuration).SetUpdate(true);
    }

    private void Awake()
    {
        if (_targetGroup == null)
            _targetGroup = GetComponentInParent<CanvasGroup>();
    }

    /// <summary>
    /// 클릭 시 Alpha를 낮춤
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && TryBeginPeek(this))
            _pointerId = eventData.pointerId;
    }

    /// <summary>
    /// 뗐을 때 Alpha를 복구
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId) return;
        _pointerId = null;
        EndPeek(this);
    }

    private void OnDisable()
    {
        bool wasPeeking = _peekOwner != null || (_fadeTween != null && _fadeTween.IsActive());
        _peekOwner = null;
        _pointerId = null;
        KillTween();
        if (wasPeeking && _targetGroup != null) _targetGroup.alpha = 1f;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) OnDisable();
    }

    private void KillTween()
    {
        if (_fadeTween != null && _fadeTween.IsActive())
            _fadeTween.Kill();
    }

    private void OnDestroy()
    {
        KillTween();
    }
}
