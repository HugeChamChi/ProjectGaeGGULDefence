using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// 유닛/토템 공용 드래그 핸들러
/// - 빈 셀로 드롭 → 이동
/// - 점유 셀로 드롭 → 위치 스왑 (유닛↔유닛 / 유닛↔토템 / 토템↔토템)
/// - 이동 불가 시 원래 위치 복귀
///
/// 변경 사항:
///   - CurrencyManager.Instance / BossManager.Instance → Manager.Currency / Manager.Boss
///   - BossMonster → BossBase 타입 변경
///   - 봉인된 셀(IsSealed)에는 배치 불가 처리
/// </summary>
public class DragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private RectTransform _rect;
    private Canvas        _canvas;

    private GridCell  _originCell;
    private Vector2   _originAnchoredPos;
    private Transform _originParent;

    private UnitBase  _unit;
    private TotemBase _totem;

    private bool _isDragging = false;

    private void Awake()
    {
        _rect  = GetComponent<RectTransform>();
        _unit  = GetComponent<UnitBase>();
        _totem = GetComponent<TotemBase>();
    }

    private Canvas GetCanvas()
    {
        if (_canvas != null) return _canvas;
        var c = GetComponentInParent<Canvas>();
        if (c != null) _canvas = c.rootCanvas;
        return _canvas;
    }

    public void SetOriginCell(GridCell cell)
    {
        _originCell = cell;
    }

    // ── 클릭 (드래그 없을 때만 발생) ──────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_unit != null && Manager.Merge != null)
            Manager.Merge.OnUnitClicked(_unit);
    }

    // ── 드래그 시작 ────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 드래그 시작 시 합성 버튼 닫기
        Manager.Merge?.HideButton();

        _isDragging = false;
        if (_originCell == null) return;

        var canvas = GetCanvas();
        if (canvas == null)
        {
            Debug.LogWarning($"DragHandler({name}): Canvas를 찾지 못했습니다.");
            return;
        }

        _originParent      = _rect.parent;
        _originAnchoredPos = _rect.anchoredPosition;

        _rect.SetParent(canvas.transform, true);
        _rect.SetAsLastSibling();

        _isDragging = true;
    }

    // ── 드래그 중 ──────────────────────────────────────────────
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        var canvas = GetCanvas();
        if (canvas == null) return;

        _rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    // ── 드래그 종료 ────────────────────────────────────────────
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) { ReturnToOrigin(); return; }
        _isDragging = false;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        GridCell targetCell = null;
        foreach (var r in results)
        {
            var cell = r.gameObject.GetComponent<GridCell>();
            if (cell != null && cell != _originCell)
            {
                targetCell = cell;
                break;
            }
        }

        if (targetCell == null)            { ReturnToOrigin(); return; }

        // 봉인된 셀에는 배치 불가
        if (!targetCell.Model.IsAvailable) { ReturnToOrigin(); return; }

        if (targetCell.IsOccupied) TrySwap(targetCell);
        else                       MoveToEmpty(targetCell);
    }

    // ── 빈 셀 이동 ─────────────────────────────────────────────
    private void MoveToEmpty(GridCell targetCell)
    {
        if (_unit  != null) _originCell.RemoveUnit();
        if (_totem != null) { _originCell.RemoveTotem(); _totem.OnRemoved(); }

        PlaceSelfAt(targetCell);
    }

    // ── 스왑 처리 ──────────────────────────────────────────────
    private void TrySwap(GridCell targetCell)
    {
        UnitBase  targetUnit  = targetCell.OccupyingUnit;
        TotemBase targetTotem = targetCell.OccupyingTotem;

        DragHandler targetDrag = null;
        if (targetUnit  != null) targetDrag = targetUnit .GetComponent<DragHandler>();
        if (targetTotem != null) targetDrag = targetTotem.GetComponent<DragHandler>();

        if (targetDrag == null) { ReturnToOrigin(); return; }

        var myOriginalCell = _originCell;

        if (_unit  != null) _originCell.RemoveUnit();
        if (_totem != null) { _originCell.RemoveTotem(); _totem.OnRemoved(); }

        if (targetUnit  != null) targetCell.RemoveUnit();
        if (targetTotem != null) { targetCell.RemoveTotem(); targetTotem.OnRemoved(); }

        PlaceSelfAt(targetCell);
        targetDrag.PlaceSelfAt(myOriginalCell);
    }

    // ── 지정 셀에 자신을 배치 ──────────────────────────────────
    public void PlaceSelfAt(GridCell cell)
    {
        if (_unit != null)
        {
            cell.TryPlaceUnit(_unit);
            _rect.SetParent(cell.transform, false);
            _rect.anchoredPosition = Vector2.zero;
            _originCell = cell;

            _unit.OnRemoved();
            // Manager 접근 통일 + 셀 참조 전달
            _unit.OnPlaced(Manager.Currency, Manager.Boss.CurrentBoss, cell);
        }

        if (_totem != null)
        {
            cell.TryPlaceTotem(_totem);
            _rect.SetParent(cell.transform, false);
            _rect.anchoredPosition = Vector2.zero;
            _originCell = cell;

            _totem.OnPlaced(cell);
        }
    }

    // ── 원래 위치로 복귀 ───────────────────────────────────────
    private void ReturnToOrigin()
    {
        if (_originParent == null) return;
        _rect.SetParent(_originParent, false);
        _rect.anchoredPosition = _originAnchoredPos;
    }
}
