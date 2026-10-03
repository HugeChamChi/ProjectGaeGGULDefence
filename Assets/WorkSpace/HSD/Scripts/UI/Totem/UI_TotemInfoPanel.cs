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
    private TotemBase _currentTotem;
    private GridManager _grid;
    private TotemKillRangeGrowth _growthTotem;
    private GridCell _displayedCell;
    private int _displayedRotation;
    private int _displayedKillCount;
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

    /// <summary>Shows the base SO diagram for an unplaced totem.</summary>
    public void SetData(TotemData data)
    {
        ClearRuntimeRange();
        _effectLink?.Clear();
        EnsurePresenter();
        _presenter.SetData(data);
        gameObject.SetActive(true);
        if (_canvas != null) _canvas.enabled = true;
    }

    /// <summary>Shows a placed totem and refreshes its actual range while this panel is open.</summary>
    public void SetData(TotemBase totem, GridManager grid)
    {
        if (totem == null || !totem.IsActive || totem.CurrentCell == null || grid == null)
        {
            Close();
            return;
        }
        SetData(totem.Data);
        _currentTotem = totem;
        _grid = grid;
        _growthTotem = totem as TotemKillRangeGrowth;
        RefreshRuntimeRange();
    }

    private void LateUpdate()
    {
        if (_grid == null) return;
        if (_currentTotem == null || !_currentTotem.IsActive || _currentTotem.CurrentCell == null)
        {
            Close();
            return;
        }
        if (_canvas != null && !_canvas.enabled) return;
        if (_displayedCell != _currentTotem.CurrentCell ||
            _displayedRotation != _currentTotem.RotationStep ||
            _displayedKillCount != (_growthTotem != null ? _growthTotem.KillCount : 0))
            RefreshRuntimeRange();
    }

    private void RefreshRuntimeRange()
    {
        rangeGrid?.SetData(_currentTotem, _grid);
        _displayedCell = _currentTotem.CurrentCell;
        _displayedRotation = _currentTotem.RotationStep;
        _displayedKillCount = _growthTotem != null ? _growthTotem.KillCount : 0;
    }

    private void ClearRuntimeRange()
    {
        _currentTotem = null;
        _grid = null;
        _growthTotem = null;
        _displayedCell = null;
        _displayedKillCount = 0;
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
        ClearRuntimeRange();
        _effectLink?.Clear();
        return base.CloseAsync();
    }
    private void OnDisable()
    {
        ClearRuntimeRange();
        _effectLink?.Clear();
    }
}
