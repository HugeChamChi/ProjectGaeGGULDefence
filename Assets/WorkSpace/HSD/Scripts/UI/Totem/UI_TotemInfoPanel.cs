using GaeGGUL.Extension;
using GaeGGUL.UI.Common;
using GaeGGUL.UI.Totem;
using TMPro;
using UnityEngine;

public class UI_TotemInfoPanel : UI_Base
{
    [Header("Totem Info")]
    [SerializeField] private UI_IconTierSlot iconSlot;
    [SerializeField] private TextMeshProUGUI txt_Name;
    [SerializeField] private TextMeshProUGUI txt_Stats;

    [Header("Preview Grid")]
    [SerializeField] private UI_TotemRangeGrid rangeGrid;

    private TotemInfoPresenter _presenter;

    protected void Awake()
    {
        EnsurePresenter();
    }

    public void SetData(TotemData data)
    {
        EnsurePresenter();
        _presenter.SetData(data);
        gameObject.SetActive(true);
    }

    private void EnsurePresenter()
    {
        if (_presenter == null) _presenter = new TotemInfoPresenter(this);
    }

    public void UpdateUI(Sprite icon, string name, string stats, Tier tier, TotemData data)
    {
        if (iconSlot != null) iconSlot.SetData(icon, tier);
        if (txt_Name != null) { txt_Name.text = $"[{name}]"; txt_Name.color = tier.GetTextColor(); }
        if (txt_Stats != null) txt_Stats.text = stats;

        if (rangeGrid != null)
        {
            rangeGrid.SetRange(data.effectRange, data.attackDisabledRange);
        }
    }
}
