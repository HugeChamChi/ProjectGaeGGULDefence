using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 용어 팝업 실험실의 화면 요소 — A 말풍선과 B 바텀 시트를 코드로 만든다.
/// 열려 있는 동안 화면 전체 방패가 누름을 받아서, 바깥 탭은 닫기만 하고 카드 선택으로 이어지지 않는다.
/// </summary>
public sealed class TermPopupLabView : MonoBehaviour
{
    private static readonly Color Cream = new Color(1f, 0.965f, 0.9f, 1f);
    // 게임 글꼴은 검은 외곽선이 글꼴 자체에 들어 있어 밝은 바탕에서 뭉개진다 → 말풍선도 어두운 바탕.
    private static readonly Color BubbleColor = new Color(0.16f, 0.17f, 0.24f, 0.98f);
    private static readonly Color SheetColor = new Color(0.118f, 0.129f, 0.188f, 0.98f);
    private static readonly Color SheetText = new Color(0.87f, 0.85f, 0.79f, 1f);
    private static readonly Color Muted = new Color(0.56f, 0.58f, 0.66f, 1f);
    private static readonly Color TermColor = new Color(0.5f, 0.83f, 0.93f, 1f);

    [Header("배치 (캔버스 기준 단위)")]
    [SerializeField, Min(0f)] private float _margin = 40f;
    [Tooltip("단어와 말풍선 사이 간격")]
    [SerializeField, Min(0f)] private float _gap = 26f;
    [Tooltip("OLD 시트 높이 / NEW 시트 최대 높이 (화면 비율)")]
    [SerializeField, Range(0.2f, 0.6f)] private float _sheetHeightRatio = 0.36f;
    [Tooltip("NEW 시트 최소 높이 — 내용이 짧아도 이만큼은 올라온다")]
    [SerializeField, Min(0f)] private float _sheetMinHeight = 420f;

    [Header("연출")]
    [SerializeField, Min(0.01f)] private float _bubbleOpenDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float _sheetOpenDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float _sheetCloseDuration = 0.2f;

    [Header("B 연출 NEW (텐션)")]
    [Tooltip("끄면 처음 버전(고정 높이, 밋밋한 슬라이드)")]
    [SerializeField] private bool _punchySheet = true;
    [SerializeField, Min(0.01f)] private float _punchOpenDuration = 0.34f;
    [Tooltip("시트가 살짝 넘쳤다 돌아오는 정도")]
    [SerializeField, Min(0f)] private float _punchOvershoot = 1.15f;
    [Tooltip("제목 줄이 튀어나오기 시작하는 시점 / 이후 줄마다 간격")]
    [SerializeField, Min(0f)] private float _punchContentDelay = 0.08f;
    [SerializeField, Min(0f)] private float _punchStagger = 0.05f;
    [SerializeField, Min(0f)] private float _punchEdgeHeight = 8f;

    private RectTransform _root;
    private TMP_FontAsset _font;
    private Material _fontMaterial;
    private Sprite _rounded;
    private GameObject _shield;

    private RectTransform _bubble;
    private RectTransform _arrow;
    private Image _bubbleKindBg;
    private TMP_Text _bubbleKind, _bubbleTitle, _bubbleBody, _bubbleFacts;
    private GameObject _bubbleMore;

    private RectTransform _sheet;
    private Image _sheetKindBg;
    private TMP_Text _sheetKind, _sheetTitle, _sheetBody, _sheetFacts, _chipLabel;
    private RectTransform _chipRow;
    private RectTransform _sheetHeader, _sheetBadge;
    private Image _sheetEdge;
    private readonly List<CanvasGroup> _sheetParts = new List<CanvasGroup>();
    private bool _sheetOpen;
    private float _sheetHeight;
    // 넘쳤다 돌아오는 동안 아래 빈틈이 보이지 않게 시트를 화면 아래로 이만큼 더 길게 둔다.
    private const float SheetOverscan = 90f;

    /// <summary>방패가 켜져 있는 동안 true.</summary>
    public bool IsOpen => _shield != null && _shield.activeSelf;
    /// <summary>화면에 보이는 시트 높이 (캔버스 기준 단위). 시트를 열 때 정해진다.</summary>
    public float SheetHeight => _sheetHeight > 0f ? _sheetHeight : _root.rect.height * _sheetHeightRatio;
    /// <summary>B 연출 NEW(텐션) 사용 여부. 실험실 버튼으로 바꾼다.</summary>
    public bool PunchySheet { get => _punchySheet; set => _punchySheet = value; }
    /// <summary>바깥 탭으로 닫혔을 때.</summary>
    public event Action Closed;
    /// <summary>말풍선의 "자세히"를 눌렀을 때.</summary>
    public event Action MoreRequested;
    /// <summary>시트의 "이 카드의 다른 용어" 칩을 눌렀을 때 (용어 ID).</summary>
    public event Action<string> ChipTapped;

    /// <summary>캔버스 맨 위에 팝업 층을 만든다. 글꼴은 카드 설명 글자와 같은 것을 쓴다.</summary>
    public static TermPopupLabView Create(RectTransform canvas, TMP_Text fontSource, Sprite rounded)
    {
        var root = NewRect("TermPopup", canvas);
        Stretch(root);
        root.SetAsLastSibling();
        var view = root.gameObject.AddComponent<TermPopupLabView>();
        view._root = root;
        view._font = fontSource.font;
        view._fontMaterial = fontSource.fontSharedMaterial;
        view._rounded = rounded;
        view.Build();
        view.HideImmediately();
        return view;
    }

    /// <summary>
    /// A 말풍선: 단어가 화면 중앙보다 위면 아래로, 아래면 위로 띄운다. summary면 요약 + "자세히".
    /// 세로 위치는 읽고 있는 카드(avoid) 바깥에 붙여 그 카드를 가리지 않고, 꼬리만 단어 가로 위치를 가리킨다.
    /// </summary>
    public void ShowBubble(TermPopupLabTerm term, Vector3 linkMinWorld, Vector3 linkMaxWorld,
                           Vector3 avoidMinWorld, Vector3 avoidMaxWorld, bool summary)
    {
        KillTweens();
        HideSheetImmediately();
        _shield.SetActive(true);
        Fill(term, _bubbleKindBg, _bubbleKind, _bubbleTitle, _bubbleBody, _bubbleFacts, summary);
        _bubbleMore.SetActive(summary);

        var area = _root.rect;
        float width = area.width - _margin * 2f;
        _bubble.sizeDelta = new Vector2(width, _bubble.sizeDelta.y);
        _bubble.localScale = Vector3.one;
        _bubble.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_bubble);
        float height = _bubble.rect.height;

        Vector2 a = _root.InverseTransformPoint(linkMinWorld);
        Vector2 b = _root.InverseTransformPoint(linkMaxWorld);
        Vector2 c = _root.InverseTransformPoint(avoidMinWorld);
        Vector2 d = _root.InverseTransformPoint(avoidMaxWorld);
        float linkCenterX = (a.x + b.x) * 0.5f;
        bool below = (a.y + b.y) * 0.5f > area.center.y;
        float avoidBottom = Mathf.Min(Mathf.Min(c.y, d.y), Mathf.Min(a.y, b.y));
        float avoidTop = Mathf.Max(Mathf.Max(c.y, d.y), Mathf.Max(a.y, b.y));

        float top = below ? avoidBottom - _gap : avoidTop + _gap + height;
        top = Mathf.Clamp(top, area.yMin + _margin + height, area.yMax - _margin);
        _bubble.pivot = new Vector2(0.5f, below ? 1f : 0f);
        _bubble.anchoredPosition = new Vector2(0f, (below ? top : top - height) - area.center.y);

        float half = width * 0.5f - 70f;
        _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, below ? 1f : 0f);
        _arrow.anchoredPosition = new Vector2(Mathf.Clamp(linkCenterX - area.center.x, -half, half), 0f);

        _bubble.localScale = Vector3.one * 0.86f;
        _bubble.DOScale(1f, _bubbleOpenDuration).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
    }

    /// <summary>B 바텀 시트: 전체 설명 + 같은 카드의 다른 용어 칩.</summary>
    public void ShowSheet(TermPopupLabTerm term, IReadOnlyList<TermPopupLabTerm> siblings)
    {
        KillTweens();
        _bubble.gameObject.SetActive(false);
        _shield.SetActive(true);
        Fill(term, _sheetKindBg, _sheetKind, _sheetTitle, _sheetBody, _sheetFacts, false);
        BuildChips(siblings, term.Id);

        _sheetEdge.color = _punchySheet ? term.KindColor : TermColor;
        _sheetEdge.rectTransform.sizeDelta = new Vector2(0f, _punchySheet ? _punchEdgeHeight : 4f);
        _sheet.gameObject.SetActive(true);

        float max = _root.rect.height * _sheetHeightRatio;
        if (_punchySheet)
        {
            // 내용에 맞춘 높이 — 고정 높이일 때 아래 절반이 비어 늘어져 보이던 것을 줄인다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_sheet);
            float content = LayoutUtility.GetPreferredHeight(_sheet) - SheetOverscan;
            _sheetHeight = Mathf.Clamp(content, Mathf.Min(_sheetMinHeight, max), max);
        }
        else _sheetHeight = max;
        _sheet.sizeDelta = new Vector2(0f, _sheetHeight + SheetOverscan);

        float rest = -SheetOverscan;
        if (!_sheetOpen) _sheet.anchoredPosition = new Vector2(0f, -(_sheetHeight + SheetOverscan));
        _sheetOpen = true;
        if (!_punchySheet)
        {
            _sheet.DOAnchorPosY(rest, _sheetOpenDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
            return;
        }

        _sheet.DOAnchorPosY(rest, _punchOpenDuration).SetEase(Ease.OutBack, _punchOvershoot).SetUpdate(true).SetLink(gameObject);
        _sheetHeader.localScale = Vector3.one * 0.7f;
        _sheetHeader.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f).SetDelay(_punchContentDelay).SetUpdate(true).SetLink(gameObject);
        _sheetBadge.localScale = Vector3.zero;
        _sheetBadge.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 3f).SetDelay(_punchContentDelay + 0.06f).SetUpdate(true).SetLink(gameObject);
        for (int i = 0; i < _sheetParts.Count; i++)
        {
            _sheetParts[i].alpha = 0f;
            _sheetParts[i].DOFade(1f, 0.2f).SetDelay(_punchContentDelay + _punchStagger * (i + 1)).SetUpdate(true).SetLink(gameObject);
        }
    }

    /// <summary>방패 탭과 같은 닫기 — 시트는 내려가며 사라진다.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        KillTweens();
        _shield.SetActive(false);
        _bubble.gameObject.SetActive(false);
        if (_sheetOpen)
        {
            _sheetOpen = false;
            _sheet.DOAnchorPosY(-(SheetHeight + SheetOverscan), _sheetCloseDuration).SetEase(Ease.InCubic).SetUpdate(true).SetLink(gameObject)
                  .OnComplete(() => _sheet.gameObject.SetActive(false));
        }
        Closed?.Invoke();
    }

    /// <summary>연출 없이 모두 숨긴다 (카드 다시 뽑기 등).</summary>
    public void HideImmediately()
    {
        KillTweens();
        _shield.SetActive(false);
        _bubble.gameObject.SetActive(false);
        HideSheetImmediately();
    }

    private void HideSheetImmediately()
    {
        _sheetOpen = false;
        _sheet.gameObject.SetActive(false);
    }

    private void KillTweens()
    {
        _bubble.DOKill();
        _sheet.DOKill();
        // 중간에 끊긴 등장 연출이 반쯤 투명·축소된 채 남지 않게 최종 상태로 돌린다.
        if (_sheetHeader != null) { _sheetHeader.DOKill(); _sheetHeader.localScale = Vector3.one; }
        if (_sheetBadge != null) { _sheetBadge.DOKill(); _sheetBadge.localScale = Vector3.one; }
        foreach (var part in _sheetParts) { part.DOKill(); part.alpha = 1f; }
    }

    private void OnDestroy()
    {
        if (_bubble != null) _bubble.DOKill();
        if (_sheet != null) _sheet.DOKill();
        if (_sheetHeader != null) _sheetHeader.DOKill();
        if (_sheetBadge != null) _sheetBadge.DOKill();
        foreach (var part in _sheetParts) if (part != null) part.DOKill();
    }

    private static void Fill(TermPopupLabTerm term, Image kindBg, TMP_Text kind, TMP_Text title, TMP_Text body, TMP_Text facts, bool summary)
    {
        kindBg.color = term.KindColor;
        kind.text = term.Kind;
        title.text = term.Id;
        body.text = summary && !string.IsNullOrEmpty(term.Short) ? term.Short : term.Body;
        bool showFacts = !summary && !string.IsNullOrEmpty(term.Facts);
        facts.gameObject.SetActive(showFacts);
        if (showFacts) facts.text = term.Facts;
    }

    private void BuildChips(IReadOnlyList<TermPopupLabTerm> siblings, string currentId)
    {
        for (int i = _chipRow.childCount - 1; i >= 0; i--)
        {
            var old = _chipRow.GetChild(i).gameObject;
            old.SetActive(false); // Destroy는 프레임 끝이라, 높이 계산에 끼지 않게 먼저 끈다
            Destroy(old);
        }
        int count = 0;
        if (siblings != null)
        {
            foreach (var other in siblings)
            {
                if (other == null || other.Id == currentId) continue;
                count++;
                var chip = NewRect("Chip_" + other.Id, _chipRow);
                var bg = chip.gameObject.AddComponent<Image>();
                bg.color = new Color(TermColor.r, TermColor.g, TermColor.b, 0.16f);
                ApplyRounded(bg);
                var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(26, 26, 10, 12);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var label = Text(chip, "Label", 30f, TermColor);
                label.text = other.Id;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                string id = other.Id;
                chip.gameObject.AddComponent<Button>().onClick.AddListener(() => ChipTapped?.Invoke(id));
            }
        }
        _chipLabel.gameObject.SetActive(count > 0);
        _chipRow.gameObject.SetActive(count > 0);
    }

    private void Build()
    {
        // 방패: 투명하지만 누름을 모두 받는다 — 바깥 탭은 닫기만 한다.
        var shield = NewRect("Shield", _root);
        Stretch(shield);
        shield.gameObject.AddComponent<Image>().color = Color.clear;
        var shieldButton = shield.gameObject.AddComponent<Button>();
        shieldButton.transition = Selectable.Transition.None;
        shieldButton.onClick.AddListener(Close);
        _shield = shield.gameObject;

        // A 말풍선
        _bubble = NewRect("Bubble", _root);
        _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, 0.5f);
        var bubbleBg = _bubble.gameObject.AddComponent<Image>();
        bubbleBg.color = BubbleColor;
        ApplyRounded(bubbleBg);
        // 어두운 등급 배경 위에서 말풍선이 묻히지 않게 얇은 하늘색 테두리 (꼬리는 테두리 없이 이어 붙음).
        var bubbleOutline = _bubble.gameObject.AddComponent<Outline>();
        bubbleOutline.effectColor = new Color(TermColor.r, TermColor.g, TermColor.b, 0.75f);
        bubbleOutline.effectDistance = new Vector2(3f, -3f);
        // 말풍선 자체를 눌러도 닫힌다 ("자세히 보기"는 자기 버튼이 먼저 받는다).
        var bubbleButton = _bubble.gameObject.AddComponent<Button>();
        bubbleButton.transition = Selectable.Transition.None;
        bubbleButton.onClick.AddListener(Close);
        var bubbleLayout = _bubble.gameObject.AddComponent<VerticalLayoutGroup>();
        bubbleLayout.padding = new RectOffset(44, 44, 34, 36);
        bubbleLayout.spacing = 12f;
        bubbleLayout.childControlWidth = bubbleLayout.childControlHeight = true;
        bubbleLayout.childForceExpandWidth = true;
        bubbleLayout.childForceExpandHeight = false;
        _bubble.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _arrow = NewRect("Arrow", _bubble);
        _arrow.gameObject.AddComponent<Image>().color = BubbleColor;
        _arrow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        _arrow.sizeDelta = new Vector2(46f, 46f);
        _arrow.localRotation = Quaternion.Euler(0f, 0f, 45f);
        _arrow.SetAsFirstSibling();
        Header(_bubble, Cream, out _bubbleKindBg, out _bubbleKind, out _bubbleTitle);
        _bubbleBody = Text(_bubble, "Body", 34f, SheetText);
        _bubbleBody.lineSpacing = 10f;
        _bubbleFacts = Text(_bubble, "Facts", 30f, TermColor);
        var more = Text(_bubble, "More", 32f, TermColor);
        more.text = "자세히 보기 >";
        more.fontStyle = FontStyles.Bold;
        more.alignment = TextAlignmentOptions.MidlineRight;
        more.raycastTarget = true;
        more.gameObject.AddComponent<Button>().onClick.AddListener(() => MoreRequested?.Invoke());
        _bubbleMore = more.gameObject;

        // B 바텀 시트
        _sheet = NewRect("Sheet", _root);
        _sheet.anchorMin = new Vector2(0f, 0f);
        _sheet.anchorMax = new Vector2(1f, 0f);
        _sheet.pivot = new Vector2(0.5f, 0f);
        var sheetBg = _sheet.gameObject.AddComponent<Image>();
        sheetBg.color = SheetColor;
        var sheetLayout = _sheet.gameObject.AddComponent<VerticalLayoutGroup>();
        sheetLayout.padding = new RectOffset(60, 60, 26, 48 + (int)SheetOverscan);
        sheetLayout.spacing = 18f;
        sheetLayout.childControlWidth = sheetLayout.childControlHeight = true;
        sheetLayout.childForceExpandWidth = true;
        sheetLayout.childForceExpandHeight = false;
        var edge = NewRect("TopEdge", _sheet);
        _sheetEdge = edge.gameObject.AddComponent<Image>();
        _sheetEdge.color = TermColor;
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        edge.anchorMin = new Vector2(0f, 1f);
        edge.anchorMax = Vector2.one;
        edge.pivot = new Vector2(0.5f, 1f);
        edge.sizeDelta = new Vector2(0f, 4f);
        edge.anchoredPosition = Vector2.zero;
        var grabRow = NewRect("GrabRow", _sheet);
        grabRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;
        var grab = NewRect("Grab", grabRow);
        grab.anchorMin = grab.anchorMax = new Vector2(0.5f, 0.5f);
        grab.sizeDelta = new Vector2(100f, 10f);
        var grabImage = grab.gameObject.AddComponent<Image>();
        grabImage.color = new Color(0.29f, 0.31f, 0.39f, 1f);
        ApplyRounded(grabImage);
        _sheetHeader = Header(_sheet, Cream, out _sheetKindBg, out _sheetKind, out _sheetTitle);
        _sheetHeader.pivot = new Vector2(0f, 0.5f); // 제목 줄은 왼쪽 기준으로 튀어나온다
        _sheetBadge = _sheetKindBg.rectTransform;
        _sheetTitle.fontSize = 50f;
        _sheetBody = Text(_sheet, "Body", 37f, SheetText);
        _sheetBody.lineSpacing = 12f;
        _sheetFacts = Text(_sheet, "Facts", 32f, TermColor);
        _chipLabel = Text(_sheet, "ChipLabel", 26f, Muted);
        _chipLabel.text = "이 카드의 다른 용어";
        _chipRow = NewRect("Chips", _sheet);
        var chipLayout = _chipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipLayout.spacing = 16f;
        chipLayout.childControlWidth = chipLayout.childControlHeight = true;
        chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;
        chipLayout.childAlignment = TextAnchor.MiddleLeft;
        var hint = Text(_sheet, "Hint", 26f, Muted);
        hint.text = "바깥을 누르면 닫힘";
        foreach (var part in new Component[] { _sheetBody, _sheetFacts, _chipLabel, _chipRow, hint })
            _sheetParts.Add(part.gameObject.AddComponent<CanvasGroup>());
    }

    private RectTransform Header(RectTransform parent, Color titleColor, out Image kindBg, out TMP_Text kind, out TMP_Text title)
    {
        var row = NewRect("Header", parent);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var badge = NewRect("Kind", row);
        kindBg = badge.gameObject.AddComponent<Image>();
        ApplyRounded(kindBg);
        var badgeLayout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
        badgeLayout.padding = new RectOffset(16, 16, 6, 8);
        badgeLayout.childControlWidth = badgeLayout.childControlHeight = true;
        badgeLayout.childForceExpandWidth = badgeLayout.childForceExpandHeight = false;
        kind = Text(badge, "Label", 26f, Color.white);
        kind.textWrappingMode = TextWrappingModes.NoWrap;
        title = Text(row, "Title", 46f, titleColor);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return row;
    }

    private TMP_Text Text(Transform parent, string name, float size, Color color)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        if (_fontMaterial != null) text.fontSharedMaterial = _fontMaterial;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private void ApplyRounded(Image image)
    {
        if (_rounded == null) return;
        image.sprite = _rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.35f;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
