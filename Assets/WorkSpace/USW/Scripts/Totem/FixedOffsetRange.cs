using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 고정 범위 — 토템 위치에 오프셋을 더하되, 회전은 반영하지 않는다.
/// (토템이 어느 방향을 보든 항상 같은 상대 위치를 가리켜야 하는 범위에 사용)
/// </summary>
[Serializable]
[DisplayName("고정 범위(회전 무시)")]
public class FixedOffsetRange : ITotemRange
{
    public List<Vector2Int> offsets = new List<Vector2Int>();

    public List<GridCell> GetCells(TotemBase totem, GridManager gridManager)
    {
        var list = new List<GridCell>();
        if (totem == null || totem.CurrentCell == null || gridManager == null) return list;

        var pos = totem.CurrentCell.GridPosition;
        foreach (var offset in offsets)
        {
            var cell = gridManager.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null) list.Add(cell);
        }
        return list;
    }

    public List<Vector2Int> GetPreviewOffsets() => new List<Vector2Int>(offsets);
}
