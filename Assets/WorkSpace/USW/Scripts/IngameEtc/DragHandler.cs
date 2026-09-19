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

    /// <summary>Records hold time without changing unit drag behavior.</summary>
    public void BeginPress()
    {
        _pressStartedAt = Time.unscaledTime;
        if (_totem != null) _gridManager?.ShowTotemRangePreview(_totem);
    }

    /// <summary>손을 뗐거나 입력이 취소되면 토템 범위를 숨긴다.</summary>
    public void EndPress()
    {
        if (_totem != null) _gridManager?.ClearTotemRangePreview();
    }

    /// <summary>Cancels interrupted touch movement or rotation.</summary>
    public void CancelPointerDrag()
    {
        if (_rotating) _rotationUI?.Cancel();
        else
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
        
        if (_spriteRenderer != null)
        {
            _originSortingLayer = _spriteRenderer.sortingLayerName;
            _originSortingOrder = _spriteRenderer.sortingOrder;
            _spriteRenderer.sortingOrder = 30000;
        }
    }

    public void OnDrag(Vector2 worldPosition)
    {
        if (_rotating) { _rotationUI?.Drag(worldPosition); return; }
        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
    }

    public void OnEndDrag(Vector2 worldPosition)
    {
        EndPress();
        if (_rotating) { _rotationUI?.End(worldPosition); _rotating = false; return; }
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sortingLayerName = _originSortingLayer;
            _spriteRenderer.sortingOrder = _originSortingOrder;
        }

        // Raycast를 쏴서 아래에 GridCell이 있는지 확인
        // 자기 자신의 Collider를 꺼서 셀을 맞출 수 있게 함
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(worldPosition, Vector2.zero);
        
        if (col != null) col.enabled = true;

        GridCell targetCell = null;
        foreach (var h in hits)
        {
            if (h.collider != null)
            {
                var cell = h.collider.GetComponentInParent<GridCell>();
                if (cell != null)
                {
                    targetCell = cell;
                    break;
                }
            }
        }

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

        if (_unit != null && targetUnit != null &&
            (_unit.IsWildcardMergeUnit || targetUnit.IsWildcardMergeUnit))
        {
            if (_mergeManager == null || !_mergeManager.TryMergeWildcardPair(_unit, targetUnit))
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
            if (_unitFactory != null) _unitFactory.InitUnitTransform(transform);
            else transform.localPosition = Vector3.zero;
            
            _originCell = cell;

            _unit.OnRemoved();
            _unit.OnPlaced(_currencyManager, _bossManager?.CurrentBoss, cell);
        }

        if (_totem != null)
        {
            cell.TryPlaceTotem(_totem);
            transform.SetParent(cell.transform, false);
            if (_unitFactory != null) _unitFactory.InitUnitTransform(transform);
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
