using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토템 기준 지정 범위 — 토템 위치에 오프셋을 더하고, 토템의 회전(RotationStep)을 반영한다.
/// TotemEditorWindow로 편집하던 기존 effectRange/attackDisabledRange와 동일한 개념의 후속 타입.
/// </summary>
[Serializable]
public class TotemRelativeOffsetRange : ITotemRange
{
    public List<Vector2Int> offsets = new List<Vector2Int>();

    public List<GridCell> GetCells(TotemBase totem, GridManager gridManager)
    {
        var list = new List<GridCell>();
        if (totem == null || totem.CurrentCell == null || gridManager == null) return list;

        var pos = totem.CurrentCell.GridPosition;
        foreach (var offset in offsets)
        {
            var rotated = totem.RotateOffset(offset);
            var cell = gridManager.GetCell(pos.x + rotated.x, pos.y + rotated.y);
            if (cell != null) list.Add(cell);
        }
        return list;
    }

    public List<Vector2Int> GetPreviewOffsets() => new List<Vector2Int>(offsets);
}
