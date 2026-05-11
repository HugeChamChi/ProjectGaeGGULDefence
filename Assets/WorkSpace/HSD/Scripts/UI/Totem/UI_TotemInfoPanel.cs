using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using GaeGGUL.UI.Totem;

public class UI_TotemInfoPanel : MonoBehaviour
{
    [Header("Totem Info")]
    [SerializeField] private Image img_Icon;
    [SerializeField] private TextMeshProUGUI txt_Name;
    [SerializeField] private TextMeshProUGUI txt_Stats;

    [Header("Preview Grid")]
    [SerializeField] private UI_TotemRangeGrid rangeGrid;

    private TotemInfoPresenter _presenter;

    protected void Awake()
    {
        _presenter = new TotemInfoPresenter(this);
    }

    public void SetData(TotemData data)
    {
        if (_presenter == null) _presenter = new TotemInfoPresenter(this);
        _presenter.SetData(data);
        gameObject.SetActive(true);
    }

    public void UpdateUI(Sprite icon, string name, string stats, TotemData data)
    {
        if (img_Icon != null) img_Icon.sprite = icon;
        if (txt_Name != null) txt_Name.text = name;
        if (txt_Stats != null) txt_Stats.text = stats;

        if (rangeGrid != null)
        {
            rangeGrid.SetRange(data.effectRange, data.attackDisabledRange);
        }
    }
}
