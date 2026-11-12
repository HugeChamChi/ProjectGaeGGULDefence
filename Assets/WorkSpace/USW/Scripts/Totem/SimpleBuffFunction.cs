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
        // 차트에 이 토템의 키가 있으면 차트 값, 없으면 에셋 값.
        float amount = totem != null && totem.Data != null ? totem.Data.SimpleAmount(kind, this.amount) : this.amount;
        if (cell == null || amount == 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;

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
