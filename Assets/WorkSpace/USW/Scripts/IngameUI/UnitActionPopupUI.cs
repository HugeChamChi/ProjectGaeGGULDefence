using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 유닛 클릭 시 나타나는 액션 팝업.
///
/// 외부 API
///   Show(unit, canMerge) — 팝업 표시
///   Hide()               — 팝업 숨기기
///   OnDismissRequested   — 외부 클릭으로 닫힘 요청 시 발행 (InGameInstaller가 구독)
///
/// 내부 구현(애니메이션·위치·클릭 감지)은 외부에 노출하지 않음.
/// </summary>
public class UnitActionPopupUI : MonoBehaviour
{
    [SerializeField] private MergeButtonUI mergeButton;
    [SerializeField] private SellButtonUI  sellButton;
    [SerializeField] private Canvas        rootCanvas;

    public event Action OnDismissRequested;

    private RectTransform _rect;
    private Tweener       _tween;
    private bool          _isShowing;
    private bool          _justShown;

    private void Awake()
    {
        _rect            = GetComponent<RectTransform>();
        _rect.localScale = Vector3.zero;
    }

    public void Show(UnitBase unit, bool canMerge)
    {
        if (_rect == null) { Debug.LogError("[UnitActionPopupUI] _rect is null — RectTransform 없음"); return; }
        if (mergeButton == null) { Debug.LogError("[UnitActionPopupUI] mergeButton 미연결 (Inspector 확인)"); return; }
        if (sellButton  == null) { Debug.LogError("[UnitActionPopupUI] sellButton 미연결 (Inspector 확인)"); return; }
        _justShown = true;

        mergeButton.SetState(canMerge);
        sellButton.SetUnit(unit);
        if (rootCanvas == null) Debug.LogWarning("[UnitActionPopupUI] rootCanvas 미연결 — 팝업 위치 (0,0) 고정");
        PositionAtCenter(unit.GetComponent<RectTransform>());

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
        _isShowing = false;

        _tween?.Kill();
        _tween = _rect.DOScale(Vector3.zero, 0.18f)
                      .SetEase(Ease.InBack)
                      .SetUpdate(true);
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
        if (targetRect == null || rootCanvas == null) return;

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
