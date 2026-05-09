using TMPro;
using UnityEngine;

public struct GachaChanceData
{
    public string rarity;
    public double percent;
}

public class UI_GachaChanceSlot : UI_SlotBase<GachaChanceData>
{
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private TextMeshProUGUI percentText;

    protected override void OnBind()
    {
        if (rarityText != null) rarityText.text = _data.rarity;
        if (percentText != null) percentText.text = $"{_data.percent:F2}%";
    }
}
