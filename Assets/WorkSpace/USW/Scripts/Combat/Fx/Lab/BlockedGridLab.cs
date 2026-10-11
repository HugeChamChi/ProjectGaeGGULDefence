using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>배치 불가 칸의 네 시안을 터치 버튼과 배치 시도로 비교하는 독립 FxLab.</summary>
public sealed class BlockedGridLab : MonoBehaviour, IFxLabPlayable
{
    private const int Columns = 6;
    private const int Rows = 4;
    private const int CellCount = Columns * Rows;
    private const float CellGap = 5f;
    private const float IntroSeconds = .32f;
    private const float FeedbackSeconds = .55f;
    private static readonly string[] PatternNames = { "한 칸", "2×2", "한 줄", "흩어짐" };
    private static readonly string[] StrengthNames = { "연함", "보통", "진함" };
    private static readonly float[] Strengths = { .65f, 1f, 1.35f };
    private static readonly Color PanelColor = new Color(.045f, .085f, .12f, .93f);
    private static readonly Color ButtonColor = new Color(.12f, .19f, .24f, .96f);
    private static readonly Color SelectedColor = new Color(.22f, .40f, .43f, 1f);
    private static readonly Color TextColor = new Color(.91f, .95f, .95f, 1f);

    [SerializeField] private Camera _camera;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Texture2D _background;
    [SerializeField] private Sprite[] _unitSprites = System.Array.Empty<Sprite>();
    [SerializeField] private BlockedGridStyle[] _styles = System.Array.Empty<BlockedGridStyle>();

    private readonly BlockedGridCellGraphic[] _cells = new BlockedGridCellGraphic[CellCount];
    private readonly TMP_Text[] _cellLabels = new TMP_Text[CellCount];
    private readonly Image[] _units = new Image[CellCount];
    private readonly bool[] _placed = new bool[CellCount];
    private readonly float[] _feedbackAt = new float[CellCount];
    private Button[] _styleButtons;
    private readonly Button[] _patternButtons = new Button[4];
    private readonly Button[] _strengthButtons = new Button[3];
    private Button _overlayButton, _labelsButton, _unitsButton, _motionButton, _areaButton;
    private TMP_Text _description, _status, _styleTitle, _legend;
    private RectTransform _stage, _board;
    private GridLayoutGroup _layout;
    private RawImage _backdrop;
    private Vector2 _lastStageSize;
    private int _selected;
    private int _pattern = 1;
    private int _strength = 1;
    private bool _labels = true;
    private bool _showUnits = true;
    private bool _motion = true;
    private bool _overlay = true;
    private bool _areaEnabled = true;
    private bool _unscaled = true;
    private float _introAt;
    private int _denied, _accepted;

    /// <summary>시안 수.</summary>
    public int StyleCount => _styles.Length;
    /// <summary>선택 시안 인덱스.</summary>
    public int SelectedIndex => _selected;
    /// <summary>차단 배치 인덱스.</summary>
    public int PatternIndex => _pattern;
    /// <summary>강도 인덱스.</summary>
    public int StrengthIndex => _strength;
    /// <summary>거부된 배치 시도 수.</summary>
    public int DeniedCount => _denied;
    /// <summary>허용된 배치 시도 수.</summary>
    public int AcceptedCount => _accepted;
    /// <summary>칸 라벨 표시 여부.</summary>
    public bool LabelsShown => _labels;
    /// <summary>예시 유닛 표시 여부.</summary>
    public bool UnitsShown => _showUnits;
    /// <summary>등장·미세한 호흡 표시 여부.</summary>
    public bool MotionEnabled => _motion;
    /// <summary>차단 도형 표시 여부. 배치 판정은 유지한다.</summary>
    public bool OverlayVisible => _overlay;
    /// <summary>이 실험실의 표시 시계 선택.</summary>
    public bool UseUnscaledTime { get => _unscaled; set => _unscaled = value; }
    /// <summary>현재 차단 칸 수.</summary>
    public int BlockedCount
    {
        get { int count = 0; for (int i = 0; i < CellCount; i++) if (IsCellBlocked(i)) count++; return count; }
    }
    private float Clock => _unscaled ? Time.unscaledTime : Time.time;
    private BlockedGridStyle Current => _styles.Length > 0 ? _styles[Mathf.Clamp(_selected, 0, _styles.Length - 1)] : null;

    private void Awake()
    {
        BuildUi();
        RestoreRecommended();
    }

    /// <summary>시안을 선택한다.</summary>
    public void SelectStyle(int index)
    {
        if (index < 0 || index >= _styles.Length) return;
        _selected = index; Play(); Refresh();
    }

    /// <summary>차단 칸 배치를 선택한다.</summary>
    public void SetPattern(int index)
    {
        if (index < 0 || index >= PatternNames.Length) return;
        _pattern = index; _areaEnabled = true; Play(); Refresh();
    }

    /// <summary>시안 표시 강도를 선택한다.</summary>
    public void SetStrength(int index)
    {
        if (index < 0 || index >= Strengths.Length) return;
        _strength = index; Refresh();
    }

    /// <summary>시각 도형 표시를 전환한다. 실제 차단 판정은 바꾸지 않는다.</summary>
    public void ToggleOverlay() { _overlay = !_overlay; Refresh(); }
    /// <summary>칸 이름 표시를 전환한다.</summary>
    public void ToggleLabels() { _labels = !_labels; Refresh(); }
    /// <summary>가능한 칸의 예시 유닛 표시를 전환한다.</summary>
    public void ToggleUnits() { _showUnits = !_showUnits; Refresh(); }
    /// <summary>반복 점멸 없이 미세한 호흡과 등장만 전환한다.</summary>
    public void ToggleMotion() { _motion = !_motion; Refresh(); }
    /// <summary>실험실의 차단 상태 전체를 해제하거나 복원한다.</summary>
    public void ToggleBlockedArea() { _areaEnabled = !_areaEnabled; Play(); Refresh(); }

    /// <summary>권장 시안 A·2×2·보통 강도의 기본 비교 상태로 복원한다.</summary>
    public void RestoreRecommended()
    {
        _selected = 0; _pattern = 1; _strength = 1;
        _labels = _showUnits = _motion = _overlay = _areaEnabled = true;
        Play(); Refresh();
    }

    /// <summary>동일 설정에서 등장과 배치 시도 기록을 다시 시작한다.</summary>
    public void Play()
    {
        _introAt = Clock; _denied = _accepted = 0;
        for (int i = 0; i < CellCount; i++) { _feedbackAt[i] = -100f; _placed[i] = false; }
        if (_status != null) { _status.text = "칸을 눌러 배치 가능 여부를 확인하세요"; _status.color = TextColor; }
        Refresh();
    }

    /// <summary>실험실의 논리상 해당 칸에 배치할 수 없는지 조회한다.</summary>
    public bool IsCellBlocked(int index)
    {
        if (!_areaEnabled || index < 0 || index >= CellCount) return false;
        int column = index % Columns, row = index / Columns;
        switch (_pattern)
        {
            case 0: return row == 1 && column == 2;
            case 1: return (row == 1 || row == 2) && (column == 2 || column == 3);
            case 2: return row == 1;
            default: return index == 2 || index == 7 || index == 11 || index == 15 || index == 20;
        }
    }

    /// <summary>칸을 터치했을 때 실제 차단 상태로 배치를 거부하거나 예시 유닛을 표시한다.</summary>
    public bool TryPlaceAt(int index)
    {
        if (index < 0 || index >= CellCount) return false;
        _feedbackAt[index] = Clock;
        bool allowed = !IsCellBlocked(index);
        if (allowed)
        {
            _accepted++; _placed[index] = true;
            _status.text = "배치 가능 · 예시 유닛을 놓았습니다";
        }
        else
        {
            _denied++; _status.text = "배치 불가 · 봉인된 칸입니다";
        }
        _status.color = allowed ? new Color(.63f, .94f, .83f) : new Color(1f, .64f, .58f);
        Refresh(); return allowed;
    }

    private void LateUpdate()
    {
        if (_stage == null) return;
        Vector2 available = _stage.rect.size;
        if ((available - _lastStageSize).sqrMagnitude > .5f)
        {
            _lastStageSize = available;
            float size = Mathf.Max(1f, Mathf.Min((available.x - CellGap * (Columns - 1)) / Columns, (available.y - CellGap * (Rows - 1)) / Rows));
            _layout.cellSize = Vector2.one * size;
            _board.sizeDelta = new Vector2(size * Columns + CellGap * (Columns - 1), size * Rows + CellGap * (Rows - 1));
        }
        if (_backdrop != null && _background != null)
        {
            RectTransform backgroundRect = (RectTransform)_backdrop.transform;
            float targetAspect = backgroundRect.rect.width / Mathf.Max(1f, backgroundRect.rect.height);
            float imageAspect = (float)_background.width / _background.height;
            if (targetAspect < imageAspect)
            {
                float width = targetAspect / imageAspect;
                _backdrop.uvRect = new Rect((1f - width) * .5f, 0f, width, 1f);
            }
            else
            {
                float height = imageAspect / targetAspect;
                _backdrop.uvRect = new Rect(0f, (1f - height) * .5f, 1f, height);
            }
        }
        float elapsed = Clock - _introAt;
        float intro = _motion ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / IntroSeconds)) : 1f;
        float breath = _motion ? .985f + .015f * Mathf.Sin(elapsed * 1.8f) : 1f;
        for (int i = 0; i < CellCount; i++)
            _cells[i].SetFeedback(intro * breath, Mathf.Clamp01(1f - (Clock - _feedbackAt[i]) / FeedbackSeconds));
    }

    private void Refresh()
    {
        if (_styleTitle == null) return;
        var style = Current;
        _styleTitle.text = style != null ? style.DisplayName : "시안 데이터 없음";
        _description.text = style != null ? style.Description : "빌더에서 시안 SO를 연결하세요";
        _legend.text = "밝은 칸  배치 가능     /     어두운 칸  배치 불가  " + BlockedCount + "칸";
        for (int i = 0; i < CellCount; i++)
        {
            bool blocked = IsCellBlocked(i);
            _cells[i].Configure(style, blocked, Strengths[_strength], _overlay);
            bool showLabel = _labels && _overlay && blocked && style != null && style.Kind != BlockedGridStyleKind.HatchLock;
            _cellLabels[i].text = showLabel ? "봉인" : "";
            _cellLabels[i].gameObject.SetActive(showLabel);
            bool sample = i == 0 || i == 13 || i == 23;
            _units[i].gameObject.SetActive(_showUnits && !blocked && (_placed[i] || sample));
        }
        Highlight(_styleButtons, _selected); Highlight(_patternButtons, _pattern); Highlight(_strengthButtons, _strength);
        SetButtonLabel(_overlayButton, _overlay ? "표시 ON" : "표시 OFF", _overlay);
        bool supportsLabels = style != null && style.Kind != BlockedGridStyleKind.HatchLock;
        _labelsButton.interactable = supportsLabels;
        SetButtonLabel(_labelsButton, supportsLabels ? (_labels ? "이름 ON" : "이름 OFF") : "이름 없음", supportsLabels && _labels);
        SetButtonLabel(_unitsButton, _showUnits ? "유닛 ON" : "유닛 OFF", _showUnits);
        SetButtonLabel(_motionButton, _motion ? "움직임 ON" : "움직임 OFF", _motion);
        SetButtonLabel(_areaButton, _areaEnabled ? "전체 해제" : "차단 복원", false);
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("BlockedGridLabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _camera; canvas.planeDistance = 1f;
        canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(540f, 1168f); scaler.matchWidthOrHeight = .5f;
        RectTransform root = (RectTransform)canvasObject.transform;
        var backdrop = NewRect("BattlefieldBackdrop", root, Vector2.zero, Vector2.one);
        _backdrop = backdrop.gameObject.AddComponent<RawImage>(); _backdrop.texture = _background; _backdrop.raycastTarget = false;
        Panel(root, "BackdropTint", Vector2.zero, Vector2.one, new Color(.025f, .055f, .065f, .24f));
        var heading = Panel(root, "Heading", new Vector2(.035f, .815f), new Vector2(.965f, .975f), PanelColor);
        Text(heading, "LabTitle", "배치 불가 구역", new Vector2(.04f, .64f), new Vector2(.96f, .95f), 30f, FontStyles.Bold);
        Text(heading, "Scope", "봉인·비활성 칸의 그리드 표시 비교", new Vector2(.04f, .49f), new Vector2(.96f, .64f), 16f);
        _styleButtons = new Button[_styles.Length];
        string[] shortNames = { "A · 사선 잠금", "B · 모서리", "C · 체인", "D · 큰 X" };
        for (int i = 0; i < _styles.Length; i++)
        {
            int choice = i;
            _styleButtons[i] = Button(heading, "Style_" + i, shortNames[Mathf.Min(i, shortNames.Length - 1)], new Vector2(.025f + i * .24f, .08f), new Vector2(.245f + i * .24f, .40f), () => SelectStyle(choice));
        }
        _styleTitle = Text(root, "CurrentStyle", "", new Vector2(.055f, .762f), new Vector2(.945f, .803f), 23f, FontStyles.Bold);
        _description = Text(root, "StyleDescription", "", new Vector2(.06f, .711f), new Vector2(.94f, .760f), 15f);
        _stage = NewRect("GridStage", root, new Vector2(.055f, .342f), new Vector2(.945f, .696f));
        Panel(_stage, "ComparisonBoardBackdrop", Vector2.zero, Vector2.one, new Color(.04f, .08f, .10f, .88f));
        _board = NewRect("Board_6x4", _stage, Vector2.one * .5f, Vector2.one * .5f);
        _layout = _board.gameObject.AddComponent<GridLayoutGroup>();
        _layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; _layout.constraintCount = Columns;
        _layout.spacing = Vector2.one * CellGap; _layout.childAlignment = TextAnchor.MiddleCenter;
        for (int i = 0; i < CellCount; i++)
        {
            int index = i;
            var rect = NewRect("Cell_" + i, _board, Vector2.zero, Vector2.zero);
            _cells[i] = rect.gameObject.AddComponent<BlockedGridCellGraphic>();
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = _cells[i]; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => TryPlaceAt(index));
            var imageRect = NewRect("ExampleUnit", rect, new Vector2(.12f, .12f), new Vector2(.88f, .88f));
            _units[i] = imageRect.gameObject.AddComponent<Image>(); _units[i].raycastTarget = false; _units[i].preserveAspect = true;
            if (_unitSprites.Length > 0) _units[i].sprite = _unitSprites[i % _unitSprites.Length];
            _cellLabels[i] = Text(rect, "SealLabel", "", new Vector2(.06f, .04f), new Vector2(.94f, .24f), 12f);
            _cellLabels[i].color = new Color(.78f, .86f, .86f);
        }
        _legend = Text(root, "Legend", "", new Vector2(.05f, .300f), new Vector2(.95f, .336f), 14f);
        var controls = Panel(root, "Controls", new Vector2(.035f, .034f), new Vector2(.965f, .293f), PanelColor);
        Text(controls, "PatternTitle", "차단 배치", new Vector2(.04f, .855f), new Vector2(.96f, .97f), 16f, FontStyles.Bold);
        for (int i = 0; i < PatternNames.Length; i++)
        {
            int choice = i;
            _patternButtons[i] = Button(controls, "Pattern_" + i, PatternNames[i], new Vector2(.025f + i * .24f, .705f), new Vector2(.245f + i * .24f, .845f), () => SetPattern(choice));
        }
        for (int i = 0; i < StrengthNames.Length; i++)
        {
            int choice = i;
            _strengthButtons[i] = Button(controls, "Strength_" + i, StrengthNames[i], new Vector2(.025f + i * .24f, .520f), new Vector2(.245f + i * .24f, .660f), () => SetStrength(choice));
        }
        _overlayButton = Button(controls, "Overlay", "", new Vector2(.745f, .520f), new Vector2(.965f, .660f), ToggleOverlay);
        _labelsButton = Button(controls, "Labels", "", new Vector2(.025f, .335f), new Vector2(.325f, .475f), ToggleLabels);
        _unitsButton = Button(controls, "Units", "", new Vector2(.350f, .335f), new Vector2(.650f, .475f), ToggleUnits);
        _motionButton = Button(controls, "Motion", "", new Vector2(.675f, .335f), new Vector2(.975f, .475f), ToggleMotion);
        Button(controls, "Restart", "다시 보기", new Vector2(.025f, .145f), new Vector2(.325f, .290f), Play);
        _areaButton = Button(controls, "Area", "", new Vector2(.350f, .145f), new Vector2(.650f, .290f), ToggleBlockedArea);
        Button(controls, "Recommended", "권장 설정", new Vector2(.675f, .145f), new Vector2(.975f, .290f), RestoreRecommended);
        _status = Text(controls, "PlacementStatus", "", new Vector2(.025f, .015f), new Vector2(.975f, .13f), 14f);
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        var rect = NewRect(name, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }

    private TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, FontStyles style = FontStyles.Normal)
    {
        var rect = NewRect(name, parent, min, max);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = _font; text.text = value;
        text.fontSize = size; text.fontStyle = style; text.color = TextColor; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = true;
        text.fontSizeMin = size * .75f; text.fontSizeMax = size; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max, UnityAction action)
    {
        var rect = Panel(parent, name, min, max, ButtonColor);
        var image = rect.GetComponent<Image>(); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);
        Text(rect, "Label", label, new Vector2(.03f, .02f), new Vector2(.97f, .98f), 16f);
        return button;
    }

    private static void Highlight(Button[] buttons, int selected)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null)
            buttons[i].targetGraphic.color = i == selected ? SelectedColor : ButtonColor;
    }

    private static void SetButtonLabel(Button button, string label, bool selected)
    {
        if (button == null) return;
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.targetGraphic.color = selected ? SelectedColor : ButtonColor;
    }
}
