using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 햄버거 딜표 버튼에서 실제 유닛·토템 피해를 C 가로막대 시안으로 표시한다.
/// 전투를 보면서 확인하도록 게임을 멈추지 않고, 창은 그리드가 보이게 화면 위쪽에 둔다(위치는 DamageChartSettings).
/// 닫기는 바깥 누르기로만 한다 (사용자 10-13 "딜표 X 지우고 일시정지 안 해도 돼").
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

    /// <summary>현재 표시 상태.</summary>
    public bool IsOpen => _canvasObject != null && _canvasObject.activeSelf;
    /// <summary>딜표 창 영역 (튜토리얼 안내 위치). 처음 열기 전에는 null.</summary>
    public RectTransform PanelArea => _panel;
    /// <summary>현재 표시 중인 실제 피해 출처 종류 수.</summary>
    public int VisibleRowCount => _sorted.Count;
    /// <summary>정확한 전투 집계.</summary>
    public DamageContributionLedger Ledger => _ledger;

    private void Start()
    {
        if (_menu != null) _menu.DamageChartRequested.AddListener(Open);
    }

    /// <summary>실제 누적 피해를 표시한다. 전투는 계속 진행된다.</summary>
    public void Open() => OpenAtSortingOrder(GameplaySortingOrder);

    /// <summary>확정된 이번 런의 딜표를 결과 화면 위에 표시한다.</summary>
    public void OpenFinal(Canvas resultCanvas)
    {
        if (_ledger == null || !_ledger.IsFinalized) return;
        int order = resultCanvas != null ? resultCanvas.sortingOrder + ResultSortingOffset : GameplaySortingOrder;
        OpenAtSortingOrder(order);
    }

    private void OpenAtSortingOrder(int sortingOrder)
    {
        if (_ledger == null || _settings == null || _style == null) return;
        if (_canvasObject == null) BuildUi();
        _canvasObject.GetComponent<Canvas>().sortingOrder = sortingOrder;
        _title.text = _ledger.IsFinalized ? "최종 딜 기여도" : "딜 기여도";
        _canvasObject.SetActive(true);
        Refresh();
    }

    /// <summary>기록을 유지하고 화면을 닫는다.</summary>
    public void Close()
    {
        if (_canvasObject != null) _canvasObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;
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
        int slots = Mathf.Max(_settings.VisibleRows, _sorted.Count);
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            _viewport.rect.height * slots / _settings.VisibleRows);
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
                row.ScaleText(_settings.RuntimeTextScale);
                _rows.Add(entry, row);
            }
            row.Rect.gameObject.SetActive(true);
            row.MoveTo(i, _content, slots);
            row.SetTargets(entry.TotalDamage, _ledger.RecentDpsFor(entry), (float)_ledger.Share(entry),
                max > 0 ? (float)(entry.TotalDamage / max) : 0f);
            if (_ledger.IsFinalized) row.Settle();
        }
        _total.text = DamageContributionRowView.Format((double)_ledger.TotalDamage);
        _dps.text = (_ledger.IsFinalized ? "종료 직전 " : "최근 ") + _settings.RecentDpsWindow.ToString("0.#") + "초 DPS\n"
            + DamageContributionRowView.Format((double)_ledger.RecentDps);
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
        _ui.Text("DamageCaption", panel.transform, "전체 누적 피해", new Vector2(.05f,.85f), new Vector2(.60f,.91f),
            16f, _style.MutedColor);
        _total = _ui.Text("TotalDamage", panel.transform, "0", new Vector2(.05f,.77f), new Vector2(.61f,.86f),
            36f, _style.TextColor);
        _dps = _ui.Text("RecentDps", panel.transform, "0", new Vector2(.62f,.79f), new Vector2(.95f,.90f),
            17f, _style.MutedColor, TextAlignmentOptions.Right);
        var scrollRect = _ui.Rect("DamageScroll", panel.transform, new Vector2(.04f,.035f), new Vector2(.96f,.75f));
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
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
    }

    private void OnDisable() => Close();
    private void OnDestroy()
    {
        if (_menu != null) _menu.DamageChartRequested.RemoveListener(Open);
        foreach (var row in _rows.Values) row.Dispose();
        if (_canvasObject != null) Destroy(_canvasObject);
    }
}
