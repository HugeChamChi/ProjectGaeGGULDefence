using UnityEngine;
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
        if (totemData.attackBuffAmount <= 0f)
        {
            Debug.LogWarning($"TotemAttackBuff({name}): attackBuffAmount = 0. TotemData를 확인하세요.");
            return;
        }
        Manager.Buff.AddAttackBuff(totemData.attackBuffAmount);
    }

    protected override void RemoveBuff()
    {
        if (totemData.attackBuffAmount <= 0f) return;
        Manager.Buff.RemoveAttackBuff(totemData.attackBuffAmount);
    }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null) return list;

        var pos = CurrentCell.GridPosition;
        var left  = Manager.Grid.GetCell(pos.x - 1, pos.y);
        var right = Manager.Grid.GetCell(pos.x + 1, pos.y);

        if (left  != null) list.Add(left);
        if (right != null) list.Add(right);

        return list;
    }

    public override void PaintAffectedCells()
    {
        foreach (var cell in GetAffectedCells())
            cell.SetBuffFlags(atk: true, spd: cell.HasSpeedBuff);
    }
}