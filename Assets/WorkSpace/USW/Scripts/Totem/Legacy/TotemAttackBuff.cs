using UnityEngine;
using VContainer;
using System.Collections.Generic;

// ════════════════════════════════════════════════════════
// TotemAttackBuff — Manager 접근 통일
// ════════════════════════════════════════════════════════

/// <summary>
/// 공격력 버프 토템
/// 범위: 토템 기준 좌(x-1) / 우(x+1) 1칸씩 — 같은 행
/// 버프: 범위 안 유닛 공격력 +10% (글로벌 AttackMultiplier)
/// 장판: 빨강
/// </summary>
public class TotemAttackBuff : TotemBase
{

    protected override void ApplyBuff()
    {
        // 글로벌 버프 적용 제거 (PaintAffectedCells에서 지역 버프로 적용)
    }

    protected override void RemoveBuff()
    {
        // 글로벌 버프 해제 제거
    }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null) return list;

        var pos = CurrentCell.GridPosition;
        var left  = _gridManager.GetCell(pos.x - 1, pos.y);
        var right = _gridManager.GetCell(pos.x + 1, pos.y);

        if (left  != null) list.Add(left);
        if (right != null) list.Add(right);

        return list;
    }

    public override void PaintAffectedCells()
    {
        float efficiency = 1f + (_totemBuffManager != null ? _totemBuffManager.TotemEfficiencyBonus : 0f);
        foreach (var cell in GetAffectedCells())
        {
            cell.SetBuffFlags(atk: true, spd: cell.HasSpeedBuff);
            float amount = totemData.GetSimpleAmount(StatKind.AttackPercent);
            if (amount > 0f)
            {
                cell.AddTotemCellBonus(StatKind.AttackPercent, amount * efficiency);
            }
        }
    }
}