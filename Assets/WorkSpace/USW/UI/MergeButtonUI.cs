using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 합성 버튼 UI
///
/// ─ Scene 구성 ─────────────────────────────────────────
/// Canvas
///   └── MergeButton  ← 이 GameObject에 MergeButtonUI 컴포넌트 부착
///         └── Text   ← "합성"
///
/// ─ Inspector 연결 ──────────────────────────────────────
///   buttonImage → 이 오브젝트의 Image 컴포넌트
///   rootCanvas  → 씬의 루트 Canvas
///
/// ─ Button 컴포넌트 OnClick ────────────────────────────
///   → MergeButtonUI.OnMergeButtonClicked() 연결
///
/// ─ 버튼이 닫히는 조건 ─────────────────────────────────
///   1. 합성 버튼 클릭 (합성 실행 또는 비활성 상태로 클릭)
///   2. 같은 유닛 재클릭 (DragHandler → MergeManager 토글)
///   3. 드래그 시작 (DragHandler.OnBeginDrag)
///   4. 버튼 외부 아무 곳이나 클릭 (LateUpdate 감지)
///      → 그리드, 빈 공간 등 모두 포함, 화면 블로킹 없음
/// </summary>
public class MergeButtonUI : InGameSingleton<MergeButtonUI>
{
    [SerializeField] private Image  buttonImage;
    [SerializeField] private Canvas rootCanvas;

    [SerializeField] private Color colorActive   = Color.white;                        // FFFFFF
    [SerializeField] private Color colorInactive = new Color(0.369f, 0.369f, 0.369f, 1f); // 5E5E5E

    private RectTransform _rect;
    private Tweener       _tween;
    private bool          _isShowing;
    private bool          _justShown;   // 클릭으로 열린 직후 프레임에 바로 닫히는 것 방지

    protected override void Awake()
    {
        base.Awake();
        _rect = GetComponent<RectTransform>();
        // SetActive(false) 쓰지 않음 — 비활성화하면 Awake가 안 돌아 싱글턴 등록 불가
        // scale 0으로 초기 숨김
        _rect.localScale = Vector3.zero;
    }

    // ── 표시 ───────────────────────────────────────────────────

    public void Show(UnitBase unit, bool canMerge)
    {
        _justShown = true;

        PositionAtUnitTopRight(unit.GetComponent<RectTransform>());
        buttonImage.color = canMerge ? colorActive : colorInactive;

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

    // ── 외부 클릭 감지 (Blocker 없이) ─────────────────────────

    private void LateUpdate()
    {
        // 열린 직후 프레임은 건너뜀 (열자마자 닫히는 현상 방지)
        if (_justShown) { _justShown = false; return; }
        if (!_isShowing) return;
        if (!Input.GetMouseButtonDown(0)) return;

        // 이번 클릭이 이 버튼 위인지 확인
        var pointer = new PointerEventData(EventSystem.current)
                      { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);

        foreach (var r in results)
        {
            if (r.gameObject.transform.IsChildOf(transform))
                return; // 버튼 영역 클릭 → 닫지 않음
        }

        // 버튼 외부 클릭 → 닫기
        Manager.Merge.HideButton();
    }

    // ── 버튼 OnClick (Inspector에서 연결) ─────────────────────

    public void OnMergeButtonClicked()
    {
        Manager.Merge.ExecuteMerge();
    }

    // ── 위치 계산 ──────────────────────────────────────────────

    private void PositionAtUnitTopRight(RectTransform unitRect)
    {
        if (unitRect == null || rootCanvas == null) return;

        // 유닛 RectTransform의 우상단 월드 좌표
        Vector3[] corners = new Vector3[4];
        unitRect.GetWorldCorners(corners);
        // corners[2] = TopRight

        // 월드 좌표 → 스크린 좌표 → 캔버스 로컬 좌표
        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(), screenPoint, cam, out Vector2 localPoint);

        _rect.anchoredPosition = localPoint;
    }
}
