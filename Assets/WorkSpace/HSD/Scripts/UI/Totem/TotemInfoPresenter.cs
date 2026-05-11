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
        _view.UpdateUI(data.icon, data.totemName, statString, data);
    }

    private string BuildStatString(TotemData data)
    {
        StringBuilder sb = new StringBuilder();

        if (data.attackBuffAmount > 0)
            sb.AppendLine($"공격력 <color=#FFD700>{data.attackBuffAmount * 100:0}%</color> 증가");
        
        if (data.speedBuffAmount > 0)
            sb.AppendLine($"공격 속도 <color=#FFD700>{data.speedBuffAmount * 100:0}%</color> 증가");

        if (data.foodSpeedBuffAmount > 0)
            sb.AppendLine($"식량 생산 간격 <color=#FFD700>{data.foodSpeedBuffAmount * 100:0}%</color> 감소");

        if (data.critDamageBuffAmount > 0)
            sb.AppendLine($"치명타 데미지 <color=#FFD700>{data.critDamageBuffAmount * 100:0}%</color> 증가");

        if (data.critChanceBuffAmount > 0)
            sb.AppendLine($"치명타 확률 <color=#FFD700>{data.critChanceBuffAmount * 100:0}%</color> 증가");

        return sb.ToString().TrimEnd();
    }
}
