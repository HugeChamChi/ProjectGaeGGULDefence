using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// #38 범위 공격불가 + 특정 셀 공격력 증폭 토템
///
/// attackDisabledRange 셀 → 공격불가 (SetTotemAttackDisabled)
/// effectRange 셀 → 공격력 배율 증폭 (SetTotemAttackModifier)
///
/// TotemData 설정:
///   attackBuffAmount = 증폭 비율 (예: 1.0 = +100% → modifier 2.0)
///   attackDisabledRange = 공격불가 오프셋 목록 (TotemEditor)
///   effectRange = 증폭 대상 오프셋 목록 (TotemEditor)
/// </summary>
public class TotemDisableBoost : TotemBase
{
    protected override void ApplyBuff()  { /* 셀 단위 효과만 사용 */ }
    protected override void RemoveBuff() { /* RebuildCellBuffFlags()에서 자동 초기화 */ }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null || totemData == null) return list;

        var pos = CurrentCell.GridPosition;

        foreach (var offset in totemData.effectRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null) list.Add(cell);
        }
        foreach (var offset in totemData.attackDisabledRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null && !list.Contains(cell)) list.Add(cell);
        }
        return list;
    }

    public override void PaintAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return;

        var pos       = CurrentCell.GridPosition;
        float modifier = 1f + totemData.attackBuffAmount;

        // effectRange → 공격력 배율 증폭
        foreach (var offset in totemData.effectRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell == null) continue;

            cell.SetTotemAttackModifier(modifier);
            cell.SetBuffFlags(atk: true || cell.HasAttackBuff, spd: cell.HasSpeedBuff);
        }

        // attackDisabledRange → 공격불가
        foreach (var offset in totemData.attackDisabledRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null) cell.SetTotemAttackDisabled(true);
        }
    }
}
