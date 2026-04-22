using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// #37 제한 해제 토템
///
/// effectRange 셀에 NullifyDamageDebuff 플래그를 설정.
/// 해당 셀의 유닛은 보스 패턴의 데미지 감소 효과를 무시 (DamageModifier = 1.0 취급).
///
/// effectRange는 TotemEditor에서 설정 (예: 상 1칸).
/// </summary>
public class TotemNullifyDebuff : TotemBase
{
    protected override void ApplyBuff()  { /* 셀 단위 효과 — PaintAffectedCells에서 처리 */ }
    protected override void RemoveBuff() { /* RebuildCellBuffFlags()에서 ClearTotemEffects로 자동 초기화 */ }

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
        return list;
    }

    public override void PaintAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return;

        var pos = CurrentCell.GridPosition;
        foreach (var offset in totemData.effectRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell == null) continue;

            cell.SetNullifyDamageDebuff(true);
            // 공격력 무효 = 공격 버프 계열로 시각화
            cell.SetBuffFlags(atk: true || cell.HasAttackBuff, spd: cell.HasSpeedBuff);
        }
    }
}
