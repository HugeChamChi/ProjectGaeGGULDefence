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
        if (col != null && _spriteRenderer != null && _spriteRenderer.sprite != null)
        {
            Bounds spriteBounds = _spriteRenderer.sprite.bounds;
            Vector2 size = spriteBounds.size;
            Vector2 center = spriteBounds.center;

            if (_spriteRenderer.gameObject != gameObject)
            {
                Vector3 childScale = _spriteRenderer.transform.localScale;
                Vector3 childPos = _spriteRenderer.transform.localPosition;
                
                size.x *= Mathf.Abs(childScale.x);
                size.y *= Mathf.Abs(childScale.y);
                
                center.x = (center.x * childScale.x) + childPos.x;
                center.y = (center.y * childScale.y) + childPos.y;
            }

            col.size = size;
            col.offset = center;
        }
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

        if (_originCell == null) return;

        _originPos = transform.position;
        
        if (_spriteRenderer != null)
        {
            _originSortingLayer = _spriteRenderer.sortingLayerName;
            _originSortingOrder = _spriteRenderer.sortingOrder;
            _spriteRenderer.sortingLayerName = "UI"; // 드래그 시 맨 앞에 보이게 임의로 UI 레이어 사용 (프로젝트 설정에 따라 변경 가능)
            _spriteRenderer.sortingOrder = 999;
        }
    }

    public void OnDrag(Vector2 worldPosition)
    {
        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
    }

    public void OnEndDrag(Vector2 worldPosition)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sortingLayerName = _originSortingLayer;
            _spriteRenderer.sortingOrder = _originSortingOrder;
        }

        // Raycast를 쏴서 아래에 GridCell이 있는지 확인
        // 자기 자신의 Collider를 꺼서 셀을 맞출 수 있게 함
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero);
        
        if (col != null) col.enabled = true;

        GridCell targetCell = null;
        if (hit.collider != null)
        {
            targetCell = hit.collider.GetComponent<GridCell>();
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
            transform.localPosition = Vector3.zero;
            
            _originCell = cell;

            _unit.OnRemoved();
            _unit.OnPlaced(_currencyManager, _bossManager?.CurrentBoss, cell);
        }

        if (_totem != null)
        {
            cell.TryPlaceTotem(_totem);
            transform.SetParent(cell.transform, false);
            transform.localPosition = Vector3.zero;
            
            _originCell = cell;

            _totem.OnPlaced(cell);
        }
    }

    private void ReturnToOrigin()
    {
        transform.position = _originPos;
    }
}
