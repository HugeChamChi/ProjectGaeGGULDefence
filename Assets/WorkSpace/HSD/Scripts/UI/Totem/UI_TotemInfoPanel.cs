using GaeGGUL.Extension;
using GaeGGUL.UI.Common;
using GaeGGUL.UI.Totem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
    private Toggle _descriptionToggle;
    private TotemData _descriptionData;
    private bool _detailed;
    private float _baseHeight;
    private Vector2 _basePosition;
    private DebuffInfoLink _effectLink;
    private TotemBase _currentTotem;
    private GridManager _grid;
    private TotemKillRangeGrowth _growthTotem;
    private GridCell _displayedCell;
    private int _displayedRotation;
    private int _displayedGrowthSecond;
    /// <summary>Includes the gesture-release shield of the nested detail window.</summary>
    public bool IsEffectInfoOpen => _effectLink?.BlocksOwnerInput == true;

    /// <summary>Keeps touches on the open information window from dismissing its action popup.</summary>
    public bool ContainsScreenPoint(Vector2 point)
    {
        if (!gameObject.activeInHierarchy || (_canvas != null && !_canvas.enabled)) return false;
        var canvas = _canvas != null ? _canvas : GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, point, camera);
    }
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
        EnsureDescriptionToggle();
        _descriptionData = data;
        _detailed = false;
        _descriptionToggle.SetIsOnWithoutNotify(false);
        _presenter.SetData(data, _detailed);
        gameObject.SetActive(true);
        _canvas = InfoPanelPresentation.EnsureCanvas(gameObject);
        if (_canvas != null) _canvas.enabled = true;
    }

    /// <summary>Shows a placed totem and refreshes its actual range while this panel is open.</summary>
    public void SetData(TotemBase totem, GridManager grid)
    {
        if (totem == null || !totem.IsPlaced || totem.CurrentCell == null || grid == null)
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
        if (_currentTotem == null || !_currentTotem.IsPlaced || _currentTotem.CurrentCell == null)
        {
            Close();
            return;
        }
        if (_canvas != null && !_canvas.enabled) return;
        if (_displayedCell != _currentTotem.CurrentCell ||
            _displayedRotation != _currentTotem.RotationStep ||
            _displayedGrowthSecond != (_growthTotem != null ? (int)(_growthTotem.CombatSeconds + _growthTotem.HarvestSeconds) : 0))
            RefreshRuntimeRange();
    }

    private void RefreshRuntimeRange()
    {
        RefreshDescription();
        _displayedCell = _currentTotem.CurrentCell;
        _displayedRotation = _currentTotem.RotationStep;
        _displayedGrowthSecond = _growthTotem != null ? (int)(_growthTotem.CombatSeconds + _growthTotem.HarvestSeconds) : 0;
    }

    private void ClearRuntimeRange()
    {
        _currentTotem = null;
        _grid = null;
        _growthTotem = null;
        _displayedCell = null;
        _displayedGrowthSecond = 0;
        _descriptionData = null;
    }

    private void EnsureDescriptionToggle()
    {
        if (_descriptionToggle != null) return;
        var rect = (RectTransform)transform;
        _baseHeight = rect.rect.height;
        _basePosition = rect.anchoredPosition;
        _canvas = InfoPanelPresentation.EnsureCanvas(gameObject);
        _descriptionToggle = InfoPanelPresentation.CreateToggle(transform, txt_Stats, detailed =>
        {
            _detailed = detailed;
            RefreshDescription();
        });
        if (txt_Name != null)
        {
            var name = txt_Name.rectTransform;
            name.anchorMin = name.anchorMax = new Vector2(0, 1);
            name.pivot = new Vector2(0, 1);
            name.anchoredPosition = new Vector2(20, -12);
            name.sizeDelta = new Vector2(510, 52);
            txt_Name.enableAutoSizing = true;
            txt_Name.fontSizeMin = 24;
            txt_Name.fontSizeMax = 30;
        }
    }

    private void RefreshDescription()
    {
        if (_descriptionData == null) return;
        _presenter.SetData(_descriptionData, _detailed);
        if (_growthTotem != null && txt_Stats != null)
        {
            txt_Stats.text = _descriptionData.GetDisplayDescription(_detailed) + "\n\n" + _growthTotem.ProgressDescription;
            FitDescription();
        }
        if (_currentTotem != null && _grid != null) rangeGrid?.SetData(_currentTotem, _grid);
    }

    private void FitDescription()
    {
        if (_descriptionToggle == null || txt_Stats == null) return;
        const float descriptionWidth = 286f;
        const float padding = 24f;
        float textHeight = Mathf.Max(182f, txt_Stats.GetPreferredValues(txt_Stats.text, descriptionWidth, float.PositiveInfinity).y);
        var outer = txt_Stats.transform.parent.parent as RectTransform;
        var inner = txt_Stats.transform.parent as RectTransform;
        outer.anchorMin = outer.anchorMax = Vector2.one;
        outer.pivot = Vector2.one;
        outer.anchoredPosition = new Vector2(-20, -85);
        outer.sizeDelta = new Vector2(descriptionWidth + padding, textHeight + padding);
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.offsetMin = inner.offsetMax = Vector2.zero;
        txt_Stats.rectTransform.offsetMin = Vector2.one * (padding * .5f);
        txt_Stats.rectTransform.offsetMax = -Vector2.one * (padding * .5f);
        var root = (RectTransform)transform;
        float height = Mathf.Max(_baseHeight, 85f + textHeight + padding + 20f);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        root.anchoredPosition = _basePosition + Vector2.up * ((height - _baseHeight) * .5f);
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
        if (txt_Stats != null)
        {
            txt_Stats.lineSpacing = 0f;
            txt_Stats.paragraphSpacing = 0f;
            txt_Stats.text = _effectLink != null ? _effectLink.SetDescription(stats, binding) : stats;
            FitDescription();
        }

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
