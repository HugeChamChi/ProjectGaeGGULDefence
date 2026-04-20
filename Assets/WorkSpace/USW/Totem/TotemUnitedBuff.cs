using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 단결 버프 토템
/// 범위: 상(y-1) 1칸 + 하(y+1) 1칸
/// 조건: 토템이 "앞"에 배치된 경우에만 버프 적용
///       — 현재 구현: x >= gridColumns / 2 (오른쪽 절반 = 앞)
///       — TODO: "앞"의 정확한 기준을 확인 후 FrontColumnThreshold 값을 조정하세요.
/// 버프: 공격력 +20%, 공격속도 +20% (글로벌)
/// 장판: 노랑 (ColorBoth와 동일 — 필요 시 별도 색상 추가 가능)
/// </summary>
public class TotemUnitedBuff : TotemBase
{
    // TODO: "앞"의 기준 열(column) — 기획자 확인 후 수정
    // 현재: x >= gridColumns / 2 이면 "앞"으로 판단
    private bool IsInFrontPosition()
    {
        if (CurrentCell == null) return false;
        return CurrentCell.GridPosition.x >= Manager.Grid.Columns / 2;
    }

    // 버프 적용 여부를 기억 — 제거 시 동일 조건으로 해제하기 위함
    private bool _buffApplied = false;

    protected override void ApplyBuff()
    {
        if (!IsInFrontPosition())
        {
            Debug.Log($"[TotemUnited] 앞 위치 아님 (x={CurrentCell?.GridPosition.x}) — 버프 미적용");
            _buffApplied = false;
            return;
        }

        if (totemData.attackBuffAmount > 0f)
            Manager.Buff.AddAttackBuff(totemData.attackBuffAmount);
        else
            Debug.LogWarning($"TotemUnitedBuff({name}): attackBuffAmount = 0.");

        if (totemData.speedBuffAmount > 0f)
            Manager.Buff.AddSpeedBuff(totemData.speedBuffAmount);
        else
            Debug.LogWarning($"TotemUnitedBuff({name}): speedBuffAmount = 0.");

        _buffApplied = true;
    }

    protected override void RemoveBuff()
    {
        if (!_buffApplied) return;
        _buffApplied = false;

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
        // 상(上) = y-1, 하(下) = y+1
        var above = Manager.Grid.GetCell(pos.x, pos.y - 1);
        var below = Manager.Grid.GetCell(pos.x, pos.y + 1);

        if (above != null) list.Add(above);
        if (below != null) list.Add(below);

        return list;
    }

    public override void PaintAffectedCells()
    {
        foreach (var cell in GetAffectedCells())
            cell.SetBuffFlags(atk: true, spd: true);
    }
}
