using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 식량 생산 속도 버프 토템
/// 범위: 토템 기준 우(x+1) / 우2(x+2) 2칸
/// 버프: 식량 생산 간격 -20% (글로벌 FoodSpeedMultiplier 감소)
/// 장판: 초록
/// </summary>
public class TotemFoodBuff : TotemBase
{
    protected override void ApplyBuff()
    {
        if (totemData.foodSpeedBuffAmount <= 0f)
        {
            Debug.LogWarning($"TotemFoodBuff({name}): foodSpeedBuffAmount = 0. TotemData를 확인하세요.");
            return;
        }
        Manager.Buff.AddFoodSpeedBuff(totemData.foodSpeedBuffAmount);
    }

    protected override void RemoveBuff()
    {
        if (totemData.foodSpeedBuffAmount <= 0f) return;
        Manager.Buff.RemoveFoodSpeedBuff(totemData.foodSpeedBuffAmount);
    }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null) return list;

        var pos = CurrentCell.GridPosition;
        var right1 = Manager.Grid.GetCell(pos.x + 1, pos.y);
        var right2 = Manager.Grid.GetCell(pos.x + 2, pos.y);

        if (right1 != null) list.Add(right1);
        if (right2 != null) list.Add(right2);

        return list;
    }

    public override void PaintAffectedCells()
    {
        foreach (var cell in GetAffectedCells())
            cell.SetFoodBuff(true);
    }
}
