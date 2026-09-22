using UnityEngine;
using VContainer;
using System.Collections.Generic;
using System;

/// <summary>
/// 유닛/토템 공용 드래그 핸들러 (World Space / Physics Raycast 기반)
/// - InputManager가 IDraggable 인터페이스를 통해 호출
/// - 의존성을 낮추기 위해 주요 액션 시 event를 발행합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class DragHandler : MonoBehaviour, IDraggable
{
    [Inject] private CurrencyManager _currencyManager;
    [Inject] private BossManager _bossManager;
    [Inject] private UnitFactory _unitFactory;
    [Inject] private MergeManager _mergeManager;
    [Inject] private GridManager _gridManager;
    [Inject] private TotemRotationUI _rotationUI;
    [Inject] private TotemInteractionSettings _totemInteractionSettings;
    private float _pressStartedAt;
    private bool _rotating;
    private bool _isDragging;

    /// <summary>드래그 시작 위치에서의 이동량입니다. 발밑 상태 표시가 같은 간격으로 따라갑니다.</summary>
    public Vector3 DragOffset => _isDragging ? transform.position - _originPos : Vector3.zero;

    /// <summary>Records hold time without changing unit drag behavior.</summary>
    public void BeginPress()
    {
        _pressStartedAt = Time.unscaledTime;
        if (_totem != null) _gridManager?.ShowTotemRangePreview(_totem);
    }

    /// <summary>손을 뗐거나 입력이 취소되면 토템 범위를 숨긴다.</summary>
    public void EndPress()
    {
        _isDragging = false;
        SetDropPreview(null);
        if (_totem != null) _gridManager?.ClearTotemRangePreview();
    }

    /// <summary>Cancels interrupted touch movement or rotation.</summary>
    public void CancelPointerDrag()
    {
        if (_rotating) _rotationUI?.Cancel();
        else if (_isDragging)
        {
            ReturnToOrigin();
            if (_spriteRenderer != null) { _spriteRenderer.sortingLayerName = _originSortingLayer; _spriteRenderer.sortingOrder = _originSortingOrder; }
        }
        _rotating = false;
        EndPress();
    }

    public static event Action<UnitBase> OnUnitClickedEvent;
    public static event Action<TotemBase> OnTotemClickedGlobal;
    public static event Action OnDragStartedEvent;

    private GridCell  _originCell;
    private Vector3   _originPos;
    private int       _originSortingOrder;
    private string    _originSortingLayer;

    private UnitBase  _unit;
    private TotemBase _totem;
    private SpriteRenderer _spriteRenderer;
    private GridCell _dropPreviewCell;
    private readonly List<RaycastHit2D> _dropHits = new List<RaycastHit2D>();
    private readonly Dictionary<Collider2D, GridCell> _dropCellCache = new Dictionary<Collider2D, GridCell>();
    private ContactFilter2D _dropFilter = new ContactFilter2D().NoFilter();

    private void OnDisable() => EndPress();

    private void SetDropPreview(GridCell cell)
    {
        if (cell != null && cell.OccupyingUnit != null && cell.OccupyingUnit.IsStunned) cell = null;
        if (_dropPreviewCell == cell) return;
        if (_dropPreviewCell != null) _dropPreviewCell.Model?.SetUnitDropPreview(false);
        _dropPreviewCell = cell;
        if (_dropPreviewCell != null) _dropPreviewCell.Model?.SetUnitDropPreview(true, IsMergeTarget(_dropPreviewCell));
    }

    // 놓으면 드래그 합성이 일어나는 칸인지 — TrySwap의 합성 분기와 같은 조건.
    private bool IsMergeTarget(GridCell cell)
    {
        return _unit != null && cell != _originCell && cell.Model != null && cell.Model.IsAvailable &&
               UnitMergeRules.CanPair(_unit, cell.OccupyingUnit);
    }

    // Preview and release must resolve the same cell. Ignore this dragged object's
    // colliders: its parent remains the origin cell until placement finishes.
    private GridCell FindDropCell(Vector2 worldPosition)
    {
        Physics2D.Raycast(worldPosition, Vector2.zero, _dropFilter, _dropHits);
        foreach (var hit in _dropHits)
        {
            var collider = hit.collider;
            if (collider == null || collider.transform.IsChildOf(transform)) continue;
            if (!_dropCellCache.TryGetValue(collider, out var cell))
            {
                // GridCell owns its collider. An occupant's collider may extend over
                // adjacent tiles and must not redirect the drop to its parent cell.
                cell = collider.GetComponent<GridCell>();
                _dropCellCache[collider] = cell;
            }
            if (cell != null) return cell;
        }
        return null;
    }

    private void Awake()
    {
        _unit  = GetComponent<UnitBase>();
        _totem = GetComponent<TotemBase>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        AdjustCollider();
    }

    public void AdjustCollider()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) 
        {
            col = gameObject.AddComponent<BoxCollider2D>();
            // 컴포넌트가 없어서 새로 추가될 때만 기본값 세팅
            col.size = new Vector2(1.2f, 1.2f);
            col.offset = new Vector2(0f, 0.6f);
        }
        // 프리팹에 이미 BoxCollider2D가 있다면 사용자가 설정한 크기/오프셋을 그대로 유지합니다.
    }

    public void SetOriginCell(GridCell cell)
    {
        _originCell = cell;
    }

    public void OnPointerClick()
    {
        if (_unit != null)
        {
            OnUnitClickedEvent?.Invoke(_unit);
        }
        if (_totem != null)
        {
            OnTotemClickedGlobal?.Invoke(_totem);
        }
    }

    public void OnBeginDrag()
    {
        if (_unit != null && _unit.IsStunned) return;
        OnDragStartedEvent?.Invoke();
        _rotating = _totem != null && _totem.Data != null && _totem.Data.isRotatable &&
            _rotationUI != null && _totemInteractionSettings != null &&
            Time.unscaledTime - _pressStartedAt < _totemInteractionSettings.MoveHoldSeconds;
        if (_rotating) { _rotationUI?.Begin(_totem); return; }

        if (_originCell == null)
        {
            _originCell = GetComponentInParent<GridCell>();
        }

        if (_originCell == null) return;

        _originPos = transform.position;
        _isDragging = true;
        if (_unit != null || _totem != null) SetDropPreview(_originCell);
        
        if (_spriteRenderer != null)
        {
            _originSortingLayer = _spriteRenderer.sortingLayerName;
            _originSortingOrder = _spriteRenderer.sortingOrder;
            _spriteRenderer.sortingOrder = 30000;
        }
    }

    public void OnDrag(Vector2 worldPosition)
    {
        if (_unit != null && _unit.IsStunned) { CancelPointerDrag(); return; }
        if (_rotating) { _rotationUI?.Drag(worldPosition); return; }
        if (!_isDragging) return;
        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        if ((_unit != null || _totem != null) && _originCell != null) SetDropPreview(FindDropCell(worldPosition));
    }

    public void OnEndDrag(Vector2 worldPosition)
    {
        if (_unit != null && _unit.IsStunned) { CancelPointerDrag(); return; }
        if (!_isDragging && !_rotating) { EndPress(); return; }
        EndPress();
        if (_rotating) { _rotationUI?.End(worldPosition); _rotating = false; return; }
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sortingLayerName = _originSortingLayer;
            _spriteRenderer.sortingOrder = _originSortingOrder;
        }

        GridCell targetCell = FindDropCell(worldPosition);

        if (targetCell == null || targetCell == _originCell)
        {
            ReturnToOrigin();
            return;
        }

        // 봉인된 셀에는 배치 불가
        if (targetCell.Model == null || !targetCell.Model.IsAvailable) 
        { 
            ReturnToOrigin(); 
            return; 
        }

        if (targetCell.IsOccupied) TrySwap(targetCell);
        else                       MoveToEmpty(targetCell);
    }

    private void MoveToEmpty(GridCell targetCell)
    {
        if (_unit  != null) _originCell.RemoveUnit();
        if (_totem != null) { _originCell.RemoveTotem(); _totem.OnRemoved(); }

        PlaceSelfAt(targetCell);
    }

    private void TrySwap(GridCell targetCell)
    {
        UnitBase  targetUnit  = targetCell.OccupyingUnit;
        TotemBase targetTotem = targetCell.OccupyingTotem;
        // 다른 유닛/토템으로 스턴 유닛을 밀거나 드래그 합성하여 이동 제한을 우회하지 않는다.
        if (targetUnit != null && targetUnit.IsStunned) { ReturnToOrigin(); return; }

        // 드래그 합성: 합성 가능한 유닛 위에 놓으면 스왑 대신 합성한다.
        if (_unit != null && targetUnit != null && UnitMergeRules.CanPair(_unit, targetUnit))
        {
            if (_mergeManager == null || !_mergeManager.TryMergeByDrag(_unit, targetUnit))
                ReturnToOrigin();
            return;
        }

        // 와일드카드는 합성 짝이 아닌 유닛과 스왑하지 않는다 (기존 동작 유지).
        if (_unit != null && targetUnit != null &&
            (_unit.IsWildcardMergeUnit || targetUnit.IsWildcardMergeUnit))
        {
            ReturnToOrigin();
            return;
        }

        DragHandler targetDrag = null;
        if (targetUnit  != null) targetDrag = targetUnit.GetComponent<DragHandler>();
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

    public void PlaceSelfAt(GridCell cell)
    {
        if (_unit != null)
        {
            cell.TryPlaceUnit(_unit);
            transform.SetParent(cell.transform, false);
            if (_unitFactory != null) _unitFactory.InitUnitTransform(_unit);
            else transform.localPosition = Vector3.zero;
            
            _originCell = cell;

            _unit.OnRemoved();
            _unit.OnPlaced(_currencyManager, _bossManager?.CurrentBoss, cell);
        }

        if (_totem != null)
        {
            cell.TryPlaceTotem(_totem);
            transform.SetParent(cell.transform, false);
            if (_unitFactory != null) _unitFactory.InitTotemTransform(transform);
            else transform.localPosition = Vector3.zero;

            _originCell = cell;

            _totem.OnPlaced(cell);
        }

        UpdateDepthSorting();
    }

    public void UpdateDepthSorting()
    {
        if (_spriteRenderer != null)
        {
            // Y좌표가 낮을수록(화면 아래일수록) 앞에 그려지도록 정렬
            _originSortingOrder = Mathf.RoundToInt(-transform.position.y * 100f);
            _spriteRenderer.sortingOrder = _originSortingOrder;
        }
    }

    private void ReturnToOrigin()
    {
        transform.position = _originPos;
    }
}
