using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 유닛 클릭 시 나타나는 액션 팝업 컨테이너
///
/// ─ 역할 ──────────────────────────────────────────────────
/// 위치 계산, Show/Hide 애니메이션, 외부 클릭 감지 담당
/// 버튼별 동작은 MergeButtonUI / SellButtonUI에 위임
///
/// ─ Scene 구성 ─────────────────────────────────────────────
/// Canvas
///   └── UnitActionPopup  ← 이 스크립트 부착
///         ├── MergeButton  ← MergeButtonUI 부착
///         └── SellButton   ← SellButtonUI 부착
///
/// ─ Inspector 연결 ─────────────────────────────────────────
///   mergeButton → 자식 MergeButton의 MergeButtonUI 컴포넌트
///   sellButton  → 자식 SellButton의 SellButtonUI 컴포넌트
///   rootCanvas  → 씬의 루트 Canvas
/// </summary>
public class UnitActionPopupUI : InGameSingleton<UnitActionPopupUI>
{
    [SerializeField] private MergeButtonUI mergeButton;
    [SerializeField] private SellButtonUI  sellButton;
    [SerializeField] private Canvas        rootCanvas;

    private RectTransform _rect;
    private Tweener       _tween;
    private bool          _isShowing;
    private bool          _justShown;

    protected override void Awake()
    {
        base.Awake();
        _rect = GetComponent<RectTransform>();
        _rect.localScale = Vector3.zero;
    }

    // ── 표시 ───────────────────────────────────────────────────

    public void Show(UnitBase unit, bool canMerge)
    {
        _justShown = true;

        mergeButton.SetState(canMerge);
        sellButton.SetUnit(unit);

        PositionAtUnitTopRight(unit.GetComponent<RectTransform>());

        _tween?.Kill();
        _rect.localScale = Vector3.zero;
        _tween = _rect.DOScale(Vector3.one, 0.25f)
                      .SetEase(Ease.OutBack)
                      .SetUpdate(true);

        _isShowing = true;
    }

    // ── 숨기기 ─────────────────────────────────────────────────

    public void Hide()
    {
        if (!_isShowing) return;
        _isShowing = false;

        _tween?.Kill();
        _tween = _rect.DOScale(Vector3.zero, 0.18f)
                      .SetEase(Ease.InBack)
                      .SetUpdate(true);
    }

    // ── 외부 클릭 감지 ─────────────────────────────────────────

    private void LateUpdate()
    {
        if (_justShown) { _justShown = false; return; }
        if (!_isShowing) return;
        if (!Input.GetMouseButtonDown(0)) return;

        var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);

        foreach (var r in results)
        {
            if (r.gameObject.transform.IsChildOf(transform))
                return;
        }

        Manager.Merge.HideButton();
    }

    // ── 위치 계산 ──────────────────────────────────────────────

    private void PositionAtUnitTopRight(RectTransform unitRect)
    {
        if (unitRect == null || rootCanvas == null) return;

        Vector3[] corners = new Vector3[4];
        unitRect.GetWorldCorners(corners);
        // corners[2] = TopRight

        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(), screenPoint, cam, out Vector2 localPoint);

        _rect.anchoredPosition = localPoint;
    }
}
