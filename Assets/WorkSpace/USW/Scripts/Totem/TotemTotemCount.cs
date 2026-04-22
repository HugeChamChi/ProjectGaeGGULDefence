using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// #41 토템 수 비례 치명타 보너스 토템
///
/// 필드에 활성화된 토템 수에 비례해 전역 치명타 확률 + 데미지 보너스 적용.
/// PaintAffectedCells (RebuildCellBuffFlags에서 호출)마다 수치 재계산.
///
/// TotemData 설정:
///   critChanceBuffAmount = 토템 1개당 치명타 확률 (예: 0.02 = 2%)
///   critDamageBuffAmount = 토템 1개당 치명타 데미지 (예: 0.03 = 3%)
///   effectRange = 시각 표시 범위 (좌우 양끝, TotemEditor)
/// </summary>
public class TotemTotemCount : TotemBase
{
    // 마지막으로 적용한 원본 수치 (efficiency 미포함 — Remove 시 동일 값 전달)
    private float _appliedCritChance;
    private float _appliedCritDamage;

    protected override void ApplyBuff()
    {
        _appliedCritChance = 0f;
        _appliedCritDamage = 0f;
        // 실제 적용은 PaintAffectedCells에서 (RegisterTotem 후 count 반영)
    }

    protected override void RemoveBuff()
    {
        if (_appliedCritChance > 0f) Manager.Buff.RemoveCritChanceBuff(_appliedCritChance);
        if (_appliedCritDamage > 0f) Manager.Buff.RemoveCritDamageBuff(_appliedCritDamage);
        _appliedCritChance = 0f;
        _appliedCritDamage = 0f;
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

        // 이전 기여분 제거
        if (_appliedCritChance > 0f) Manager.Buff.RemoveCritChanceBuff(_appliedCritChance);
        if (_appliedCritDamage > 0f) Manager.Buff.RemoveCritDamageBuff(_appliedCritDamage);

        // 현재 토템 수 기준으로 재계산 (이 토템 포함)
        int count = Manager.Buff.GetActiveTotemCount();
        _appliedCritChance = count * totemData.critChanceBuffAmount;
        _appliedCritDamage = count * totemData.critDamageBuffAmount;

        if (_appliedCritChance > 0f) Manager.Buff.AddCritChanceBuff(_appliedCritChance);
        if (_appliedCritDamage > 0f) Manager.Buff.AddCritDamageBuff(_appliedCritDamage);

        // 셀 시각화
        var pos = CurrentCell.GridPosition;
        foreach (var offset in totemData.effectRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell == null) continue;

            cell.SetBuffFlags(atk: true || cell.HasAttackBuff, spd: cell.HasSpeedBuff);
        }
    }
}
