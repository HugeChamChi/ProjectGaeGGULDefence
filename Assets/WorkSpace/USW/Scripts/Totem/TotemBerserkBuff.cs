using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 광전사 버프 토템
/// 범위: 토템 기준 상(y-1) 1칸
/// 버프: 공격력 +10%, 공격속도 +10% (글로벌 AttackMultiplier + SpeedMultiplier)
/// 장판: 빨강+주황 혼합 (ColorBoth)
///
/// NOTE: 상(上) = y-1 기준. 그리드 방향이 다른 경우 offset을 +1로 변경하세요.
/// </summary>
public class TotemBerserkBuff : TotemBase
{
    protected override void ApplyBuff()
    {
        if (totemData.attackBuffAmount > 0f)
            Manager.Buff.AddAttackBuff(totemData.attackBuffAmount);
        else
            Debug.LogWarning($"TotemBerserkBuff({name}): attackBuffAmount = 0.");

        if (totemData.speedBuffAmount > 0f)
            Manager.Buff.AddSpeedBuff(totemData.speedBuffAmount);
        else
            Debug.LogWarning($"TotemBerserkBuff({name}): speedBuffAmount = 0.");
    }

    protected override void RemoveBuff()
    {
        if (totemData.attackBuffAmount > 0f)
            Manager.Buff.RemoveAttackBuff(totemData.attackBuffAmount);
        if (totemData.speedBuffAmount > 0f)
            Manager.Buff.RemoveSpeedBuff(totemData.speedBuffAmount);
    }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null) return list;

        var pos = CurrentCell.GridPosition;
        // 상(上) = y-1 (GridLayoutGroup 위→아래 기준, y=0이 최상단)
        var above = Manager.Grid.GetCell(pos.x, pos.y - 1);
        if (above != null) list.Add(above);

        return list;
    }

    public override void PaintAffectedCells()
    {
        foreach (var cell in GetAffectedCells())
            cell.SetBuffFlags(atk: true, spd: true);
    }
}
