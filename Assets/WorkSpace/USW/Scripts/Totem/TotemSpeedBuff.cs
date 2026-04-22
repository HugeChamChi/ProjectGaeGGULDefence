using UnityEngine;
using System.Collections.Generic;

// ════════════════════════════════════════════════════════
// TotemSpeedBuff — Manager 접근 통일
// ════════════════════════════════════════════════════════

/// <summary>
/// 공격속도(게이지) 버프 토템
/// 범위: 토템 기준 대각선 4칸 (좌상/우상/좌하/우하)
/// 버프: 범위 안 유닛 게이지 속도 +30% (글로벌 SpeedMultiplier 감소)
/// 장판: 주황
/// </summary>
public class TotemSpeedBuff : TotemBase
{
    protected override void ApplyBuff()
    {
        if (totemData.speedBuffAmount <= 0f)
        {
            Debug.LogWarning($"TotemSpeedBuff({name}): speedBuffAmount = 0. TotemData를 확인하세요.");
            return;
        }
        Manager.Buff.AddSpeedBuff(totemData.speedBuffAmount);
    }

    protected override void RemoveBuff()
    {
        if (totemData.speedBuffAmount <= 0f) return;
        Manager.Buff.RemoveSpeedBuff(totemData.speedBuffAmount);
    }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null) return list;

        var pos = CurrentCell.GridPosition;
        var offsets = new Vector2Int[]
        {
            new Vector2Int(-1, -1),
            new Vector2Int( 1, -1),
            new Vector2Int(-1,  1),
            new Vector2Int( 1,  1),
        };

        foreach (var offset in offsets)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null) list.Add(cell);
        }

        return list;
    }

    public override void PaintAffectedCells()
    {
        foreach (var cell in GetAffectedCells())
            cell.SetBuffFlags(atk: cell.HasAttackBuff, spd: true);
    }
}