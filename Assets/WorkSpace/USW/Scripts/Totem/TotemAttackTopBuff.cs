using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 최상단 공격력 버프 토템
/// 범위: 그리드 최상단 행(y=0) 전체 칸
/// 버프: 공격력 +30% (글로벌 AttackMultiplier)
/// 장판: 빨강 (공격력 버프와 동일)
///
/// NOTE: Unity GridLayoutGroup이 위→아래로 채워지면 y=0이 최상단입니다.
///       그리드 방향이 다른 경우 GetAffectedCells()의 topRow 값을 조정하세요.
/// </summary>
public class TotemAttackTopBuff : TotemBase
{
    protected override void ApplyBuff()
    {
        if (totemData.attackBuffAmount <= 0f)
        {
            Debug.LogWarning($"TotemAttackTopBuff({name}): attackBuffAmount = 0. TotemData를 확인하세요.");
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

        // 최상단 = y=0 (GridLayoutGroup 위→아래 기준)
        const int topRow = 0;
        int cols = Manager.Grid.Columns;

        for (int x = 0; x < cols; x++)
        {
            var cell = Manager.Grid.GetCell(x, topRow);
            if (cell != null) list.Add(cell);
        }

        return list;
    }

    public override void PaintAffectedCells()
    {
        foreach (var cell in GetAffectedCells())
            cell.SetBuffFlags(atk: true, spd: cell.HasSpeedBuff);
    }
}
