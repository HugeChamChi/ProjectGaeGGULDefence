using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Standalone level-up, totem and glossary preview using one externally passed preference.</summary>
public sealed class DescriptionModeLab : MonoBehaviour
{
    [SerializeField] private RectTransform _canvas;
    [SerializeField] private RectTransform _cardContainer;
    [SerializeField] private LevelUpCardUI _cardPrefab;
    [SerializeField] private DescriptionModeLabData _data;
    [SerializeField] private Sprite _rounded;
    [SerializeField] private Shader _blur;
    private readonly DescriptionModeState _state = new DescriptionModeState();
    private readonly List<CardView> _cards = new List<CardView>();
    private TermLinker _linker;
    private TermPopup _popup;
    private RectTransform _safeRoot;
    private RectTransform _toggleRect;
    private TMP_Text _fontSource;
    private TMP_Text _heading;
    private TMP_Text _status;
    private int _activeCard;
    private bool _totems;
    private bool _belowHeading;

    private sealed class CardView
    {
        public GameObject Root;
        public TMP_Text Text;
        public string[] Descriptions;
        public List<string> Terms;
    }

    /// <summary>Shared state of this standalone Play session.</summary>
    public DescriptionModeState State => _state;
    /// <summary>Popup used by both screens.</summary>
    public TermPopup Popup => _popup;
    /// <summary>Number of intentional card presses; glossary/toggle presses must not increment this.</summary>
    public int SelectionCount { get; private set; }
    /// <summary>Which sample screen is visible.</summary>
    public bool ShowingTotems => _totems;
    /// <summary>Visible description, for interaction and overflow checks.</summary>
    public TMP_Text GetDescription(int index) => _cards[index].Text;

    private void Start()
    {
        _fontSource = _cardPrefab.GetComponentInChildren<TMP_Text>(true);
        _linker = new TermLinker(_data.Glossary, new Color(0.18f, 0.6f, 0.8f), new Color(1f, 0.62f, 0.1f), string.Empty);
        _safeRoot = Rect("DescriptionSafeArea", _canvas);
        Stretch(_safeRoot);
        _safeRoot.gameObject.AddComponent<SafeAreaApplier>();
        BuildToolbar();
        _popup = TermPopup.Create(_safeRoot, _fontSource, _rounded, _blur);
        _popup.ConfigureDescriptionMode(_state);
        _popup.SiblingTapped += ShowTerm;
        _state.OnChanged += Refresh;
        ShowSelections();
        var backdrop = _canvas.GetComponentInChildren<TierRevealFx>(true);
        if (backdrop != null) backdrop.Play(Tier.Legend);
    }

    /// <summary>Switches sample screens without resetting the preference.</summary>
    public void ShowSelections() => ShowScreen(false);
    /// <summary>Shows totem presentation samples without running reward/gameplay managers.</summary>
    public void ShowTotems() => ShowScreen(true);

    private void ShowScreen(bool totems)
    {
        _popup.HideImmediately();
        _totems = totems;
        _heading.text = totems ? "토템 선택" : "레벨업 선택";
        foreach (var card in _cards) { card.Root.SetActive(false); Destroy(card.Root); }
        _cards.Clear();
        var layout = _cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        var entries = totems ? _data.Totems : _data.Selections;
        for (int i = 0; i < entries.Length; i++) BuildCard(entries[i], i);
        Refresh(_state.Current);
        _popup.transform.SetAsLastSibling();
    }

    private void BuildCard(DescriptionModeLabEntry entry, int index)
    {
        var descriptions = entry.Resolve();
        GameObject root;
        TMP_Text body;
        if (entry.Selection != null)
        {
            var card = Instantiate(_cardPrefab, _safeRoot);
            card.Setup(entry.Selection, _ => SelectCard(), descriptions[0]);
            root = card.gameObject;
            body = null;
            foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
                if (text.text == descriptions[0]) { body = text; break; }
            if (body == null) throw new System.InvalidOperationException("Card description reference missing");
            // Fixed preview bounds reserve room for the longest sample; only text changes with the toggle.
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(960f, 380f);
            rt.anchoredPosition = new Vector2(0f, -660f - index * 410f);
            body.enableAutoSizing = false;
            body.fontSize = 34f;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 170f);
            body.rectTransform.anchoredPosition = new Vector2(body.rectTransform.anchoredPosition.x, -38f);
            foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
                if (text != body) text.rectTransform.anchoredPosition = new Vector2(text.rectTransform.anchoredPosition.x, 88f);
        }
        else
        {
            var rt = Rect("TotemChoice_" + index, _safeRoot);
            root = rt.gameObject;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(980f, 380f);
            rt.anchoredPosition = new Vector2(0f, -660f - index * 410f);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(1f, 0.92f, 0.84f);
            bg.sprite = _rounded;
            bg.type = Image.Type.Sliced;
            var button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(SelectCard);
            var icon = Rect("Icon", rt).gameObject.AddComponent<Image>();
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(170f, 190f);
            icon.rectTransform.anchoredPosition = new Vector2(112f, 0f);
            icon.sprite = entry.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var title = Label("Title", rt, entry.Title, 40f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(224f, -76f);
            title.rectTransform.offsetMax = new Vector2(-24f, -20f);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            body = Label("Description", rt, descriptions[0], 32f);
            Stretch(body.rectTransform);
            body.rectTransform.offsetMin = new Vector2(224f, 20f);
            body.rectTransform.offsetMax = new Vector2(-24f, -88f);
            body.alignment = TextAlignmentOptions.TopLeft;
        }
        int cardIndex = index;
        var link = body.gameObject.AddComponent<TermLinkTapTarget>();
        link.Configure(root, (_, id) =>
        {
            if (!id.StartsWith(TermLinker.LinkPrefix, System.StringComparison.Ordinal)) return;
            _activeCard = cardIndex;
            ShowTerm(id.Substring(TermLinker.LinkPrefix.Length));
        }, 12f);
        _cards.Add(new CardView { Root = root, Text = body, Descriptions = descriptions });
    }

    private void SelectCard()
    {
        if (_popup.IsOpen) return;
        SelectionCount++;
        _status.text = "선택 입력 " + SelectionCount + "회 · 실험실에서는 효과를 적용하지 않습니다";
    }

    /// <summary>Opens a linked glossary entry from the current card.</summary>
    public void ShowTerm(string id)
    {
        if (!_data.Glossary.TryFind(id, out var entry)) return;
        var siblings = new List<TermEntry>();
        if (_activeCard >= 0 && _activeCard < _cards.Count)
            foreach (var termId in _cards[_activeCard].Terms)
                if (_data.Glossary.TryFind(termId, out var other)) siblings.Add(other);
        _popup.Show(entry, siblings);
    }

    private void Refresh(DescriptionMode mode)
    {
        foreach (var card in _cards)
        {
            string text = _linker.AddLinks(card.Descriptions[(int)mode]);
            card.Text.text = text;
            card.Terms = TermLinker.LinkedIds(text);
        }
    }

    private void BuildToolbar()
    {
        var row = Rect("LabNavigation", _safeRoot);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = Vector2.one;
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-56f, 100f);
        row.anchoredPosition = new Vector2(0f, -20f);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        Button(row, "레벨업", ShowSelections);
        Button(row, "토템", ShowTotems);
        Button(row, "배치 A / B", TogglePlacement);
        _heading = Label("Heading", _safeRoot, "레벨업 선택", 50f);
        _heading.rectTransform.anchorMin = _heading.rectTransform.anchorMax = new Vector2(0f, 1f);
        _heading.rectTransform.pivot = new Vector2(0f, 1f);
        _heading.rectTransform.sizeDelta = new Vector2(580f, 110f);
        _heading.rectTransform.anchoredPosition = new Vector2(48f, -190f);
        _heading.alignment = TextAlignmentOptions.MidlineLeft;
        var toggle = DescriptionModeToggle.Create(_safeRoot, _fontSource, _rounded, _state);
        _toggleRect = (RectTransform)toggle.transform;
        PositionToggle();
        _status = Label("Status", _safeRoot, "FxLab · 문구/배치 미리보기 · 용어를 눌러 설명 열기", 28f);
        _status.rectTransform.anchorMin = new Vector2(0f, 0f);
        _status.rectTransform.anchorMax = new Vector2(1f, 0f);
        _status.rectTransform.pivot = new Vector2(0.5f, 0f);
        _status.rectTransform.sizeDelta = new Vector2(-60f, 100f);
        _status.rectTransform.anchoredPosition = new Vector2(0f, 30f);
    }

    /// <summary>Compares upper-right and right-aligned below-heading placements.</summary>
    public void TogglePlacement() { _belowHeading = !_belowHeading; PositionToggle(); }
    private void PositionToggle()
    {
        _toggleRect.anchorMin = _toggleRect.anchorMax = Vector2.one;
        _toggleRect.pivot = Vector2.one;
        _toggleRect.anchoredPosition = new Vector2(-48f, _belowHeading ? -325f : -193f);
    }

    private void Button(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var rt = Rect(label, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = new Color(0.20f, 0.22f, 0.29f);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var text = Label("Label", rt, label, 34f);
        Stretch(text.rectTransform);
    }

    private TMP_Text Label(string name, Transform parent, string content, float size)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _fontSource.font;
        text.fontSharedMaterial = _fontSource.fontSharedMaterial;
        text.fontSize = size;
        text.text = content;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        return rt;
    }
    private static void Stretch(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    private void OnDestroy()
    {
        _state.OnChanged -= Refresh;
        if (_popup != null) { _popup.ConfigureDescriptionMode(null); _popup.SiblingTapped -= ShowTerm; }
    }
}
