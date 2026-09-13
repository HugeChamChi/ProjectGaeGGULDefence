using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>중심에서 체비쇼프 거리로 정의하는 사각 고리. 거리 1은 인접 8칸, 거리 2는 바깥 16칸.</summary>
[Serializable, DisplayName("사각 고리 범위")]
public class TotemSquareRingRange : ITotemRange
{
    /// <summary>포함할 최소 거리. 1이면 중심 칸 제외.</summary>
    [Min(0)] public int MinRadius = 1;
    /// <summary>포함할 최대 거리.</summary>
    [Min(0)] public int MaxRadius = 1;

    /// <summary>오프셋이 고리에 속하는지 검사한다.</summary>
    public bool Contains(Vector2Int offset)
    {
        int radius = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y));
        return radius >= MinRadius && radius <= MaxRadius;
    }

    /// <inheritdoc />
    public List<Vector2Int> GetPreviewOffsets()
    {
        var result = new List<Vector2Int>();
        if (MinRadius < 0 || MaxRadius < MinRadius) return result;
        for (int y = -MaxRadius; y <= MaxRadius; y++)
            for (int x = -MaxRadius; x <= MaxRadius; x++)
            {
                var offset = new Vector2Int(x, y);
                if (Contains(offset)) result.Add(offset);
            }
        return result;
    }

    /// <inheritdoc />
    public List<GridCell> GetCells(TotemBase totem, GridManager gridManager)
    {
        var result = new List<GridCell>();
        if (totem?.CurrentCell == null || gridManager == null) return result;
        var origin = totem.CurrentCell.GridPosition;
        foreach (var offset in GetPreviewOffsets())
        {
            var cell = gridManager.GetCell(origin.x + offset.x, origin.y + offset.y);
            if (cell != null) result.Add(cell);
        }
        return result;
    }
}
