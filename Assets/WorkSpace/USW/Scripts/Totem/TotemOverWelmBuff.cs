using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 압도(OverWelm) 버프 토템
///
/// 토템 배치 위치를 (0,0) 기준으로
/// (-1,+1) (0,+1) (+1,+1) — 위쪽 3칸을 공격 불가 상태로 만듦
/// </summary>
public class TotemOverWelmBuff : TotemBase
{
    // 영향받는 셀 오프셋 목록
    private static readonly Vector2Int[] Offsets =
    {
        new Vector2Int(-1, 1),
        new Vector2Int( 0, 1),
        new Vector2Int( 1, 1),
    };

    protected override void ApplyBuff()  { /* 셀 단위 효과 — PaintAffectedCells에서 처리 */ }
    protected override void RemoveBuff() { /* RebuildCellBuffFlags()에서 자동 초기화 */ }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null) return list;

        var pos = CurrentCell.GridPosition;
        foreach (var offset in Offsets)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null) list.Add(cell);
        }
        return list;
    }

    public override void PaintAffectedCells()
    {
        if (CurrentCell == null) return;

        var pos = CurrentCell.GridPosition;
        foreach (var offset in Offsets)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null)
                cell.SetTotemAttackDisabled(true);
        }
    }
}
