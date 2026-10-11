using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하단 정보 패널 (강화예시 구성).
/// 위: 큰 아이콘(레벨 표시 포함) / "강화 3-1" / 스탯 줄 (왼쪽 스탯 이름, 오른쪽 "+2% → +4%").
/// 패널을 좌우로 밀면 추천 방식이 바뀐다 (비교 테스트용, 맨 아래 점으로 표시):
///   0번 버튼형: 아래 [추천 강화] (지금 선택과 상관없이 추천 노드를 올림) | [강화] (선택한 노드)
///   1번 토글형: 버튼 줄 왼쪽 [추천 따라가기] 토글 + 오른쪽 [강화] (토글 켜짐 = 추천 노드, 꺼짐 = 선택한 노드)
/// 선택한 노드가 최대 레벨이면 [강화] 자리에 MAX. 안내 문구는 두지 않는다.
/// </summary>
public sealed class ResearchInfoPanel
{
    private const float Inner = 36f;
    private const float IconSize = 190f;
    private const float TitleFont = 44f;
    private const float StatFont = 40f;
    private const float ValueFont = 44f;
    private const float StripPadding = 16f;
    private const float RowHeight = 112f;
    private const float RowBottom = 38f;
    private const float SlotGap = 24f;
    private const float SingleButtonWidth = 440f;
    private const float ButtonFont = 44f;
    private const float MaxFont = 72f;
    private const float ToggleFont = 30f;
    private const float ToggleLabelWidth = 220f;
    private const float ToggleGap = 16f;
    private const float TrackWidth = 92f;
    private const float TrackHeight = 50f;
    private const float KnobPadding = 6f;
    private const float DotSize = 14f;
    private const float DotGap = 16f;
    private const float DotBottom = 22f;
    private const int ModeCount = 2;
    private const string Arrow = "  ›  ";
    private static readonly Color PanelColor = new Color32(0x00, 0x7E, 0x90, 0xFF);
    private static readonly Color CardColor = new Color32(0x00, 0x6B, 0x7D, 0xFF);
    private static readonly Color StripColor = new Color32(0x00, 0x94, 0xA6, 0xFF);
    private static readonly Color Teal = new Color32(0x10, 0xB5, 0xC1, 0xFF);
    private static readonly Color Highlight = new Color32(0xFF, 0xEC, 0x35, 0xFF);

    private enum Style { Primary, Secondary, Disabled }

    // [강화]/[선택 바꾸기]/[잠김] 버튼과 MAX 글자가 같은 자리를 쓴다.
    private sealed class MainSlot
    {
        public Button Button;
        public Image Image;
        public TextMeshProUGUI Text;
        public TextMeshProUGUI Max;
        public GameObject MaxRoot;
        public TextMeshProUGUI Cost;
    }

    private readonly ResearchViewSettings _s;
    private ResearchProgress _progress;
    private readonly RectTransform _iconSlot;
    private readonly TextMeshProUGUI _title;
    private readonly TextMeshProUGUI _statName;
    private readonly TextMeshProUGUI _value;
    private int _gold;
    private readonly GameObject[] _rows = new GameObject[ModeCount];
    private readonly MainSlot[] _main = new MainSlot[ModeCount];
    private readonly Image[] _dots = new Image[ModeCount];
    private readonly Button _recommend;
    private readonly Image _recommendImage;
    private readonly TextMeshProUGUI _recommendText;
    private readonly TextMeshProUGUI _recommendCost;
    private readonly GameObject _toggle;
    private readonly Image _track;
    private readonly RectTransform _knob;
    private readonly TextMeshProUGUI _toggleLabel;
    private readonly Image _icon;
    private readonly TextMeshProUGUI _level;
    private ResearchNodeData _node;
    private bool _follow;
    private readonly float _iconSize;

    /// <summary>선택한 노드를 올리라는 요청.</summary>
    public event Action<ResearchNodeData> OnUpgradeClicked;
    /// <summary>추천 노드를 올리라는 요청 (0번 [추천 강화], 또는 1번에서 토글 켜짐 + [강화]).</summary>
    public event Action OnRecommendClicked;
    /// <summary>[선택 바꾸기]를 눌렀을 때 (택1에서 막힌 노드).</summary>
    public event Action<ResearchNodeData> OnChangeChoiceClicked;
    /// <summary>[추천 따라가기] 토글을 눌렀을 때 (바뀐 값).</summary>
    public event Action<bool> OnFollowToggled;
    /// <summary>방식이 바뀌었을 때 (0 = 버튼형, 1 = 토글형).</summary>
    public event Action<int> OnModeChanged;

    /// <summary>지금 방식 (0 = 버튼형, 1 = 토글형).</summary>
    public int Mode { get; private set; }

    public ResearchInfoPanel(RectTransform panel, ResearchViewSettings settings)
    {
        _s = settings;
        _iconSize = IconSize;
        var topLeft = new Vector2(0f, 1f);

        // 패널 어디를 밀어도 방식이 바뀌게 패널 바탕이 드래그를 받는다.
        if (panel.TryGetComponent<Image>(out var panelImage))
        {
            panelImage.sprite = null;
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;
        }
        var card = ResearchUi.NewImage(panel, "InfoCard", _s.RoundedFill, Vector2.zero, Vector2.zero);
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        card.color = CardColor;
        card.rectTransform.anchorMin = new Vector2(0f, 1f);
        card.rectTransform.anchorMax = Vector2.one;
        card.rectTransform.offsetMin = new Vector2(16f, -250f);
        card.rectTransform.offsetMax = new Vector2(-16f, -20f);
        panel.gameObject.AddComponent<ResearchSwipeArea>().OnSwipe += dir => SetMode(Mode + dir);

        _iconSlot = ResearchUi.Place(ResearchUi.NewRect(panel, "IconSlot"), topLeft, new Vector2(0.5f, 0.5f),
            new Vector2(Inner + _iconSize * 0.5f, -Inner - _iconSize * 0.5f), new Vector2(_iconSize, _iconSize));
        var frame = ResearchUi.NewImage(_iconSlot, "Frame", _s.RoundedFill, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(frame.rectTransform);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        frame.color = Teal;
        var inset = ResearchUi.NewImage(_iconSlot, "Inset", _s.RoundedFill, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(inset.rectTransform, 12f, 36f, 12f, 12f);
        inset.type = Image.Type.Sliced;
        inset.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        inset.color = new Color32(0x2C, 0x62, 0x73, 0xFF);
        _icon = ResearchUi.NewImage(inset.transform, "Icon", null, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(_icon.rectTransform, 12f, 8f, 12f, 8f);
        _icon.preserveAspect = true;
        var levelRect = ResearchUi.Place(ResearchUi.NewRect(_iconSlot, "Level"), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(_iconSize, 32f));
        _level = ResearchUi.NewText(levelRect, "Text", string.Empty, 26f, _s.FontBold, TextAlignmentOptions.Center);

        float textX = Inner * 2f + IconSize;
        var titleRect = ResearchUi.NewRect(panel, "Title");
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(textX, -116f);
        titleRect.offsetMax = new Vector2(-Inner, -44f);
        _title = ResearchUi.NewText(titleRect, "Text", string.Empty, TitleFont, _s.FontBold, TextAlignmentOptions.BottomLeft);
        _title.color = new Color32(0xC8, 0xF5, 0xFA, 0xFF);
        _title.fontStyle = FontStyles.Bold | FontStyles.Italic;
        _title.enableAutoSizing = true;
        _title.fontSizeMin = 28f;
        _title.fontSizeMax = TitleFont;

        var strip = ResearchUi.NewImage(panel, "StatStrip", _s.RoundedFill, Vector2.zero, Vector2.zero);
        strip.type = Image.Type.Sliced;
        strip.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        strip.color = StripColor;
        var stripRect = strip.rectTransform;
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(0.5f, 1f);
        stripRect.offsetMin = new Vector2(textX, -222f);
        stripRect.offsetMax = new Vector2(-Inner, -138f);
        var stripInner = ResearchUi.Stretch(ResearchUi.NewRect(stripRect, "Inner"), StripPadding, 0f, StripPadding, 0f);
        _statName = ResearchUi.NewText(stripInner, "Stat", string.Empty, StatFont, _s.FontBold, TextAlignmentOptions.Left);
        _statName.color = Color.white;
        _value = ResearchUi.NewText(stripInner, "Value", string.Empty, ValueFont, _s.FontBold, TextAlignmentOptions.Right);
        _statName.rectTransform.anchorMax = new Vector2(0.43f, 1f);
        _value.rectTransform.anchorMin = new Vector2(0.43f, 0f);
        _statName.fontStyle = _value.fontStyle = FontStyles.Bold | FontStyles.Italic;
        _statName.enableAutoSizing = _value.enableAutoSizing = true;
        _statName.fontSizeMin = _value.fontSizeMin = 24f;
        _statName.fontSizeMax = StatFont;
        _value.fontSizeMax = ValueFont;

        // 0번 버튼형: 왼쪽 [추천 강화] | 오른쪽 [강화]/MAX.
        var row0 = Row(panel, "ButtonRow_Recommend");
        _rows[0] = row0.gameObject;
        _main[0] = BuildMain(Slot(row0, 1), "MainButton0", () => OnMainClicked(0));
        _recommend = ResearchUi.NewButton(Slot(row0, 0), "RecommendButton", _s.RoundedFill, CardColor, _s.RecommendButtonLabel,
            ButtonFont, _s.FontBold, out _recommendImage, out _recommendText);
        _recommendImage.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        ResearchUi.Stretch((RectTransform)_recommend.transform);
        _recommend.onClick.AddListener(() => OnRecommendClicked?.Invoke());
        _recommendCost = BuildPrice(_recommend);
        var row1 = Row(panel, "ButtonRow_Follow");
        _rows[1] = row1.gameObject;
        var center = ResearchUi.Place(ResearchUi.NewRect(row1, "Center"), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-SingleButtonWidth * 0.5f, 0f), new Vector2(SingleButtonWidth, RowHeight));
        _main[1] = BuildMain(center, "MainButton1", () => OnMainClicked(1));
        _toggle = BuildToggle(panel, out _track, out _knob, out _toggleLabel);


        float dotsWidth = ModeCount * DotSize + (ModeCount - 1) * DotGap;
        for (int i = 0; i < ModeCount; i++)
        {
            _dots[i] = ResearchUi.NewImage(panel, "Dot" + i, _s.CircleFill, new Vector2(DotSize, DotSize), Vector2.zero);
            ResearchUi.Place(_dots[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-dotsWidth * 0.5f + DotSize * 0.5f + i * (DotSize + DotGap), DotBottom * 0.5f), new Vector2(DotSize, DotSize));
        }
        ApplyMode();
    }

    /// <summary>표시할 트리의 진행 상황 (구성 전환 시 바꾼다).</summary>
    public void SetProgress(ResearchProgress progress) => _progress = progress;

    public void SetGold(int gold) => _gold = gold;

    /// <summary>노드 정보를 보여준다.</summary>
    public void Show(ResearchNodeData node)
    {
        _node = node;
        _icon.sprite = node != null ? node.Icon : null;
        _iconSlot.gameObject.SetActive(node != null);
        Refresh();
    }

    /// <summary>방식을 바꾼다 (0 = 버튼형, 1 = 토글형, 범위 밖이면 무시).</summary>
    public void SetMode(int mode)
    {
        if (mode < 0 || mode >= ModeCount || mode == Mode) return;
        Mode = mode;
        ApplyMode();
        OnModeChanged?.Invoke(mode);
    }

    /// <summary>토글 모양과 1번 방식 [강화]의 동작을 바꾼다 (켜짐 = 청록 + 동그라미 오른쪽).</summary>
    public void SetFollow(bool on)
    {
        _follow = on;
        _track.color = on ? Teal : CardColor;
        _toggleLabel.color = on ? Color.white : new Color(1f, 1f, 1f, 0.6f);
        float offset = (TrackWidth - TrackHeight) * 0.5f;
        _knob.anchoredPosition = new Vector2(on ? offset : -offset, 0f);
        Refresh();
    }

    /// <summary>레벨이 바뀌었을 때 다시 그린다.</summary>
    public void Refresh()
    {
        var recommended = _progress?.GetRecommended();
        bool canRecommend = recommended != null && _gold >= _progress.GetUpgradeCost(recommended);
        _recommendText.text = _s.RecommendButtonLabel;
        SetPrice(_recommendText, _recommendCost, recommended);
        SetStyle(_recommend, _recommendImage, _recommendText, recommended != null ? Style.Secondary : Style.Disabled);
        _recommend.interactable = canRecommend;

        if (_node == null || _progress == null)
        {
            _title.text = _statName.text = _value.text = string.Empty;
            foreach (var slot in _main) { slot.Button.gameObject.SetActive(false); slot.MaxRoot.SetActive(false); }
            return;
        }

        int level = _progress.GetLevel(_node);
        var state = _progress.GetState(_node);
        _level.text = $"{level}/{_node.MaxLevel}";
        _icon.color = state == ResearchNodeState.Locked || state == ResearchNodeState.Blocked
            ? new Color(1f, 1f, 1f, 0.5f) : Color.white;

        _title.text = string.Format(_s.TitleFormat, _node.Id);
        _statName.text = _s.GetStat(_node.Stat).DisplayName;
        string current = _s.FormatValue(_node.Stat, ResearchProgress.GetValue(_node, level));
        if (state == ResearchNodeState.Maxed)
            _value.text = $"<color=#{Hex(Highlight)}>{current}</color>";
        else
        {
            string next = _s.FormatValue(_node.Stat, ResearchProgress.GetValue(_node, level + 1));
            _value.text = $"<color=#{Hex(Color.white)}>{current}</color><color=#{Hex(Highlight)}>{Arrow}</color><color=#{Hex(Highlight)}>{next}</color>";
        }

        ApplySelected(_main[0], state);
        // 1번 토글형: 토글이 켜져 있으면 [강화]는 추천 노드를 올린다 (선택한 노드가 MAX여도 누를 수 있다).
        if (_follow)
        {
            _main[1].MaxRoot.SetActive(false);
            _main[1].Button.gameObject.SetActive(true);
            _main[1].Text.text = _s.UpgradeLabel;
            SetPrice(_main[1].Text, _main[1].Cost, recommended);
            SetStyle(_main[1].Button, _main[1].Image, _main[1].Text, recommended != null ? Style.Primary : Style.Disabled);
            _main[1].Button.interactable = canRecommend;
        }
        else ApplySelected(_main[1], state);
    }

    /// <summary>강화 연출 (큰 아이콘).</summary>
    public void Punch()
    {
        _iconSlot.DOKill();
        _iconSlot.localScale = Vector3.one;
        _iconSlot.DOPunchScale(Vector3.one * 0.1f, 0.3f).SetLink(_iconSlot.gameObject);
    }

    private void ApplyMode()
    {
        for (int i = 0; i < ModeCount; i++)
        {
            _rows[i].SetActive(i == Mode);
            _dots[i].color = i == Mode ? Teal : CardColor;
        }
        _toggle.SetActive(Mode == 1);
    }

    // 선택한 노드 기준: MAX / [강화] / [선택 바꾸기] / [잠김].
    private void ApplySelected(MainSlot slot, ResearchNodeState state)
    {
        bool maxed = state == ResearchNodeState.Maxed;
        slot.MaxRoot.SetActive(maxed);
        slot.Button.gameObject.SetActive(!maxed);
        if (maxed) return;
        bool available = state == ResearchNodeState.Available;
        bool blocked = state == ResearchNodeState.Blocked;
        slot.Text.text = available ? _s.UpgradeLabel : blocked ? _s.ChangeChoiceLabel : _s.LockedLabel;
        SetPrice(slot.Text, slot.Cost, available ? _node : null);
        bool affordable = available && _gold >= _progress.GetUpgradeCost(_node);
        SetStyle(slot.Button, slot.Image, slot.Text, available ? Style.Primary : blocked ? Style.Secondary : Style.Disabled);
        slot.Button.interactable = affordable || blocked;
    }

    private void SetPrice(TextMeshProUGUI label, TextMeshProUGUI price, ResearchNodeData node)
    {
        bool visible = node != null && _progress != null;
        price.transform.parent.gameObject.SetActive(visible);
        ResearchUi.Stretch(label.rectTransform);
        label.rectTransform.anchorMin = new Vector2(0f, visible ? 0.44f : 0f);
        label.fontSize = visible ? 36f : ButtonFont;
        if (!visible) return;
        int cost = _progress.GetUpgradeCost(node);
        price.text = cost.ToString("N0");
        price.color = _gold >= cost ? Highlight : new Color32(255, 65, 65, 255);
    }

    private TextMeshProUGUI BuildPrice(Button button)
    {
        // 잔액 부족으로 버튼이 비활성화되어도 배경과 아이콘 색은 유지한다.
        button.transition = Selectable.Transition.None;
        var row = ResearchUi.Place(ResearchUi.NewRect(button.transform, "Price"),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(250f, 44f));
        var frame = ResearchUi.NewImage(row, "Frame", _s.RoundedFill, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(frame.rectTransform);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        frame.color = new Color32(0x39, 0xBB, 0xC5, 0xFF);
        var background = ResearchUi.NewImage(row, "Background", _s.RoundedFill, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(background.rectTransform, 2f, 2f, 2f, 2f);
        background.type = Image.Type.Sliced;
        background.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        background.color = new Color32(0x00, 0x50, 0x60, 0xFF);
        var icon = ResearchUi.NewImage(row, "CurrencyIcon", _s.GoldIcon, Vector2.zero, Vector2.zero);
        ResearchUi.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(38f, 38f));
        icon.preserveAspect = true;
        var price = ResearchUi.NewText(row, "Amount", string.Empty, 30f, _s.FontBold, TextAlignmentOptions.Center);
        ResearchUi.Stretch(price.rectTransform, 58f, 0f, 12f, 0f);
        return price;
    }

    private void OnMainClicked(int mode)
    {
        if (mode == 1 && _follow) { OnRecommendClicked?.Invoke(); return; }
        if (_node == null || _progress == null) return;
        if (_progress.GetState(_node) == ResearchNodeState.Blocked) OnChangeChoiceClicked?.Invoke(_node);
        else OnUpgradeClicked?.Invoke(_node);
    }

    private static RectTransform Row(RectTransform panel, string name)
    {
        var row = ResearchUi.NewRect(panel, name);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.offsetMin = new Vector2(Inner, RowBottom);
        row.offsetMax = new Vector2(-Inner, RowBottom + RowHeight);
        return row;
    }

    // 버튼 줄을 왼쪽/오른쪽 두 칸으로 나눈다 (0 = 왼쪽, 1 = 오른쪽).
    private static RectTransform Slot(RectTransform row, int index)
    {
        var slot = ResearchUi.NewRect(row, index == 0 ? "Left" : "Right");
        slot.anchorMin = new Vector2(index * 0.5f, 0f);
        slot.anchorMax = new Vector2(index * 0.5f + 0.5f, 1f);
        slot.offsetMin = new Vector2(index == 0 ? 0f : SlotGap * 0.5f, 0f);
        slot.offsetMax = new Vector2(index == 0 ? -SlotGap * 0.5f : 0f, 0f);
        return slot;
    }

    private MainSlot BuildMain(RectTransform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        var slot = new MainSlot();
        slot.Button = ResearchUi.NewButton(parent, name, _s.RoundedFill, Teal, _s.UpgradeLabel, ButtonFont, _s.FontBold,
            out slot.Image, out slot.Text);
        slot.Image.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        ResearchUi.Stretch((RectTransform)slot.Button.transform);
        slot.Button.onClick.AddListener(onClick);
        slot.Cost = BuildPrice(slot.Button);
        var maxRoot = ResearchUi.Stretch(ResearchUi.NewRect(parent, "MaxLabel"));
        slot.MaxRoot = maxRoot.gameObject;
        slot.Max = ResearchUi.NewText(maxRoot, "Max", _s.MaxLabel, MaxFont, _s.FontBold, TextAlignmentOptions.Center);
        slot.Max.color = Teal;
        slot.Max.fontStyle = FontStyles.Italic;
        return slot;
    }

    // 버튼 줄 왼쪽: "추천 따라가기" 글자 + 토글 (전체가 한 버튼).
    private GameObject BuildToggle(RectTransform panel, out Image track, out RectTransform knob, out TextMeshProUGUI label)
    {
        var root = ResearchUi.Place(ResearchUi.NewRect(panel, "FollowToggle"), Vector2.zero, new Vector2(0f, 0.5f),
            new Vector2(Inner + 16f, RowBottom + RowHeight * 0.5f),
            new Vector2(ToggleLabelWidth + ToggleGap + TrackWidth, TrackHeight));
        var hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        button.onClick.AddListener(() => OnFollowToggled?.Invoke(!_follow));

        var labelRect = ResearchUi.NewRect(root, "Label");
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = new Vector2(-(TrackWidth + ToggleGap), 0f);
        label = ResearchUi.NewText(labelRect, "Text", _s.FollowLabel, ToggleFont, _s.FontBold, TextAlignmentOptions.Right);

        track = ResearchUi.NewImage(root, "Track", _s.RoundedFill, Vector2.zero, Vector2.zero);
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = _s.RoundedCornerScale * 2f;
        ResearchUi.Place(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(TrackWidth, TrackHeight));
        float size = TrackHeight - KnobPadding * 2f;
        var knobImage = ResearchUi.NewImage(track.transform, "Knob", _s.CircleFill, new Vector2(size, size), Vector2.zero);
        knobImage.color = _s.OnColor;
        knob = knobImage.rectTransform;
        return root.gameObject;
    }

    // 주 버튼 = 청록 바탕 흰 글자, 보조 = 회색 바탕 청록 글자, 비활성 = 회색 바탕 흐린 글자.
    private void SetStyle(Button button, Image image, TextMeshProUGUI text, Style style)
    {
        button.interactable = style != Style.Disabled;
        image.color = style == Style.Primary ? Teal : style == Style.Secondary ? CardColor : new Color32(0x58, 0x8B, 0x90, 0xFF);
        text.color = style == Style.Disabled ? new Color32(0xB2, 0xCE, 0xD0, 0xFF) : Color.white;
    }

    private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
}
