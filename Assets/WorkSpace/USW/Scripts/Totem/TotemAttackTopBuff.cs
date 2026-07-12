using UnityEngine;
using VContainer;
using System.Collections.Generic;

/// <summary>
/// 전진배치 토템 — 그리드 최상단 행(y=0) 전체 칸의 유닛에게만 공격력 버프.
/// 토템 배치 위치 및 회전과 무관하게 항상 최상단 가로 한 줄(0,0 ~ N,0)에 고정 적용.
///
/// 버프 방식: 전역(AttackMultiplier)이 아닌 셀별 보너스(TotemCellAttackBonus).
///   → 최상단 줄에 있는 유닛만 공격력 상승. (스펙: 0,0 / 1,0 / 2,0 / 3,0 / 4,0 / 5,0)
/// 장판: 빨강(공격 버프).
///
/// 셀별 버프는 RebuildCellBuffFlags → PaintAffectedCells 사이클로 적용/해제되므로
/// ApplyBuff / RemoveBuff는 비워 둔다.
/// </summary>
public class TotemAttackTopBuff : TotemBase
{
    // 최상단 = y=0 (GridLayoutGroup 위→아래 기준)
    private const int TopRow = 0;

    protected override void ApplyBuff()  { }
    protected override void RemoveBuff() { }

    public override List<GridCell> GetAffectedCells()
    {
        // 토템 위치와 무관하게 항상 최상단 한 줄 전체.
        var list = new List<GridCell>();
        int cols = _gridManager.Columns;

        for (int x = 0; x < cols; x++)
        {
            var cell = _gridManager.GetCell(x, TopRow);
            if (cell != null) list.Add(cell);
        }

        return list;
    }

    public override void PaintAffectedCells()
    {
        if (totemData == null) return;

        float amount = totemData.GetSimpleAmount(TotemBuffKind.Attack);
        if (amount <= 0f) return;

        float efficiency = 1f + (_totemBuffManager != null ? _totemBuffManager.TotemEfficiencyBonus : 0f);

        foreach (var cell in GetAffectedCells())
        {
            cell.AddTotemCellAttackBonus(amount * efficiency);
            cell.SetBuffFlags(atk: true, spd: cell.HasSpeedBuff);
        }
    }
}
