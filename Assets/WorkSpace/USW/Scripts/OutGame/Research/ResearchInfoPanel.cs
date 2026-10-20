using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>KHJ 강화 시안의 고정 정보판. 추천 강화와 선택한 노드 강화를 각각 제공한다.</summary>
public sealed class ResearchInfoPanel
{
    private const float IconSize = 216f;
    private const float ButtonHeight = 86f;
    private const float ButtonBottom = 8f;
    private const float ButtonMargin = 34f;
    private const float ButtonGap = 76f;
    private const float TitleFont = 56f;
    private const float StatFont = 44f;
    private const float ButtonFont = 46f;
    private const float DotSize = 12f;
    private const float DotGap = 5f;
    private const float MaxButtonWidth = 420f;
    private readonly ResearchViewSettings _s;
    private readonly RectTransform _iconSlot;
    private readonly Image _icon;
    private readonly RectTransform _dotRow;
    private readonly List<Image> _dots = new();
    private readonly TextMeshProUGUI _title;
    private readonly TextMeshProUGUI _statName;
    private readonly TextMeshProUGUI _value;
    private readonly TextMeshProUGUI _currentValue;
    private readonly RectTransform _valueArrow;
    private readonly Button _recommend;
    private readonly Button _main;
    private readonly TextMeshProUGUI _recommendText;
    private readonly TextMeshProUGUI _mainText;
    private ResearchProgress _progress;
    private ResearchNodeData _node;
    private RectTransform _pressedButton;

    /// <summary>선택한 노드를 한 단계 강화하는 요청.</summary>
    public event Action<ResearchNodeData> OnUpgradeClicked;
    /// <summary>추천 노드를 선택하고 한 단계 강화하는 요청.</summary>
    public event Action OnRecommendClicked;
    /// <summary>택1에서 다른 선택을 되돌리는 요청.</summary>
    public event Action<ResearchNodeData> OnChangeChoiceClicked;

    /// <summary>공용 SO에 연결된 KHJ 카드·프레임·버튼 아트를 배치한다.</summary>
    public ResearchInfoPanel(RectTransform panel, ResearchViewSettings settings)
    {
        _s = settings;
        if (panel.TryGetComponent<Image>(out var background))
        {
            background.sprite = settings.CardBackground;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = true;
        }
        _iconSlot = ResearchUi.Place(ResearchUi.NewRect(panel, "IconSlot"), new Vector2(0f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(172f, -202f), Vector2.one * IconSize);
        var frame = ResearchUi.NewImage(_iconSlot, "Frame", settings.IconFrameSprite, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(frame.rectTransform);
        frame.preserveAspect = true;
        _icon = ResearchUi.NewImage(_iconSlot, "Icon", null, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(_icon.rectTransform, 38f, 48f, 38f, 32f);
        _icon.preserveAspect = true;
        _dotRow = ResearchUi.Place(ResearchUi.NewRect(_iconSlot, "LevelDots"), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 33f), new Vector2(IconSize - 40f, DotSize));
        _title = Text(panel, "Title", TitleFont, TextAlignmentOptions.Left, 356f, 54f, 94f, 181f);
        _statName = Text(panel, "StatName", 36f, TextAlignmentOptions.Left, 374f, 68f, 202f, 246f);
        _currentValue = Text(panel, "CurrentValue", StatFont, TextAlignmentOptions.Right, 374f, 406f, 254f, 312f);
        _currentValue.color = new Color32(218, 228, 235, 255);
        _valueArrow = ResearchUi.NewRect(panel, "ValueArrowSlot");
        _valueArrow.anchorMin = new Vector2(0f, 1f);
        _valueArrow.anchorMax = Vector2.one;
        _valueArrow.offsetMin = new Vector2(690f, -300f);
        _valueArrow.offsetMax = new Vector2(-340f, -266f);
        var valueArrow = _valueArrow.gameObject.AddComponent<ResearchValueArrow>();
        valueArrow.color = new Color32(235, 216, 132, 255);
        valueArrow.raycastTarget = false;
        _value = Text(panel, "Value", StatFont, TextAlignmentOptions.Right, 752f, 68f, 254f, 312f);
        _value.color = settings.InfoValueColor;
        var row = ResearchUi.NewRect(panel, "ActionRow");
        row.anchorMin = Vector2.zero; row.anchorMax = new Vector2(1f, 0f); row.pivot = new Vector2(0.5f, 0f);
        row.offsetMin = new Vector2(ButtonMargin, ButtonBottom);
        row.offsetMax = new Vector2(-ButtonMargin, ButtonBottom + ButtonHeight);
        _recommend = BuildButton(row, "RecommendButton", 0, settings.MaxLabelSprite, settings.RecommendButtonLabel, out _recommendText);
        _main = BuildButton(row, "MainButton0", 1, settings.UpgradeButtonSprite, settings.UpgradeLabel, out _mainText);
        var arrow = ResearchUi.NewRect(_recommend.transform, "UpgradeArrow");
        ResearchUi.Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-134f, 0f), new Vector2(52f, 56f));
        arrow.gameObject.AddComponent<ResearchRecommendationArrow>().raycastTarget = false;
        ResearchUi.Stretch(_recommendText.rectTransform, 72f, 0f, 12f, 0f);
        _recommend.onClick.AddListener(() =>
        {
            _pressedButton = (RectTransform)_recommend.transform;
            OnRecommendClicked?.Invoke();
        });
        _main.onClick.AddListener(OnMainClicked);
    }

    /// <summary>표시할 트리의 진행 상황을 연결한다.</summary>
    public void SetProgress(ResearchProgress progress) => _progress = progress;

    /// <summary>선택한 노드의 아이콘과 설명을 표시한다.</summary>
    public void Show(ResearchNodeData node)
    {
        _node = node;
        _icon.sprite = node != null ? node.Icon : null;
        _iconSlot.gameObject.SetActive(node != null);
        Refresh();
    }

    /// <summary>최신 SO 수치·진행으로 표시와 버튼 상태를 갱신한다.</summary>
    public void Refresh()
    {
        var recommended = _progress?.GetRecommended();
        _recommend.interactable = recommended != null && _progress.CanUpgrade(recommended);
        _recommendText.text = _s.RecommendButtonLabel;
        bool maxed = _node != null && _progress != null && _progress.GetState(_node) == ResearchNodeState.Maxed;
        SetActionLayout(maxed);
        if (_node == null || _progress == null)
        {
            _title.text = _statName.text = _value.text = _currentValue.text = string.Empty;
            _valueArrow.gameObject.SetActive(false);
            _currentValue.gameObject.SetActive(false);
            _main.interactable = false;
            _mainText.text = _s.LockedLabel;
            return;
        }
        int level = _progress.GetLevel(_node);
        var state = _progress.GetState(_node);
        _title.text = string.Format(_s.TitleFormat, _node.Id);
        _statName.text = _s.GetDisplayName(_progress.Tree, _node);
        int shownLevel = state == ResearchNodeState.Maxed ? level : Mathf.Min(level + 1, _node.MaxLevel);
        _currentValue.text = _s.FormatDisplayValue(_progress.Tree, _node, level);
        _value.text = _s.FormatDisplayValue(_progress.Tree, _node, shownLevel);
        _currentValue.gameObject.SetActive(!maxed);
        _valueArrow.gameObject.SetActive(!maxed);
        _mainText.text = state == ResearchNodeState.Maxed ? _s.MaxLabel
            : state == ResearchNodeState.Blocked ? _s.ChangeChoiceLabel
            : state == ResearchNodeState.Available ? _s.UpgradeLabel : _s.LockedLabel;
        _main.interactable = state == ResearchNodeState.Blocked || _progress.CanUpgrade(_node);
        RefreshDots(level);
    }

    private void SetActionLayout(bool maxed)
    {
        _recommend.gameObject.SetActive(!maxed);
        var rect = (RectTransform)_main.transform;
        if (maxed)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(-MaxButtonWidth * 0.5f, 0f);
            rect.offsetMax = new Vector2(MaxButtonWidth * 0.5f, 0f);
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(ButtonGap * 0.5f, 0f);
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>실제 누른 버튼과 선택 아이콘에 강화 완료 탄력을 적용한다.</summary>
    public void Punch(bool completed = false)
    {
        if (completed)
        {
            Complete(_iconSlot);
            Complete(_pressedButton != null ? _pressedButton : (RectTransform)_main.transform);
        }
        else
        {
            Bounce(_iconSlot);
            if (_pressedButton != null) Bounce(_pressedButton);
        }
        _pressedButton = null;
    }

    private void Complete(RectTransform rect)
    {
        rect.DOKill(); rect.localScale = Vector3.one;
        var impact = rect.GetComponent<ResearchCompletionImpact>();
        if (impact == null) { impact = rect.gameObject.AddComponent<ResearchCompletionImpact>(); impact.Initialize(_s); }
        impact.Play();
    }

    private void OnMainClicked()
    {
        if (_node == null || _progress == null) return;
        _pressedButton = (RectTransform)_main.transform;
        if (_progress.GetState(_node) == ResearchNodeState.Blocked) OnChangeChoiceClicked?.Invoke(_node);
        else OnUpgradeClicked?.Invoke(_node);
    }

    private void RefreshDots(int level)
    {
        while (_dots.Count < _node.MaxLevel)
            _dots.Add(ResearchUi.NewImage(_dotRow, "Dot" + _dots.Count, _s.CircleFill, Vector2.one * DotSize, Vector2.zero));
        float width = _node.MaxLevel * DotSize + Mathf.Max(0, _node.MaxLevel - 1) * DotGap;
        for (int i = 0; i < _dots.Count; i++)
        {
            _dots[i].gameObject.SetActive(i < _node.MaxLevel);
            _dots[i].rectTransform.anchoredPosition = new Vector2(-width * 0.5f + DotSize * 0.5f + i * (DotSize + DotGap), 0f);
            _dots[i].color = i < level ? Color.white : _s.InfoEmptyDotColor;
        }
    }

    private TextMeshProUGUI Text(RectTransform panel, string name, float size, TextAlignmentOptions alignment,
        float left, float right, float top, float bottom)
    {
        var rect = ResearchUi.NewRect(panel, name);
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -bottom); rect.offsetMax = new Vector2(-right, -top);
        var text = ResearchUi.NewText(rect, "Text", string.Empty, size, _s.FontBold, alignment, _s.FontMaterial);
        text.enableAutoSizing = true; text.fontSizeMin = UiTypography.AutoSizeMinimum; text.fontSizeMax = Mathf.Max(UiTypography.BodySize, size);
        return text;
    }

    private Button BuildButton(RectTransform row, string name, int index, Sprite sprite, string label, out TextMeshProUGUI text)
    {
        var button = ResearchUi.NewButton(row, name, sprite, Color.white, label, ButtonFont, _s.FontBold, out var image, out text, _s.FontMaterial);
        image.type = Image.Type.Sliced;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(index * 0.5f, 0f); rect.anchorMax = new Vector2((index + 1) * 0.5f, 1f);
        rect.offsetMin = new Vector2(index == 0 ? 0f : ButtonGap * 0.5f, 0f);
        rect.offsetMax = new Vector2(index == 0 ? -ButtonGap * 0.5f : 0f, 0f);
        text.enableAutoSizing = true; text.fontSizeMin = UiTypography.AutoSizeMinimum; text.fontSizeMax = ButtonFont;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static void Bounce(RectTransform rect)
    {
        rect.DOKill(); rect.localScale = Vector3.one;
        DOTween.Sequence().Append(rect.DOScale(0.92f, 0.09f).SetEase(Ease.InQuad))
            .Append(rect.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
            .SetTarget(rect).SetUpdate(true).SetLink(rect.gameObject);
    }
}
