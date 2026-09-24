using System;
using UnityEngine;

/// <summary>
/// 드래그 판매 (씬 스코프 서비스).
/// 유닛/토템을 이동 드래그하는 동안 판매 띠(DragSellBarUI)를 띄우고, 띠 위에서 놓으면 기존 판매 버튼과 같은 경로로 판매한다.
/// 띠가 등록되지 않은 씬에서는 아무 일도 하지 않으므로 기존 판매 버튼 흐름에 영향이 없다.
/// </summary>
public sealed class DragSellService
{
    private readonly UnitSpawner _unitSpawner;
    private readonly TotemSpawner _totemSpawner;
    private readonly MergeManager _mergeManager;
    private readonly GridManager _gridManager;

    private DragSellBarUI _zone;
    private DragHandler _owner;
    private bool _hover;

    /// <summary>이동 드래그 시작(true) / 종료(false). 판매 띠가 있을 때만 발행된다.</summary>
    public event Action<bool> OnMoveDragChanged;

    /// <summary>손가락이 판매 띠 안에 들어옴(true) / 나감(false).</summary>
    public event Action<bool> OnHoverChanged;

    public DragSellService(UnitSpawner unitSpawner, TotemSpawner totemSpawner, MergeManager mergeManager, GridManager gridManager)
    {
        _unitSpawner = unitSpawner;
        _totemSpawner = totemSpawner;
        _mergeManager = mergeManager;
        _gridManager = gridManager;
    }

    /// <summary>판매 띠가 켜질 때 스스로 등록한다.</summary>
    public void RegisterZone(DragSellBarUI zone) => _zone = zone;

    /// <summary>판매 띠가 꺼질 때 등록을 해제한다.</summary>
    public void UnregisterZone(DragSellBarUI zone)
    {
        if (_zone != zone) return;
        if (_owner != null) EndMove(_owner);
        _zone = null;
    }

    /// <summary>이동 드래그가 실제로 시작됐을 때 (토템은 이동 모드 확정 후, 회전 제외).</summary>
    public void BeginMove(DragHandler owner)
    {
        if (_zone == null || owner == null) return;
        _owner = owner;
        SetHover(false);
        OnMoveDragChanged?.Invoke(true);
    }

    /// <summary>드래그 중 손가락 위치로 띠 호버 상태를 갱신한다.</summary>
    public void UpdateMove(DragHandler owner, Vector2 worldPosition)
    {
        if (_zone == null || owner != _owner) return;
        SetHover(_zone.ContainsWorldPoint(worldPosition));
    }

    /// <summary>놓기·취소·비활성화 등 드래그가 끝나는 모든 경로에서 호출된다. 시작한 핸들러만 끝낼 수 있다.</summary>
    public void EndMove(DragHandler owner)
    {
        if (owner == null || owner != _owner) return;
        _owner = null;
        SetHover(false);
        OnMoveDragChanged?.Invoke(false);
    }

    /// <summary>띠 위에서 놓았다면 판매하고 true. 판매 버튼과 같은 경로(판매 + 선택 해제 / 범위 미리보기 정리 + 판매).</summary>
    public bool TrySell(UnitBase unit, TotemBase totem, Vector2 worldPosition)
    {
        if (_zone == null || !_zone.ContainsWorldPoint(worldPosition)) return false;
        if (unit != null)
        {
            if (unit.OriginalTier == Tier.Chieftain) return false;
            _unitSpawner?.SellUnit(unit);
            _mergeManager?.ClearSelection();
            return true;
        }
        if (totem != null)
        {
            _gridManager?.ClearTotemRangePreview();
            _totemSpawner?.SellTotem(totem);
            return true;
        }
        return false;
    }

    private void SetHover(bool hover)
    {
        if (_hover == hover) return;
        _hover = hover;
        OnHoverChanged?.Invoke(hover);
    }
}
