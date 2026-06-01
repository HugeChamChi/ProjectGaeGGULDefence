using UnityEngine;
using VContainer;
using System.Collections.Generic;

/// <summary>
/// effectRange / attackDisabledRange 기반 토템의 공통 기반 클래스.
///
/// 범위 인프라(GetAffectedCells, PaintAffectedCells, attackDisabledRange 처리)를 담당.
/// 버프는 RebuildCellBuffFlags → PaintAffectedCells 사이클로 적용되므로
/// ApplyBuff / RemoveBuff는 비워 둔다.
///
/// 서브클래스는 PaintRangeBuffs(cell) 하나만 구현하면 된다.
/// </summary>
public abstract class RangedBuffTotemBase : TotemBase
{

    /// <summary>effectRange 안 각 셀에 적용할 버프와 시각 플래그를 여기서 구현.</summary>
    protected abstract void PaintRangeBuffs(GridCell cell);

    // ── TotemBase 추상 메서드 구현 ────────────────────────────

    protected override void ApplyBuff()  { }
    protected override void RemoveBuff() { }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null || totemData == null) return list;

        var pos = CurrentCell.GridPosition;
        foreach (var offset in totemData.effectRange)
        {
            var rotated = RotateOffset(offset);
            var cell    = _gridManager.GetCell(pos.x + rotated.x, pos.y + rotated.y);
            if (cell != null) list.Add(cell);
        }
        return list;
    }

    public override void PaintAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return;

        var pos = CurrentCell.GridPosition;

        // effectRange 셀 — 서브클래스 버프/시각화
        foreach (var offset in totemData.effectRange)
        {
            var rotated = RotateOffset(offset);
            var cell    = _gridManager.GetCell(pos.x + rotated.x, pos.y + rotated.y);
            if (cell == null) continue;
            PaintRangeBuffs(cell);
        }

        // attackDisabledRange 셀 — 공격불가 (모든 범위 토템 공통)
        foreach (var offset in totemData.attackDisabledRange)
        {
            var rotated = RotateOffset(offset);
            var cell    = _gridManager.GetCell(pos.x + rotated.x, pos.y + rotated.y);
            if (cell != null) cell.SetTotemAttackDisabled(true);
        }
    }
}
