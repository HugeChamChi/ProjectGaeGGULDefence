using GaeGGUL.Extension;
using GaeGGUL.UI.Common;
using GaeGGUL.UI.Totem;
using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class UI_TotemInfoPanel : UI_Base
{
    [Header("Totem Info")]
    [SerializeField] private UI_IconTierSlot iconSlot;
    [SerializeField] private TextMeshProUGUI txt_Name;
    [SerializeField] private TextMeshProUGUI txt_Stats;

    [Header("Preview Grid")]
    [SerializeField] private UI_TotemRangeGrid rangeGrid;

    private TotemInfoPresenter _presenter;
    private DebuffInfoLink _effectLink;
    /// <summary>Includes the gesture-release shield of the nested detail window.</summary>
    public bool IsEffectInfoOpen => _effectLink?.BlocksOwnerInput == true;

    /// <summary>Supplies the shared explanation presenter explicitly from scene wiring.</summary>
    [VContainer.Inject]
    public void ConfigureEffectInfo(DebuffInfoPresenter presenter)
    {
        if (txt_Stats == null) return;
        _effectLink = txt_Stats.GetComponent<DebuffInfoLink>() ?? txt_Stats.gameObject.AddComponent<DebuffInfoLink>();
        _effectLink.Configure(this, presenter);
    }

    protected override void Awake()
    {
        base.Awake();
        EnsurePresenter();
    }

    public void SetData(TotemData data)
    {
        _effectLink?.Clear();
        EnsurePresenter();
        _presenter.SetData(data);
        gameObject.SetActive(true);
        if (_canvas != null) _canvas.enabled = true;
    }

    private void EnsurePresenter()
    {
        if (_presenter == null) _presenter = new TotemInfoPresenter(this);
    }

    public void UpdateUI(Sprite icon, string name, string stats, Tier tier, TotemData data)
    {
        if (iconSlot != null) iconSlot.SetData(icon, tier);
        if (txt_Name != null) { txt_Name.text = name; txt_Name.color = tier.GetTextColor(); }
        DebuffBinding binding = default;
        if (data != null) data.TryGetDebuffBinding(out binding);
        if (txt_Stats != null) txt_Stats.text = _effectLink != null ? _effectLink.SetDescription(stats, binding) : stats;

        if (rangeGrid != null)
        {
            rangeGrid.SetData(data);
        }
    }

    /// <summary>Canvas-based closure also closes its detail popup.</summary>
    public override UniTask CloseAsync()
    {
        _effectLink?.Clear();
        return base.CloseAsync();
    }
    private void OnDisable() => _effectLink?.Clear();
}
