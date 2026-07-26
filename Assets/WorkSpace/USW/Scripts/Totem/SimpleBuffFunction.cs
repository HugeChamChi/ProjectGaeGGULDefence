using System;

/// <summary>
/// 간단한 버프 — 지정된 종류(kind)의 수치(amount)를 범위 안 셀에 누산한다.
/// </summary>
[Serializable]
[DisplayName("단순 버프")]
public class SimpleBuffFunction : ITotemFunction
{
    public StatKind kind;
    public float amount;

    public void Apply(TotemBase totem, GridCell cell, TotemBuffManager buffManager)
    {
        if (cell == null || amount <= 0f) return;

        float efficiency = 1f + (buffManager != null ? buffManager.TotemEfficiencyBonus : 0f);
        float appliedAmount = kind == StatKind.FoodSpeed ? amount : amount * efficiency;

        switch (kind)
        {
            case StatKind.AttackPercent:
            case StatKind.CritChance:
            case StatKind.CritDamage:
                cell.SetBuffFlags(atk: true, spd: cell.HasSpeedBuff);
                break;
            case StatKind.Speed:
                cell.SetBuffFlags(atk: cell.HasAttackBuff, spd: true);
                break;
            case StatKind.FoodSpeed:
            case StatKind.FoodAmount:
                cell.SetFoodBuff(true);
                break;
        }

        cell.AddTotemCellBonus(kind, appliedAmount);
    }
}
