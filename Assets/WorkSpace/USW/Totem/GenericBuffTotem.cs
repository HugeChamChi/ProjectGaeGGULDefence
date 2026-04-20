using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 단순 수치+범위 토템 공용 스크립트 (총 33개 토템 공유)
///
/// TotemData SO의 수치 필드를 읽어 전역 버프를 적용.
/// 효과 범위 / 공격불가 범위는 TotemData.effectRange / attackDisabledRange 오프셋 사용.
///
/// [버프 적용 방식]
///   - 공격력, 속도, 식량 등 → 전역 (TotemBuffManager 배율 변경)
///   - attackDisabledRange 셀 → 셀별 SetTotemAttackDisabled (기능적 효과)
///   - effectRange 셀 → SetBuffFlags 시각 표시
///
/// [새 토템 추가 시]
///   프리팹에 이 스크립트 부착 후 TotemData SO 연결.
///   수치는 SO Inspector에서 입력, 범위는 TotemEditor에서 설정.
/// </summary>
public class GenericBuffTotem : TotemBase
{
    // ── 버프 적용/해제 ────────────────────────────────────────

    protected override void ApplyBuff()
    {
        if (totemData == null) return;

        if (totemData.attackBuffAmount    > 0f) Manager.Buff.AddAttackBuff(totemData.attackBuffAmount);
        if (totemData.speedBuffAmount     > 0f) Manager.Buff.AddSpeedBuff(totemData.speedBuffAmount);
        if (totemData.foodSpeedBuffAmount > 0f) Manager.Buff.AddFoodSpeedBuff(totemData.foodSpeedBuffAmount);
        if (totemData.foodAmountBuffAmount > 0f) Manager.Buff.AddFoodAmountBuff(totemData.foodAmountBuffAmount);
        if (totemData.critDamageBuffAmount > 0f) Manager.Buff.AddCritDamageBuff(totemData.critDamageBuffAmount);
        if (totemData.critChanceBuffAmount > 0f) Manager.Buff.AddCritChanceBuff(totemData.critChanceBuffAmount);
    }

    protected override void RemoveBuff()
    {
        if (totemData == null) return;

        if (totemData.attackBuffAmount    > 0f) Manager.Buff.RemoveAttackBuff(totemData.attackBuffAmount);
        if (totemData.speedBuffAmount     > 0f) Manager.Buff.RemoveSpeedBuff(totemData.speedBuffAmount);
        if (totemData.foodSpeedBuffAmount > 0f) Manager.Buff.RemoveFoodSpeedBuff(totemData.foodSpeedBuffAmount);
        if (totemData.foodAmountBuffAmount > 0f) Manager.Buff.RemoveFoodAmountBuff(totemData.foodAmountBuffAmount);
        if (totemData.critDamageBuffAmount > 0f) Manager.Buff.RemoveCritDamageBuff(totemData.critDamageBuffAmount);
        if (totemData.critChanceBuffAmount > 0f) Manager.Buff.RemoveCritChanceBuff(totemData.critChanceBuffAmount);
    }

    // ── 범위 셀 목록 ──────────────────────────────────────────

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

    // ── 셀 시각화 (RebuildCellBuffFlags에서 호출) ─────────────

    public override void PaintAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return;

        bool hasAtk = totemData.attackBuffAmount    > 0f
                   || totemData.critDamageBuffAmount > 0f
                   || totemData.critChanceBuffAmount > 0f;
        bool hasSpd = totemData.speedBuffAmount     > 0f;
        bool hasFod = totemData.foodSpeedBuffAmount > 0f
                   || totemData.foodAmountBuffAmount > 0f;

        var pos = CurrentCell.GridPosition;

        // 효과 범위 — 시각 플래그
        foreach (var offset in totemData.effectRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell == null) continue;

            cell.SetBuffFlags(
                atk: hasAtk || cell.HasAttackBuff,
                spd: hasSpd || cell.HasSpeedBuff);

            if (hasFod) cell.SetFoodBuff(true);
        }

        // 공격불가 범위 — 셀별 기능 효과
        foreach (var offset in totemData.attackDisabledRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null) cell.SetTotemAttackDisabled(true);
        }
    }
}
