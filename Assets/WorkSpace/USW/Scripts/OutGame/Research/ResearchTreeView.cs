using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 세로 스크롤 강화 트리 하나. 노드 칸 좌표(Row/Column)로 배치하고, 부모→자식을 ㄱ자 연결선으로 잇는다.
/// 트리는 아래(Row 0)에서 위로 자란다. 연결선은 부모가 최대 레벨이면 주황빛 금색, 아니면 연회색.
/// 옆가지(Column 2) 노드는 부모 높이에서 가로로 나간 뒤 세로로 잇는다.
/// 줄기 이름이 있으면 맨 아래에, 관문 노드(RequiredTotalLevel)에는 가는 가로선과 진행 글자를 그린다.
/// 추천 노드 위에는 청록 "추천" 표시가 떠 있다.
/// </summary>
public sealed class ResearchTreeView
{
    private const float LaneNameFont = 34f;
    private const float GateLabelFont = 30f;
    private const float GateLabelGap = 26f;
    private const float BadgeWidth = 110f;
    private const float BadgeHeight = 50f;
    private const float BadgeFont = 30f;
    private const float BadgeGap = 4f; // 노드 윗변에 걸치도록 (배지 가운데가 윗변 바로 위)
    private const float BadgeBob = 10f;
    private const float BadgeBobSeconds = 0.5f;

    private readonly struct Segment
    {
        public readonly Vector2 A, B;
        public readonly ResearchNodeData Parent;
        public Segment(Vector2 a, Vector2 b, ResearchNodeData parent) { A = a; B = b; Parent = parent; }
    }

    private readonly ResearchTreeData _tree;
    private readonly ResearchViewSettings _s;
    private readonly ResearchProgress _progress;
    private readonly ScrollRect _scroll;
    private readonly RectTransform _content;
    private readonly RectTransform _lineLayer;
    private readonly RectTransform _badge;
    private readonly Dictionary<string, ResearchNodeView> _views = new Dictionary<string, ResearchNodeView>();
    private readonly List<(Image Image, ResearchNodeData Parent)> _lines = new List<(Image, ResearchNodeData)>();
    private readonly List<(TextMeshProUGUI Label, ResearchNodeData Gate, int Stage)> _gates = new List<(TextMeshProUGUI, ResearchNodeData, int)>();
    private ResearchNodeView _selected;

    /// <summary>노드를 눌렀을 때.</summary>
    public event Action<ResearchNodeData> OnNodeClicked;

    /// <summary>스크롤 영역 전체 (구성·탭 전환 시 켜고 끈다).</summary>
    public GameObject Root => _scroll.gameObject;

    public ResearchTreeView(RectTransform parent, ResearchTreeData tree, ResearchViewSettings settings, ResearchProgress progress)
    {
        _tree = tree;
        _s = settings;
        _progress = progress;

        var scrollRect = ResearchUi.Stretch(ResearchUi.NewRect(parent, "Tree_" + tree.name));
        _scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Elastic;
        _scroll.scrollSensitivity = 30f;

        var viewport = ResearchUi.Stretch(ResearchUi.NewRect(scrollRect, "Viewport"));
        viewport.gameObject.AddComponent<RectMask2D>();
        // 빈 곳을 끌어도 스크롤되도록 투명 그래픽을 깐다.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        _scroll.viewport = viewport;

        _content = ResearchUi.NewRect(viewport, "Content");
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _scroll.content = _content;

        int maxRow = 0;
        foreach (var node in _tree.Nodes) if (node != null) maxRow = Mathf.Max(maxRow, node.Row);
        _content.sizeDelta = new Vector2(0f, maxRow * _s.RowSpacing + _s.ContentPadding * 2f);

        var backLayer = ResearchUi.Stretch(ResearchUi.NewRect(_content, "Back"));
        _lineLayer = ResearchUi.Stretch(ResearchUi.NewRect(_content, "Lines"));
        var nodeLayer = ResearchUi.Stretch(ResearchUi.NewRect(_content, "Nodes"));
        var overLayer = ResearchUi.Stretch(ResearchUi.NewRect(_content, "Over"));

        if (_tree.ShowLanes) BuildLaneNames(backLayer);
        BuildGates(backLayer, overLayer);

        foreach (var node in _tree.Nodes)
        {
            if (node == null) continue;
            var view = ResearchNodeView.Create(nodeLayer, node, _s, _s.SizeOf(node), v => OnNodeClicked?.Invoke(v.Node));
            var rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = PositionOf(node);
            _views[node.Id] = view;
        }
        BuildLines();
        _badge = BuildBadge(overLayer);
        Refresh();
    }

    /// <summary>모든 노드 상태, 연결선 색, 관문 글자, 추천 표시를 다시 그린다.</summary>
    public void Refresh()
    {
        foreach (var view in _views.Values)
            view.Refresh(_progress.GetLevel(view.Node), _progress.GetState(view.Node));

        // 끝낸 길(금색)이 아직인 길(연회색) 위에 오도록 순서를 맞춘다 (겹치는 공용 구간).
        foreach (var (image, parent) in _lines)
        {
            bool lit = _progress.GetState(parent) == ResearchNodeState.Maxed;
            image.color = lit ? _s.Accent : _s.Faint;
            if (lit) image.transform.SetAsLastSibling();
        }

        int total = _progress.TotalLevel;
        foreach (var (label, gate, stage) in _gates)
        {
            bool open = total >= gate.RequiredTotalLevel;
            label.text = open ? string.Format(_s.GateOpenFormat, stage)
                : string.Format(_s.GateLabelFormat, stage, total, gate.RequiredTotalLevel);
            label.color = open ? _s.Ink : _s.Muted;
        }

        var recommended = _progress.GetRecommended();
        _badge.gameObject.SetActive(recommended != null);
        if (recommended != null)
            _badge.anchoredPosition = PositionOf(recommended) + new Vector2(0f, VisualHalf(recommended) + BadgeGap);
    }

    /// <summary>선택 표시를 옮긴다.</summary>
    public void Select(ResearchNodeData node)
    {
        _selected?.SetSelected(false);
        _selected = node != null && _views.TryGetValue(node.Id, out var view) ? view : null;
        _selected?.SetSelected(true);
    }

    /// <summary>노드 강화 연출.</summary>
    public void Punch(ResearchNodeData node)
    {
        if (node != null && _views.TryGetValue(node.Id, out var view)) view.Punch();
    }

    /// <summary>노드가 화면 세로 가운데 오도록 스크롤한다.</summary>
    public void ScrollTo(ResearchNodeData node, bool animate = false)
    {
        Canvas.ForceUpdateCanvases();
        float viewportHeight = _scroll.viewport.rect.height;
        float range = _content.rect.height - viewportHeight;
        if (range <= 0f || node == null) return;
        // 콘텐츠 아래에서 노드까지 거리 → 스크롤 위치 (0 = 맨 아래).
        float target = Mathf.Clamp01((PositionOf(node).y - viewportHeight * 0.5f) / range);
        _scroll.StopMovement();
        DOTween.Kill(_scroll);
        if (animate) _scroll.DOVerticalNormalizedPos(target, 0.35f).SetEase(Ease.OutCubic).SetLink(_scroll.gameObject).SetTarget(_scroll);
        else _scroll.verticalNormalizedPosition = target;
    }

    // 화면에서 보이는 반 높이 (마름모는 대각선).
    private float VisualHalf(ResearchNodeData node) => _s.SizeOf(node) * (node.Shape == ResearchNodeShape.Special ? 0.707f : 0.5f);

    private float ColumnOffset => _tree.ColumnSpacing > 0f ? _tree.ColumnSpacing : _s.ColumnOffset;

    private Vector2 PositionOf(ResearchNodeData node)
    {
        float x = node.Column >= 2 ? _s.SpecialColumnX : _s.TreeCenterX + node.Column * ColumnOffset;
        return new Vector2(x, _s.ContentPadding + node.Row * _s.RowSpacing);
    }

    private void BuildLaneNames(RectTransform layer)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            float x = _s.TreeCenterX + (lane - 1) * ColumnOffset;
            string name = _tree.LaneNames != null && lane < _tree.LaneNames.Length ? _tree.LaneNames[lane] : string.Empty;
            var labelRect = ResearchUi.Place(ResearchUi.NewRect(layer, "LaneName" + lane), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(x, _s.ContentPadding * 0.4f), new Vector2(ColumnOffset, LaneNameFont * 1.5f));
            var label = ResearchUi.NewText(labelRect, "Text", name, LaneNameFont, _s.FontBold, TextAlignmentOptions.Center);
            label.color = _s.Muted;
        }
    }

    private void BuildGates(RectTransform back, RectTransform over)
    {
        int stage = 1;
        foreach (var node in _tree.Nodes)
        {
            if (node == null || node.RequiredTotalLevel <= 0) continue;
            stage++;
            var pos = PositionOf(node);
            var band = ResearchUi.NewImage(back, "GateBand", null, Vector2.zero, Vector2.zero);
            band.color = _s.Faint;
            var rect = band.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(0f, pos.y);
            rect.sizeDelta = new Vector2(0f, _s.LineWidth * 0.5f);

            var labelRect = ResearchUi.Place(ResearchUi.NewRect(over, "GateLabel"), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(pos.x, pos.y - VisualHalf(node) - GateLabelGap * 0.5f), new Vector2(ColumnOffset * 3f, GateLabelFont * 1.4f));
            var label = ResearchUi.NewText(labelRect, "Text", string.Empty, GateLabelFont, _s.FontRegular, TextAlignmentOptions.Center);
            _gates.Add((label, node, stage));
        }
    }

    private RectTransform BuildBadge(RectTransform layer)
    {
        var root = ResearchUi.NewRect(layer, "RecommendBadge");
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(BadgeWidth, BadgeHeight);
        var pill = ResearchUi.NewImage(root, "Pill", _s.RoundedFill, new Vector2(BadgeWidth, BadgeHeight), Vector2.zero);
        pill.type = Image.Type.Sliced;
        pill.pixelsPerUnitMultiplier = _s.RoundedCornerScale * 1.5f;
        pill.color = _s.Active;
        var text = ResearchUi.NewText(pill.transform, "Text", _s.RecommendLabel, BadgeFont, _s.FontBold, TextAlignmentOptions.Center);
        text.color = _s.OnColor;
        pill.rectTransform.DOAnchorPosY(BadgeBob, BadgeBobSeconds).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(pill.gameObject);
        return root;
    }

    // 부모 → 자식 연결선. 일반: 부모 위 → 중간 높이 → 자식 아래 (ㄱ자). 옆가지: 부모 높이에서 가로 → 세로.
    private void BuildLines()
    {
        var segments = new List<Segment>();
        foreach (var child in _tree.Nodes)
        {
            if (child == null) continue;
            Vector2 c = PositionOf(child);
            foreach (string parentId in child.Parents)
            {
                var parent = _tree.Find(parentId);
                if (parent == null) continue;
                Vector2 p = PositionOf(parent);
                if (child.Column >= 2)
                {
                    segments.Add(new Segment(p, new Vector2(c.x, p.y), parent));
                    segments.Add(new Segment(new Vector2(c.x, p.y), c, parent));
                    continue;
                }
                float midY = (p.y + c.y) * 0.5f;
                segments.Add(new Segment(p, new Vector2(p.x, midY), parent));
                segments.Add(new Segment(new Vector2(p.x, midY), new Vector2(c.x, midY), parent));
                segments.Add(new Segment(new Vector2(c.x, midY), c, parent));
            }
        }

        foreach (var seg in segments)
        {
            if ((seg.A - seg.B).sqrMagnitude < 0.01f) continue;
            var image = ResearchUi.NewImage(_lineLayer, "Line", null, Vector2.zero, Vector2.zero);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = (seg.A + seg.B) * 0.5f;
            rect.sizeDelta = new Vector2(Mathf.Abs(seg.A.x - seg.B.x) + _s.LineWidth, Mathf.Abs(seg.A.y - seg.B.y) + _s.LineWidth);
            _lines.Add((image, seg.Parent));
        }
    }
}
