using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 토템 클릭 시 나타나는 액션 팝업.
///
/// 외부 API
///   Show(totem)          — 팝업 표시
///   Hide()               — 팝업 숨기기
///   OnDismissRequested   — 외부 클릭으로 닫힘 요청 시 발행
///   OnSellTotemRequested — 판매 버튼 클릭 시 발행
///
/// 씬 구조
///   TotemActionPopup  ← 이 컴포넌트, Pivot (0.5, 0.5)
///     ├── RotateButton  ← Button, anchoredPosition (-60, -100) = 7시
///     └── SellButton    ← Button, anchoredPosition ( 60, -100) = 5시
/// </summary>
public class TotemActionPopupUI : MonoBehaviour
{
    [SerializeField] private RotateButtonUI rotateButton;
    [SerializeField] private SellTotemButtonUI sellButton;
    [SerializeField] private Canvas rootCanvas;

    public event Action              OnDismissRequested;
    public event Action<TotemBase>   OnSellTotemRequested;

    private RectTransform _rect;
    private Tweener       _tween;
    private bool          _isShowing;
    private bool          _justShown;
    private TotemBase     _currentTotem;

    private void Awake()
    {
        _rect            = GetComponent<RectTransform>();
        _rect.localScale = Vector3.zero;
    }

    public void Show(TotemBase totem)
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_rect == null) return;
        _justShown    = true;
        _currentTotem = totem;

        rotateButton.SetTotem(totem);
        sellButton.SetTotem(totem);

        gameObject.SetActive(true);
        PositionAtCenter(totem.GetComponent<RectTransform>());

        _tween?.Kill();
        _rect.localScale = Vector3.zero;
        _tween = _rect.DOScale(Vector3.one, 0.25f)
                      .SetEase(Ease.OutBack)
                      .SetUpdate(true);

        _isShowing = true;
    }

    public void Hide()
    {
        if (!_isShowing) return;
        _isShowing    = false;
        _currentTotem = null;

        _tween?.Kill();
        _tween = _rect.DOScale(Vector3.zero, 0.18f)
                      .SetEase(Ease.InBack)
                      .SetUpdate(true)
                      .OnComplete(() => gameObject.SetActive(false));
    }

    internal void RaiseSellRequested(TotemBase totem)
    {
        OnSellTotemRequested?.Invoke(totem);
    }

    private void LateUpdate()
    {
        if (_justShown) { _justShown = false; return; }
        if (!_isShowing || !Input.GetMouseButtonDown(0)) return;

        var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);

        foreach (var r in results)
        {
            if (r.gameObject.transform.IsChildOf(transform)) return;
        }

        OnDismissRequested?.Invoke();
    }

    private void PositionAtCenter(RectTransform targetRect)
    {
        if (targetRect == null || rootCanvas == null || _rect == null) return;

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) / 2f;

        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : rootCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, center);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(), screenPoint, cam, out Vector2 localPoint);

        _rect.anchoredPosition = localPoint;
    }
}
