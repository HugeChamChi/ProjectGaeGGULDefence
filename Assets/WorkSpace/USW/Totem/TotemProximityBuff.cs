using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// #35 인접 기물 감지 버프 토템
///
/// effectRange 셀(주변 8칸)의 기물(유닛) 수에 비례해 전역 공격력 버프를 동적 적용.
/// 유닛 배치/제거 시 UnitBase.OnAnyUnitChanged를 통해 자동 재계산.
///
/// attackBuffAmount = 유닛 1개당 공격력 증가율 (예: 0.04 = 4%)
/// </summary>
public class TotemProximityBuff : TotemBase
{
    private float _appliedAmount;

    protected override void ApplyBuff()
    {
        _appliedAmount          = 0f;
        UnitBase.OnAnyUnitChanged += Recalculate;
        Recalculate();
    }

    protected override void RemoveBuff()
    {
        UnitBase.OnAnyUnitChanged -= Recalculate;

        if (_appliedAmount > 0f)
            Manager.Buff.RemoveAttackBuff(_appliedAmount);

        _appliedAmount = 0f;
    }

    private void Recalculate()
    {
        if (totemData == null || CurrentCell == null || !IsActive) return;

        int count = CountUnitsInRange();
        float newAmount = count * totemData.attackBuffAmount;

        // delta 적용
        float delta = newAmount - _appliedAmount;
        if (Mathf.Approximately(delta, 0f)) return;

        if (delta > 0f)
            Manager.Buff.AddAttackBuff(delta);
        else
            Manager.Buff.RemoveAttackBuff(-delta);

        _appliedAmount = newAmount;
    }

    private int CountUnitsInRange()
    {
        int count = 0;
        var pos   = CurrentCell.GridPosition;

        foreach (var offset in totemData.effectRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null && cell.OccupyingUnit != null)
                count++;
        }
        return count;
    }

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

            cell.SetBuffFlags(atk: true || cell.HasAttackBuff, spd: cell.HasSpeedBuff);
        }
    }
}
