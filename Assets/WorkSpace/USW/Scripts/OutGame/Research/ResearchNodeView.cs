using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 강화 노드 하나의 화면. UI3600 아트로 잠금·강화 가능·추천·MAX 상태를 표시한다.
/// 아트 미연결 시 기존 네모·원·마름모 도형을 사용한다.
/// 하단 패널의 큰 아이콘도 같은 뷰를 터치 없이 쓴다.
/// </summary>
public sealed class ResearchNodeView : MonoBehaviour
{
    private const float PunchScale = 0.14f;
    private const float PunchSeconds = 0.3f;
    private const float IconRatio = 0.46f;
    private const float PipRatio = 0.06f;
    private const float PipGapRatio = 0.035f;
    private const float PipBottomRatio = 0.14f;
    private const int MaxPips = 10;
    private const float LevelFontRatio = 0.14f;
    private const float BadgeRatio = 0.3f;
    private const float BadgeIconRatio = 0.6f;
    private const float ShadowGrow = 1.3f;
    private const float ShadowDropRatio = 0.05f;
    private const float DiamondLevelGap = 0.95f;

    private ResearchViewSettings _s;
    private Image _selection;
    private Image _body;
    private Image _ring;
    private Image _ringBack;
    private Image _icon;
    private Image _badge;
    private Image _badgeIcon;
    private Image _blocked;
    private TextMeshProUGUI _levelText;
    private readonly List<Image> _pips = new List<Image>();
    private bool _pulse;
    private Color _pulseColor;
    private Image _maxArt;
    private Image _dotsBack;
    private Image _dotsFill;
    private bool _art;
    private Sprite _frameOverride;

    /// <summary>이 뷰가 보여주는 노드.</summary>
    public ResearchNodeData Node { get; private set; }

    /// <summary>노드 뷰를 만든다. onClick이 null이면 터치를 받지 않는다.</summary>
    public static ResearchNodeView Create(RectTransform parent, ResearchNodeData node, ResearchViewSettings settings, float size, Action<ResearchNodeView> onClick)
    {
        var rect = ResearchUi.NewRect(parent, "Node_" + node.Id);
        rect.sizeDelta = new Vector2(size, size);
        var view = rect.gameObject.AddComponent<ResearchNodeView>();
        view.Build(node, settings, size, onClick);
        return view;
    }

    private void Build(ResearchNodeData node, ResearchViewSettings s, float size, Action<ResearchNodeView> onClick)
    {
        Node = node;
        _s = s;
        var rect = (RectTransform)transform;
        if (s.HasNodeArt)
        {
            BuildArt(rect, size, onClick);
            return;
        }
        bool circle = node.Shape == ResearchNodeShape.Circle;
        bool diamond = node.Shape == ResearchNodeShape.Special;

        // 바탕 묶음 (마름모는 45도 돌린다). 아이콘·레벨·배지는 돌리지 않는다.
        var frame = ResearchUi.NewRect(rect, "Frame");
        frame.sizeDelta = new Vector2(size, size);
        if (diamond) frame.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Sprite fill = circle ? s.CircleFill : s.RoundedFill;
        Sprite outline = circle ? s.CircleOutline : s.RoundedOutline;

        var shadow = ResearchUi.NewImage(rect, "Shadow", s.Shadow, Vector2.one * size * (diamond ? 1.414f : 1f) * ShadowGrow,
            new Vector2(0f, -size * ShadowDropRatio));
        shadow.color = s.ShadowColor;
        shadow.transform.SetAsFirstSibling();

        _selection = Shape(frame, "Selection", outline, size + s.SelectionPadding * 2f, !circle);
        _selection.color = s.Active;
        _selection.gameObject.SetActive(false);
        _body = Shape(frame, "Body", fill, size, !circle);

        if (circle)
        {
            _ringBack = Shape(frame, "RingBack", outline, size, false);
            _ring = Shape(frame, "Ring", outline, size, false);
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = true;
        }
        else _ring = Shape(frame, "Ring", outline, size, true);

        float iconSize = size * IconRatio * (diamond ? 1.15f : 1f);
        _icon = ResearchUi.NewImage(rect, "Icon", node.Icon, new Vector2(iconSize, iconSize),
            new Vector2(0f, circle || diamond ? 0f : size * 0.05f));
        _icon.preserveAspect = true;

        if (circle || diamond || node.MaxLevel > MaxPips)
        {
            float y = diamond ? -size * DiamondLevelGap : circle ? -size * 0.3f : -size * 0.36f;
            var levelRect = ResearchUi.Place(ResearchUi.NewRect(rect, "Level"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, y), new Vector2(size, size * 0.2f));
            _levelText = ResearchUi.NewText(levelRect, "Text", "", size * LevelFontRatio, s.FontBold, TextAlignmentOptions.Center);
            if (circle) _icon.rectTransform.anchoredPosition = new Vector2(0f, size * 0.06f);
        }
        else BuildPips(rect, size);

        float badge = size * BadgeRatio;
        var badgePos = diamond ? new Vector2(size * 0.55f, size * 0.55f) : new Vector2(size * 0.42f, size * 0.42f);
        _badge = ResearchUi.NewImage(rect, "Badge", s.CircleFill, new Vector2(badge, badge), badgePos);
        _badge.color = s.Active;
        _badgeIcon = ResearchUi.NewImage(_badge.transform, "Arrow", s.UpgradeArrow, new Vector2(badge, badge) * BadgeIconRatio, Vector2.zero);
        _badgeIcon.preserveAspect = true;
        _badgeIcon.color = s.OnColor;
        _blocked = ResearchUi.NewImage(rect, "Blocked", s.BlockedIcon, new Vector2(badge, badge) * 0.8f, badgePos);
        _blocked.preserveAspect = true;
        _blocked.color = s.Muted;

        if (onClick == null) return;
        _body.raycastTarget = true;
        var button = gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = _body;
        button.onClick.AddListener(() => onClick(this));
    }

    private void BuildPips(RectTransform rect, float size)
    {
        float pip = size * PipRatio, gap = size * PipGapRatio;
        float width = Node.MaxLevel * pip + (Node.MaxLevel - 1) * gap;
        for (int i = 0; i < Node.MaxLevel; i++)
        {
            var image = ResearchUi.NewImage(rect, "Pip" + i, _s.CircleFill, new Vector2(pip, pip),
                new Vector2(-width * 0.5f + pip * 0.5f + i * (pip + gap), -size * 0.5f + size * (_art ? 0.21f : PipBottomRatio)));
            _pips.Add(image);
        }
    }

    private void BuildArt(RectTransform rect, float size, Action<ResearchNodeView> onClick)
    {
        _art = true;
        _body = ResearchUi.NewImage(rect, "NodeBody", _s.NodeLockedSprite, Vector2.one * size, Vector2.zero);
        _body.preserveAspect = true;
        _selection = Shape(rect, "Selection", _s.RoundedOutline, size + 4f, true);
        _selection.color = _s.Accent;
        _selection.gameObject.SetActive(false);
        _icon = ResearchUi.NewImage(rect, "NodeIconSlot", Node.Icon, Vector2.one * size * 0.5f, new Vector2(0f, size * 0.06f));
        _icon.preserveAspect = true;
        _maxArt = ResearchUi.NewImage(rect, "Node_Max", _s.NodeMaxSprite, new Vector2(size * 0.4f, size * 0.17f), new Vector2(0f, -size * 0.29f));
        _maxArt.preserveAspect = true;
        if (_s.NodeMaxSprite == null)
        {
            _levelText = ResearchUi.NewText(_maxArt.transform, "MaxText", _s.MaxLabel, size * LevelFontRatio, _s.FontBold, TextAlignmentOptions.Center);
            _maxArt.enabled = false;
        }
        _maxArt.gameObject.SetActive(false);
        if (Node.MaxLevel == 5 && _s.LevelDotsSprite != null)
        {
            var dotsSize = new Vector2(size * 0.42f, size * 0.068f);
            var dotsPosition = new Vector2(0f, -size * 0.29f);
            _dotsBack = ResearchUi.NewImage(rect, "LevelDots", _s.LevelDotsSprite, dotsSize, dotsPosition);
            _dotsFill = ResearchUi.NewImage(rect, "LevelDotsFill", _s.LevelDotsSprite, dotsSize, dotsPosition);
            _dotsFill.type = Image.Type.Filled;
            _dotsFill.fillMethod = Image.FillMethod.Horizontal;
            _dotsFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        else if (Node.MaxLevel <= MaxPips) BuildPips(rect, size);
        else
        {
            var levelRect = ResearchUi.NewRect(rect, "Level");
            ResearchUi.Place(levelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -size * 0.29f), new Vector2(size, size * 0.2f));
            _levelText = ResearchUi.NewText(levelRect, "Text", string.Empty, size * LevelFontRatio, _s.FontBold, TextAlignmentOptions.Center);
        }
        _blocked = ResearchUi.NewImage(rect, "Blocked", _s.BlockedIcon, Vector2.one * size * 0.2f, new Vector2(size * 0.36f, size * 0.36f));
        _blocked.preserveAspect = true;
        if (onClick == null) return;
        _body.raycastTarget = true;
        var button = gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = _body;
        button.onClick.AddListener(() => onClick(this));
    }

    /// <summary>레벨·상태를 다시 그린다.</summary>
    public void Refresh(int level, ResearchNodeState state, bool recommended = false)
    {
        if (_art)
        {
            RefreshArt(level, state, recommended);
            return;
        }
        bool maxed = state == ResearchNodeState.Maxed;
        bool available = state == ResearchNodeState.Available;
        bool blocked = state == ResearchNodeState.Blocked;

        _body.color = maxed ? _s.Accent : blocked ? _s.Soft : _s.Surface;
        Color ring = maxed ? _s.Accent : available ? _s.Active : _s.Faint;
        _icon.color = maxed ? _s.OnColor : available ? _s.Ink : _s.Faint;

        if (_ringBack != null)
        {
            // 원형: 바탕 테두리는 연회색, 진행 바는 청록으로 레벨만큼 채운다 (최대면 금색).
            _ringBack.color = maxed ? _s.Accent : _s.Faint;
            _ring.color = maxed ? _s.Accent : _s.Active;
            _ring.fillAmount = Node.MaxLevel > 0 ? (float)level / Node.MaxLevel : 0f;
        }
        else _ring.color = ring;

        if (_levelText != null)
        {
            _levelText.text = maxed ? _s.MaxLabel : $"{level}/{Node.MaxLevel}";
            bool inside = Node.Shape == ResearchNodeShape.Circle;
            _levelText.color = inside && maxed ? _s.OnColor : maxed ? _s.Accent : available ? _s.Ink : _s.Muted;
        }
        for (int i = 0; i < _pips.Count; i++)
            _pips[i].color = maxed ? _s.OnColor : i < level ? _s.Active : _s.Faint;

        _badge.gameObject.SetActive(available);
        _blocked.gameObject.SetActive(blocked);

        // 깜빡임: 네모·마름모는 테두리, 원형은 바탕 테두리.
        _pulse = available;
        _pulseColor = _ringBack != null ? _s.Active : ring;
        if (!_pulse && _ringBack == null) _ring.color = ring;
    }

    private void RefreshArt(int level, ResearchNodeState state, bool recommended)
    {
        bool maxed = state == ResearchNodeState.Maxed;
        bool available = state == ResearchNodeState.Available;
        _body.sprite = _frameOverride != null ? _frameOverride : maxed ? _s.NodeIconSlotSprite : available ? (recommended ? _s.NodeRecommendSprite : _s.NodeAvailableSprite) : _s.NodeLockedSprite;
        _body.color = Color.white;
        _icon.color = available || maxed ? _s.OnColor : _s.Muted;
        _blocked.gameObject.SetActive(state == ResearchNodeState.Blocked);
        _maxArt.gameObject.SetActive(maxed);
        if (_dotsBack != null)
        {
            _dotsBack.gameObject.SetActive(!maxed);
            _dotsFill.gameObject.SetActive(!maxed);
            _dotsBack.color = _s.Faint;
            _dotsFill.color = _s.OnColor;
            _dotsFill.fillAmount = (float)level / Node.MaxLevel;
        }
        for (int i = 0; i < _pips.Count; i++)
        {
            _pips[i].gameObject.SetActive(!maxed);
            _pips[i].color = i < level ? _s.OnColor : _s.Faint;
        }
        if (_levelText != null)
        {
            _levelText.text = maxed ? _s.MaxLabel : $"{level}/{Node.MaxLevel}";
            _levelText.color = _s.OnColor;
            if (_levelText.transform.parent != _maxArt.transform) _levelText.gameObject.SetActive(!maxed);
        }
        _pulse = false;
    }

    private void Update()
    {
        if (!_pulse || _s.PulseSeconds <= 0f) return;
        float t = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _s.PulseSeconds) + 1f) * 0.5f;
        var c = _pulseColor;
        c.a *= Mathf.Lerp(_s.PulseMinAlpha, 1f, t);
        if (_ringBack != null) _ringBack.color = Color.Lerp(_s.Faint, _s.Active, c.a * 0.5f);
        else _ring.color = c;
    }

    /// <summary>선택 테두리를 켜고 끈다.</summary>
    public void SetSelected(bool selected) => _selection.gameObject.SetActive(selected);

    /// <summary>하단 카드에서 사용하는 별도 아이콘 프레임을 지정한다.</summary>
    public void SetFrameSprite(Sprite frame) => _frameOverride = frame;

    /// <summary>강화했을 때 톡 튀는 연출.</summary>
    public void Punch()
    {
        transform.DOKill(true);
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * PunchScale, PunchSeconds, 6, 0.6f).SetLink(gameObject);
    }

    private void OnDestroy() => transform.DOKill();

    private Image Shape(RectTransform parent, string name, Sprite sprite, float size, bool sliced)
    {
        var image = ResearchUi.NewImage(parent, name, sprite, new Vector2(size, size), Vector2.zero);
        if (sliced)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = _s.RoundedCornerScale;
        }
        return image;
    }
}
