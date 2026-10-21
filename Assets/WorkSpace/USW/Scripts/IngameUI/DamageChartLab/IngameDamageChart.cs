using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 네 전투 버튼을 밀어 실시간 딜 그래프로 전환한다. 결과창은 확정 기록을 별도 창으로 표시한다.
/// 같은 집계·행 뷰를 재사용하며 전투 시간을 멈추지 않는다.
/// </summary>
public sealed class IngameDamageChart : MonoBehaviour
{
    private const int GameplaySortingOrder = 310;
    private const int ResultSortingOffset = 30;
    [SerializeField] private DamageChartStyle _style;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private IngameHudMenu _menu;
    [Inject] private DamageContributionLedger _ledger;
    [Inject] private DamageChartLabSettings _settings;
    private readonly Dictionary<DamageContributionEntry, DamageContributionRowView> _rows =
        new Dictionary<DamageContributionEntry, DamageContributionRowView>();
    private readonly List<DamageContributionEntry> _sorted = new List<DamageContributionEntry>();
    private DamageChartUiFactory _ui;
    private GameObject _canvasObject;
    private RectTransform _content, _viewport, _panel;
    private TMP_Text _title, _total, _dps, _empty;
    private float _refreshClock;
    private IngameHudDamagePager _pager;
    private TMP_Text _caption;
    private bool _finalPopup, _compact;
    private float _rowTextScale;

    /// <summary>현재 표시 상태.</summary>
    public bool IsOpen => (_pager != null && _pager.IsChartVisible) || (_canvasObject != null && _canvasObject.activeSelf);
    /// <summary>딜표 창 영역 (튜토리얼 안내 위치). 처음 열기 전에는 null.</summary>
    public RectTransform PanelArea => _panel;
    /// <summary>현재 표시 중인 실제 피해 출처 종류 수.</summary>
    public int VisibleRowCount => _sorted.Count;
    /// <summary>정확한 전투 집계.</summary>
    public DamageContributionLedger Ledger => _ledger;
    /// <summary>공통 네 버튼 페이지 전환기.</summary>
    public IngameHudDamagePager HudPager => _pager;

    private void Start()
    {
        if (_menu != null) _menu.DamageChartRequested.AddListener(Open);
        if (_settings == null || _style == null || _ledger == null) return;
        var pager = gameObject.AddComponent<IngameHudDamagePager>();
        if (!pager.Initialize(this, _menu, _settings, _font, _style))
        {
            Destroy(pager);
            Debug.LogWarning("[IngameDamageChart] 공통 하단 HUD를 찾지 못해 기존 창을 사용합니다.", this);
            return;
        }
        _pager = pager;
        BuildUi();
        AttachToHud();
    }

    /// <summary>실제 누적 피해를 표시한다. 전투는 계속 진행된다.</summary>
    public void Open()
    {
        if (_pager == null) { OpenAtSortingOrder(GameplaySortingOrder); return; }
        if (_finalPopup) AttachToHud();
        _title.text = "딜 그래프";
        _menu?.SetExpanded(false);
        _pager.SetChartVisible(true);
        Canvas.ForceUpdateCanvases();
        Refresh();
    }

    /// <summary>확정된 이번 런의 딜표를 결과 화면 위에 표시한다.</summary>
    public void OpenFinal(Canvas resultCanvas)
    {
        if (_ledger == null || !_ledger.IsFinalized) return;
        _pager?.SetChartVisible(false, false);
        int order = resultCanvas != null ? resultCanvas.sortingOrder + ResultSortingOffset : GameplaySortingOrder;
        OpenAtSortingOrder(order);
    }

    private void OpenAtSortingOrder(int sortingOrder)
    {
        if (_ledger == null || _settings == null || _style == null) return;
        if (_canvasObject == null) BuildUi();
        _finalPopup = true;
        _panel.SetParent(_canvasObject.transform, false);
        _panel.anchorMin = _settings.RuntimePanelMin; _panel.anchorMax = _settings.RuntimePanelMax;
        _panel.offsetMin = _panel.offsetMax = Vector2.zero;
        ConfigureLayout(false);
        _canvasObject.GetComponent<Canvas>().sortingOrder = sortingOrder;
        _title.text = _ledger.IsFinalized ? "최종 딜 기여도" : "딜 기여도";
        _canvasObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Refresh();
    }

    private void AttachToHud()
    {
        _finalPopup = false;
        _title.text = "딜 그래프";
        _panel.SetParent(_pager.GraphPage, false);
        _panel.anchorMin = Vector2.zero; _panel.anchorMax = Vector2.one;
        // Fill the whole HUD page; the swipe hint overlays the bottom edge.
        _panel.offsetMin = _panel.offsetMax = Vector2.zero;
        _canvasObject.SetActive(false);
        ConfigureLayout(true);
    }

    private void ConfigureLayout(bool compact)
    {
        _compact = compact;
        float textScale = compact ? _settings.HudTextScale : _settings.RuntimeTextScale;
        foreach (var row in _rows.Values)
        {
            row.SetPortraitOnly(compact);
            row.ScaleText(textScale);
        }
        _rowTextScale = textScale;
        _caption.gameObject.SetActive(!compact);
        // The HUD page has no room for the total; the result popup still shows it.
        _total.gameObject.SetActive(!compact);
        Place(_title.rectTransform, new Vector2(.04f, compact ? .84f : .92f), new Vector2(.55f, .98f));
        // Reserve the upper-right corner for the unchanged options button.
        Place(_total.rectTransform, new Vector2(compact ? .48f : .05f, compact ? .84f : .77f), new Vector2(compact ? .85f : .61f, compact ? .98f : .86f));
        _total.alignment = compact ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        _total.fontSizeMax = compact ? 30f : 36f;
        _title.fontSizeMax = compact ? 28f : 22f;
        _dps.fontSizeMax = compact ? 21f : 17f;
        Place(_dps.rectTransform, new Vector2(.04f, compact ? .73f : .79f), new Vector2(compact ? .96f : .95f, compact ? .85f : .90f));
        _dps.alignment = compact ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        Place((RectTransform)_viewport.parent, new Vector2(.025f, compact ? .07f : .025f), new Vector2(.975f, compact ? .72f : .75f));
        var scroll = _viewport.parent.GetComponent<HudDamageSwipeArea>();
        scroll.Pager = compact ? _pager : null;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>기록을 유지하고 화면을 닫는다.</summary>
    public void Close()
    {
        if (_canvasObject != null) _canvasObject.SetActive(false);
        _pager?.SetChartVisible(false);
    }

    private void Update()
    {
        if (!IsOpen && (_pager == null || !_pager.IsGraphExposed)) return;
        float delta = Time.unscaledDeltaTime;
        _refreshClock += delta;
        if (_refreshClock >= _settings.RefreshInterval) { _refreshClock = 0; Refresh(); }
        foreach (var row in _rows.Values)
            if (row.Rect.gameObject.activeSelf) row.Tick(delta);
    }

    private void Refresh()
    {
        _sorted.Clear();
        foreach (var entry in _ledger.Entries)
            if (entry.TotalDamage > 0m) _sorted.Add(entry);
        _sorted.Sort((a, b) =>
        {
            int order = b.TotalDamage.CompareTo(a.TotalDamage);
            return order != 0 ? order : string.CompareOrdinal(a.DisplayName, b.DisplayName);
        });
        foreach (var row in _rows.Values) row.Rect.gameObject.SetActive(row.Entry.TotalDamage > 0m);
        _empty.gameObject.SetActive(_sorted.Count == 0);
        int visibleRows = _compact ? _settings.HudVisibleRows : _settings.VisibleRows;
        int slots = Mathf.Max(visibleRows, _sorted.Count);
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            _viewport.rect.height * slots / visibleRows);
        decimal max = _sorted.Count > 0 ? _sorted[0].TotalDamage : 0;
        for (int i = 0; i < _sorted.Count; i++)
        {
            var entry = _sorted[i];
            if (!_rows.TryGetValue(entry, out var row))
            {
                Color color = entry.Source is TotemData totem ? totem.descriptionHighlightColor : _style.AccentColor;
                if (_settings.Samples != null)
                    foreach (var sample in _settings.Samples)
                        if (sample.Unit == entry.Unit && entry.Unit != null) { color = sample.Color; break; }
                row = new DamageContributionRowView(_content, new DamageChartSample
                { Unit = entry.Unit, Portrait = entry.Portrait, Color = color }, entry, _ui);
                row.ApplyStyle(_style);
                row.SetPortraitOnly(_compact);
                row.ScaleText(_rowTextScale);
                _rows.Add(entry, row);
            }
            row.Rect.gameObject.SetActive(true);
            row.MoveTo(i, _content, slots);
            row.SetTargets(entry.TotalDamage, _ledger.RecentDpsFor(entry), (float)_ledger.Share(entry),
                max > 0 ? (float)(entry.TotalDamage / max) : 0f);
            if (_ledger.IsFinalized) row.Settle();
        }
        _total.text = (_compact ? "누적 " : "") + DamageContributionRowView.Format((double)_ledger.TotalDamage);
        _dps.text = (_ledger.IsFinalized ? "종료 직전 " : "최근 ") + _settings.RecentDpsWindow.ToString("0.#") + "초 DPS\n"
            + DamageContributionRowView.Format((double)_ledger.RecentDps);
        if (_compact) _dps.text = _dps.text.Replace("\n", "  ");
    }

    private void BuildUi()
    {
        _ui = new DamageChartUiFactory(_font);
        _canvasObject = new GameObject("IngameDamageChartCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_canvasObject, gameObject.scene);
        var canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = GameplaySortingOrder;
        var scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(540f, 1168f); scaler.matchWidthOrHeight = .5f;
        var backdrop = _ui.Image("DamageChartBackdrop", _canvasObject.transform, Vector2.zero, Vector2.one,
            new Color(.025f, .05f, .075f, .35f)); // 아래 그리드가 보이게 옅게. 누르면 닫힘
        backdrop.raycastTarget = true;
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var panel = _ui.Image("DamageChartPanel", _canvasObject.transform, _settings.RuntimePanelMin,
            _settings.RuntimePanelMax, _style.PanelColor);
        panel.raycastTarget = true;
        _panel = panel.rectTransform;
        new DamageChartSkinView(panel, _ui).Apply(_style.PanelSprite, _style.FrameShapeSprite, _style.PanelColor);
        _title = _ui.Text("ChartTitle", panel.transform, "딜 기여도", new Vector2(.05f,.92f), new Vector2(.78f,.98f),
            22f, _style.TextColor);
        _caption = _ui.Text("DamageCaption", panel.transform, "전체 누적 피해", new Vector2(.05f,.85f), new Vector2(.60f,.91f),
            16f, _style.MutedColor);
        _total = _ui.Text("TotalDamage", panel.transform, "0", new Vector2(.05f,.77f), new Vector2(.61f,.86f),
            36f, _style.TextColor);
        _dps = _ui.Text("RecentDps", panel.transform, "0", new Vector2(.62f,.79f), new Vector2(.95f,.90f),
            17f, _style.MutedColor, TextAlignmentOptions.Right);
        var scrollRect = _ui.Rect("DamageScroll", panel.transform, new Vector2(.04f,.035f), new Vector2(.96f,.75f));
        var scroll = scrollRect.gameObject.AddComponent<HudDamageSwipeArea>();
        _viewport = _ui.Rect("Viewport", scrollRect, Vector2.zero, Vector2.one);
        var viewportImage = _viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.clear; viewportImage.raycastTarget = true;
        _viewport.gameObject.AddComponent<RectMask2D>();
        _content = _ui.Rect("DamageRows", _viewport, new Vector2(0,1), Vector2.one);
        _content.pivot = new Vector2(.5f,1);
        scroll.viewport = _viewport; scroll.content = _content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        _empty = _ui.Text("NoDamage", _viewport, "아직 피해 기록이 없어요", new Vector2(.05f,.40f),
            new Vector2(.95f,.60f), 21f, _style.MutedColor, TextAlignmentOptions.Center);
        Canvas.ForceUpdateCanvases();
        _canvasObject.SetActive(false);
    }

    private void OnDisable() => Close();
    private void OnDestroy()
    {
        if (_menu != null) _menu.DamageChartRequested.RemoveListener(Open);
        foreach (var row in _rows.Values) row.Dispose();
        if (_canvasObject != null) Destroy(_canvasObject);
    }
}
