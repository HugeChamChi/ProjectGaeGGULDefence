using UnityEngine;
using System.Text;

public class TotemInfoPresenter
{
    private readonly UI_TotemInfoPanel _view;
    private TotemData _currentData;

    public TotemInfoPresenter(UI_TotemInfoPanel view)
    {
        _view = view;
    }

    public void SetData(TotemData data)
    {
        _currentData = data;
        
        string statString = BuildStatString(data);
        _view.UpdateUI(data.icon, data.totemName, statString, data.tier, data);
    }

    private string BuildStatString(TotemData data)
    {
        StringBuilder sb = new StringBuilder();

        float attack     = data.GetSimpleAmount(StatKind.AttackPercent);
        float speed      = data.GetSimpleAmount(StatKind.Speed);
        float foodSpeed  = data.GetSimpleAmount(StatKind.FoodSpeed);
        float critDamage = data.GetSimpleAmount(StatKind.CritDamage);
        float critChance = data.GetSimpleAmount(StatKind.CritChance);

        if (attack > 0)
            sb.AppendLine($"공격력 <color=#FFD700>{attack * 100:0}%</color> 증가");

        if (speed > 0)
            sb.AppendLine($"공격 속도 <color=#FFD700>{speed * 100:0}%</color> 증가");

        if (foodSpeed > 0)
            sb.AppendLine($"식량 생산 간격 <color=#FFD700>{foodSpeed * 100:0}%</color> 감소");

        if (critDamage > 0)
            sb.AppendLine($"치명타 데미지 <color=#FFD700>{critDamage * 100:0}%</color> 증가");

        if (critChance > 0)
            sb.AppendLine($"치명타 확률 <color=#FFD700>{critChance * 100:0}%</color> 증가");

        return sb.ToString().TrimEnd();
    }
}
