using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// #34 판매 누적 버프 토템
///
/// 유닛이 삭제(판매)될 때마다 공격력을 attackBuffAmount씩 영구 누적.
/// UnitSpawner.OnAnyUnitSold 정적 이벤트로 판매 감지.
///
/// effectRange 셀은 시각 표시용.
/// </summary>
public class TotemSellStack : TotemBase
{
    private int   _stackCount;
    private float _appliedAmount;

    protected override void ApplyBuff()
    {
        _stackCount    = 0;
        _appliedAmount = 0f;
        UnitSpawner.OnAnyUnitSold += OnUnitSold;
    }

    protected override void RemoveBuff()
    {
        UnitSpawner.OnAnyUnitSold -= OnUnitSold;

        if (_appliedAmount > 0f)
            Manager.Buff.RemoveAttackBuff(_appliedAmount);

        _stackCount    = 0;
        _appliedAmount = 0f;
    }

    private void OnUnitSold()
    {
        if (totemData == null) return;

        float increment = totemData.attackBuffAmount;
        _stackCount++;
        _appliedAmount += increment;
        Manager.Buff.AddAttackBuff(increment);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemSellStack] 판매 누적 x{_stackCount} → 공격력 +{_appliedAmount * 100f:F2}%");
#endif
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
