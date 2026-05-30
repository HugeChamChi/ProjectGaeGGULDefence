using UnityEngine;

/// <summary>
/// 단순 수치+범위 토템 공용 스크립트 (총 33개 토템 공유).
///
/// TotemData SO의 수치를 읽어 effectRange 셀에만 셀별 버프를 기록한다.
/// 범위 순회 / attackDisabledRange / ApplyBuff/RemoveBuff는 RangedBuffTotemBase가 담당.
/// </summary>
public class GenericBuffTotem : RangedBuffTotemBase
{
    protected override void PaintRangeBuffs(GridCell cell)
    {
        // 시각 플래그
        bool hasAtk = totemData.attackBuffAmount    > 0f
                   || totemData.critDamageBuffAmount > 0f
                   || totemData.critChanceBuffAmount > 0f;
        bool hasSpd = totemData.speedBuffAmount     > 0f;
        bool hasFod = totemData.foodSpeedBuffAmount > 0f
                   || totemData.foodAmountBuffAmount > 0f;

        cell.SetBuffFlags(
            atk: hasAtk || cell.HasAttackBuff,
            spd: hasSpd || cell.HasSpeedBuff);

        if (hasFod) cell.SetFoodBuff(true);

        // 셀별 버프 보너스 누산 (RebuildCellBuffFlags 사이클마다 초기화 후 재계산)
        if (totemData.attackBuffAmount     > 0f) cell.AddTotemCellAttackBonus(totemData.attackBuffAmount);
        if (totemData.speedBuffAmount      > 0f) cell.AddTotemCellSpeedBonus(totemData.speedBuffAmount);
        if (totemData.foodSpeedBuffAmount  > 0f) cell.AddTotemCellFoodSpeedBonus(totemData.foodSpeedBuffAmount);
        if (totemData.foodAmountBuffAmount > 0f) cell.AddTotemCellFoodAmountBonus(totemData.foodAmountBuffAmount);
        if (totemData.critChanceBuffAmount > 0f) cell.AddTotemCellCritChanceBonus(totemData.critChanceBuffAmount);
        if (totemData.critDamageBuffAmount > 0f) cell.AddTotemCellCritDamageBonus(totemData.critDamageBuffAmount);
    }
}
