using System;
using System.Collections.Generic;
using UnityEngine;

public enum TotemDirection { Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight }

/// <summary>
/// 특정 방향으로 쭉 뻗는 직선 범위 — 토템 기준 direction 방향으로 그리드 끝까지.
/// 길이는 입력받지 않고 항상 그리드 경계까지 뻗는다 (대각선 UpLeft/UpRight/DownLeft/DownRight 포함).
/// rotationAware가 true면 토템의 RotationStep만큼 direction도 함께 회전한다.
/// </summary>
[Serializable]
[DisplayName("직선 범위")]
public class DirectionalLineRange : ITotemRange
{
    /// <summary>에디터 미리보기 그리드(6x4) 안에서 어느 위치에서 시작하든 끝까지 닿기에 충분한 칸 수.</summary>
    private const int PreviewMaxSteps = 5;

    public TotemDirection direction = TotemDirection.Right;
    public bool rotationAware = true;

    private static Vector2Int BaseVector(TotemDirection dir) => dir switch
    {
        TotemDirection.Up        => new Vector2Int(0, -1),
        TotemDirection.Down      => new Vector2Int(0, 1),
        TotemDirection.Left      => new Vector2Int(-1, 0),
        TotemDirection.Right     => new Vector2Int(1, 0),
        TotemDirection.UpLeft    => new Vector2Int(-1, -1),
        TotemDirection.UpRight   => new Vector2Int(1, -1),
        TotemDirection.DownLeft  => new Vector2Int(-1, 1),
        TotemDirection.DownRight => new Vector2Int(1, 1),
        _ => Vector2Int.zero
    };

    public List<GridCell> GetCells(TotemBase totem, GridManager gridManager)
    {
        var list = new List<GridCell>();
        if (totem == null || totem.CurrentCell == null || gridManager == null) return list;

        var pos      = totem.CurrentCell.GridPosition;
        var step     = rotationAware ? totem.RotateOffset(BaseVector(direction)) : BaseVector(direction);
        int maxSteps = Mathf.Max(gridManager.Columns, gridManager.Rows);

        for (int i = 1; i <= maxSteps; i++)
        {
            var cell = gridManager.GetCell(pos.x + step.x * i, pos.y + step.y * i);
            if (cell != null) list.Add(cell);
        }
        return list;
    }

    public List<Vector2Int> GetPreviewOffsets()
    {
        var list = new List<Vector2Int>();
        var step = BaseVector(direction);
        for (int i = 1; i <= PreviewMaxSteps; i++)
            list.Add(new Vector2Int(step.x * i, step.y * i));
        return list;
    }
}
