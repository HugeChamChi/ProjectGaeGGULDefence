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
    private BaseInput _input;
    private bool _selectionBlocked;
    private bool _restoreInteractable;
    private int _releaseAfterFrame;

    /// <summary>필드 보기와 복귀 중에는 모든 선택 입력을 잠근다.</summary>
    public bool BlocksSelection => _selectionBlocked;

    /// <summary>패널의 입력 잠금 중에도 필드 보기를 시작한 카드의 홀드를 유지한다.</summary>
    public bool OwnsPeek(object owner) => owner != null && ReferenceEquals(_peekOwner, owner);

    /// <summary>한 입력만 필드 보기를 소유하도록 하고 일시정지 중에도 투명도를 낮춘다.</summary>
    public bool TryBeginPeek(object owner)
    {
        if (!isActiveAndEnabled || _targetGroup == null || owner == null || _selectionBlocked ||
            !_targetGroup.interactable) return false;
        _peekOwner = owner;
        _input = EventSystem.current != null ? EventSystem.current.currentInputModule?.input : null;
        _restoreInteractable = _targetGroup.interactable;
        _selectionBlocked = true;
        // Raycasts stay blocked so the second finger cannot reach field units either.
        _targetGroup.interactable = false;
        KillTween();
        _fadeTween = _targetGroup.DOFade(_peekAlpha, _fadeDuration).SetUpdate(true);
        return true;
    }

    /// <summary>필드 보기를 시작한 입력이 끝나면 원래 표시로 복구한다.</summary>
    public void EndPeek(object owner)
    {
        if (!ReferenceEquals(_peekOwner, owner)) return;
        _peekOwner = null;
        _releaseAfterFrame = Time.frameCount;
        KillTween();
        if (_targetGroup != null)
            _fadeTween = _targetGroup.DOFade(1f, _fadeDuration).SetUpdate(true);
    }

    private void LateUpdate()
    {
        if (!_selectionBlocked || _peekOwner != null || Time.frameCount <= _releaseAfterFrame) return;
        if (_fadeTween != null && _fadeTween.IsActive() && _fadeTween.IsPlaying()) return;
        // A Button may still receive PointerClick after its original PointerDown was blocked.
        // Wait for every touch and the release frame to finish before allowing a new tap.
        bool pointerHeld = _input != null
            ? _input.touchCount > 0 || _input.GetMouseButton(0)
            : Input.touchCount > 0 || Input.GetMouseButton(0);
        if (!pointerHeld) RestoreInput();
    }

    private void RestoreInput()
    {
        if (_selectionBlocked && _targetGroup != null)
            _targetGroup.interactable = _restoreInteractable;
        _selectionBlocked = false;
        _input = null;
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
        bool wasPeeking = _selectionBlocked || _peekOwner != null || (_fadeTween != null && _fadeTween.IsActive());
        _peekOwner = null;
        _pointerId = null;
        KillTween();
        RestoreInput();
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
        _fadeTween = null;
    }

    private void OnDestroy()
    {
        KillTween();
    }
}
