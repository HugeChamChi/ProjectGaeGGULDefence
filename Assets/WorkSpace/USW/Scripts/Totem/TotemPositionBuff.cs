using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// #25 위치 조건부 버프 토템
///
/// 토템 배치 위치에 따라 다른 버프를 범위 셀에 적용.
///   앞쪽 (y &lt; frontRowThreshold) → 공격력 버프 (attackBuffAmount)
///   뒤쪽 (y >= frontRowThreshold) → 속도 버프 (speedBuffAmount)
///
/// effectRange는 TotemEditor에서 설정 (예: 상+하).
/// </summary>
public class TotemPositionBuff : TotemBase
{
    [Tooltip("이 행(y) 미만을 '앞쪽'으로 판정. 기본 2 = y0,1이 앞줄")]
    [SerializeField] private int _frontRowThreshold = 2;

    private bool _isFront;

    protected override void ApplyBuff()
    {
        if (totemData == null || CurrentCell == null) return;

        _isFront = CurrentCell.GridPosition.y < _frontRowThreshold;

        if (_isFront)
            Manager.Buff.AddAttackBuff(totemData.attackBuffAmount);
        else
            Manager.Buff.AddSpeedBuff(totemData.speedBuffAmount);
    }

    protected override void RemoveBuff()
    {
        if (totemData == null) return;

        if (_isFront)
            Manager.Buff.RemoveAttackBuff(totemData.attackBuffAmount);
        else
            Manager.Buff.RemoveSpeedBuff(totemData.speedBuffAmount);
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

            cell.SetBuffFlags(
                atk: _isFront || cell.HasAttackBuff,
                spd: !_isFront || cell.HasSpeedBuff);
        }
    }
}
