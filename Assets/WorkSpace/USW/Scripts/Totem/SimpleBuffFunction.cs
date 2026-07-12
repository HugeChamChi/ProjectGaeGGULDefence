using System;

/// <summary>
/// 간단한 버프 — 지정된 종류(kind)의 수치(amount)를 범위 안 셀에 누산한다.
/// </summary>
[Serializable]
public class SimpleBuffFunction : ITotemFunction
{
    public TotemBuffKind kind;
    public float amount;

    public void Apply(TotemBase totem, GridCell cell, TotemBuffManager buffManager)
    {
        if (cell == null || amount <= 0f) return;

        float efficiency = 1f + (buffManager != null ? buffManager.TotemEfficiencyBonus : 0f);

        switch (kind)
        {
            case TotemBuffKind.Attack:
                cell.SetBuffFlags(atk: true, spd: cell.HasSpeedBuff);
                cell.AddTotemCellAttackBonus(amount * efficiency);
                break;
            case TotemBuffKind.Speed:
                cell.SetBuffFlags(atk: cell.HasAttackBuff, spd: true);
                cell.AddTotemCellSpeedBonus(amount * efficiency);
                break;
            case TotemBuffKind.FoodSpeed:
                cell.SetFoodBuff(true);
                cell.AddTotemCellFoodSpeedBonus(amount);
                break;
            case TotemBuffKind.FoodAmount:
                cell.SetFoodBuff(true);
                cell.AddTotemCellFoodAmountBonus(amount * efficiency);
                break;
            case TotemBuffKind.CritChance:
                cell.SetBuffFlags(atk: true, spd: cell.HasSpeedBuff);
                cell.AddTotemCellCritChanceBonus(amount * efficiency);
                break;
            case TotemBuffKind.CritDamage:
                cell.SetBuffFlags(atk: true, spd: cell.HasSpeedBuff);
                cell.AddTotemCellCritDamageBonus(amount * efficiency);
                break;
        }
    }
}
