using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using GaeGGUL.UI.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 획득 선택지 목록 실험실(FxLab_SelectionInventory) — 사용자 10-13 "옵션(≡) 눌렀을 때 레벨업 선택지 보유 목록 임시 UI 시안",
/// "어느 유닛 것인지(정보창 OK)", "번잡하지 않게" → A 아이콘 보관함 선택.
/// 2차 요청: 왼쪽 위부터 채워지는 느낌 + 부드러운 연출, 레벨업 선택지처럼 설명 속 용어를 누르면 용어 시트, 핵심 용어는 영상.
/// 카드·아이콘·확정 설명은 실제 LevelUpData, 대상 유닛은 실제 Composition.Feedback.Target,
/// 용어는 실제 TermGlossary + 카드별 DescriptionTerms를 선택지와 같은 TermLinker/TermLinkTapTarget/TermPopup으로 연결한다.
/// 런타임에 UI를 코드로 만들며 원본 프리팹·코드는 건드리지 않는다.
/// </summary>
public sealed class SelectionInventoryLab : MonoBehaviour
{
    /// <summary>시안 이름.</summary>
    public static readonly string[] Labels = { "A 아이콘 보관함", "B 목록", "C 유닛별" };

    /// <summary>표본 크기(획득 장수) 이름.</summary>
    public static readonly string[] SampleLabels = { "3장(초반)", "8장(중반)", "24장(전부)" };

    private static readonly string[] Descriptions =
    {
        "왼쪽 위부터 채움. 얼굴 = 대상 유닛. 파란 글자 = 용어, 주황 = 영상 용어",
        "한 줄에 아이콘·이름·요약·대상 얼굴. 줄을 누르면 요약↔상세 설명 전환",
        "대상 유닛별로 묶음. 머리글 얼굴 누르면 정보창, 아이콘 누르면 아래 상세",
    };

    // 획득 순서 표본 (History 순서 가정)
    private static readonly int[] EarlySample = { 9101, 9107, 9110 };
    private static readonly int[] MidSample = { 9101, 9107, 9110, 9106, 9108, 9118, 9114, 9120 };

    // Feedback 미기입 카드 — 설명 문구 기준 실험실 임시 보정 (실제 적용 시 SO Feedback에 기입 필요)
    private static readonly Dictionary<int, SelectionFeedbackTarget> PatchedTargets = new()
    {
        { 9121, SelectionFeedbackTarget.Deltan }, { 9122, SelectionFeedbackTarget.Betan },
        { 9123, SelectionFeedbackTarget.Deltan }, { 9124, SelectionFeedbackTarget.Deltan },
    };

    private static readonly SelectionFeedbackTarget[] GroupOrder =
    {
        SelectionFeedbackTarget.ChiefSkill, SelectionFeedbackTarget.Betan, SelectionFeedbackTarget.Gamman,
        SelectionFeedbackTarget.Deltan, SelectionFeedbackTarget.Zeltan, SelectionFeedbackTarget.CombatDroneOwner,
        SelectionFeedbackTarget.AllUnits,
    };

    private static readonly Color PanelFill = new Color32(0x2A, 0x27, 0x66, 0xFF);
    private static readonly Color PanelWell = new Color32(0x1B, 0x19, 0x45, 0xFF);
    private static readonly Color EmptySlot = new Color(1f, 1f, 1f, 0.045f);
    private static readonly Color Ink = new Color32(0x0E, 0x0C, 0x24, 0xFF);
    private static readonly Color Muted = new Color32(0xB8, 0xB5, 0xE6, 0xFF);
    private static readonly Color Highlight = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
    private static readonly Color MenuBlue = new Color32(0x3F, 0x3C, 0xC2, 0xFF);
    private static readonly Color ButtonIdle = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.9f);
    private const float DimAlpha = 0.62f;

    private const float PanelWidth = 980f;
    private const float PanelHeight = 1380f;
    private const float PanelY = 40f;
    private const float HeaderHeight = 130f;
    private const float DetailHeight = 330f;
    private const float Pad = 28f;
    private const int GridColumns = 4;
    private const int MinGridSlots = 16;
    private const float GridCell = 200f;
    private const float GridGap = 16f;
    private const float GroupCell = 150f;
    private const float FaceBadge = 66f;
    private const float RowIcon = 112f;
    private const float RowFace = 84f;
    private const float Outline = 4f;
    private const float TermHitPadding = 18f;
    private const int TermLayerOrder = 40; // Unit_Info_Panel(30)보다 위

    [Header("데이터 (빌더가 연결)")]
    [SerializeField] private LevelUpData[] _cards = Array.Empty<LevelUpData>();
    [SerializeField] private UnitData _betan;
    [SerializeField] private UnitData _gamman;
    [SerializeField] private UnitData _deltan;
    [SerializeField] private UnitData _zeltan;
    [SerializeField] private ChieftainData _chief;
    [SerializeField] private Sprite _droneIcon;
    [SerializeField] private TermGlossary _glossary;

    [Header("UI (빌더가 연결)")]
    [SerializeField] private RectTransform _canvas;
    [SerializeField] private UI_UnitInfoPanel _infoPanel;
    [SerializeField] private TMP_FontAsset _titleFont;
    [SerializeField] private TMP_FontAsset _bodyFont;
    [SerializeField] private Sprite _rounded;
    [SerializeField] private Sprite _circle;
    [SerializeField] private Shader _blurShader;

    [Header("용어 색 (레벨업 선택지와 같은 계열, 어두운 바탕용으로 밝힘)")]
    [SerializeField] private Color _termColor = new Color(0.36f, 0.75f, 0.95f);
    [SerializeField] private Color _videoTermColor = new Color(1f, 0.62f, 0.1f);

    [Header("연출")]
    [SerializeField, Min(0.01f)] private float _openSeconds = 0.28f;
    [SerializeField, Min(0.01f)] private float _closeSeconds = 0.16f;
    [Tooltip("첫 칸이 나오기 시작하는 시점")]
    [SerializeField, Min(0f)] private float _fillDelay = 0.14f;
    [Tooltip("칸마다 간격. 칸이 많으면 전체가 _fillMaxSeconds 안에 끝나도록 줄인다")]
    [SerializeField, Min(0f)] private float _fillStagger = 0.045f;
    [SerializeField, Min(0.05f)] private float _fillMaxSeconds = 0.6f;
    [SerializeField, Min(0.01f)] private float _popSeconds = 0.3f;

    [Header("상태")]
    [SerializeField] private int _style;
    [SerializeField] private int _sample = 1;

    private sealed class Entry
    {
        public LevelUpData Card;
        public DescriptionSnapshot Snapshot;
        public string Fallback;
        public SelectionFeedbackTarget Target;
        public bool Patched;
        public bool Instant;
    }

    private readonly List<Entry> _entries = new();
    private readonly Dictionary<int, Entry> _byId = new();
    private readonly List<Image> _styleButtons = new();
    private readonly List<Image> _sampleButtons = new();
    private readonly List<(Entry Entry, Graphic Ring, RectTransform Slot)> _selectables = new();
    private readonly DescriptionModeState _termMode = new();
    private DescriptionDisplaySettings _descriptionSettings;
    private TermLinker _linker;
    private TermPopup _termPopup;
    private RectTransform _popup;
    private CanvasGroup _popupGroup;
    private RectTransform _panel;
    private Image _dim;
    private RectTransform _body;
    private RectTransform _detail;
    private TMP_Text _detailText;
    private RectTransform _miniPopup;
    private TMP_Text _status;
    private Entry _selected;
    private int _appearIndex;
    private int _appearCount;

    private void Start()
    {
        _descriptionSettings = new DescriptionDisplaySettings();
        _linker = _glossary != null ? new TermLinker(_glossary, _termColor, _videoTermColor) : null;
        PrepareEntries();
        BuildHud();
        BuildControls();
        BuildTermLayer();
        if (_infoPanel != null)
        {
            _infoPanel.OnDismissRequested += _infoPanel.Close;
            _infoPanel.gameObject.SetActive(false);
        }
        Open();
    }

    private void OnDestroy()
    {
        if (_descriptionSettings != null) _descriptionSettings.Changed -= RefreshDescriptionMode;
        _termMode.OnChanged -= ChangeDescriptionMode;
    }

    // ── 버튼 ─────────────────────────────────────────────────────────

    /// <summary>시안 선택: 0 아이콘 보관함, 1 목록, 2 유닛별.</summary>
    public void SelectStyle(int index)
    {
        _style = Mathf.Clamp(index, 0, Labels.Length - 1);
        Open();
    }

    /// <summary>표본 크기 선택: 0 초반, 1 중반, 2 전부.</summary>
    public void SelectSample(int index)
    {
        _sample = Mathf.Clamp(index, 0, SampleLabels.Length - 1);
        Open();
    }

    /// <summary>목록 창을 연다(≡ 메뉴 버튼과 같은 동작). 패널이 튀어나오고 칸이 왼쪽 위부터 차례로 채워진다.</summary>
    public void Open()
    {
        if (_popup != null) Destroy(_popup.gameObject);
        CloseMini();
        _termPopup?.HideImmediately();
        if (_infoPanel != null && _infoPanel.gameObject.activeSelf) _infoPanel.Close();
        _selectables.Clear();
        var owned = Owned();
        _selected = owned.Count > 0 ? owned[owned.Count - 1] : null;
        BuildPopup(owned);
        PlayOpen(owned.Count);
        RefreshButtons();
    }

    /// <summary>목록 창을 닫아 전투 화면과 ≡ 버튼을 본다.</summary>
    public void Close()
    {
        CloseMini();
        _termPopup?.HideImmediately();
        if (_popup == null) { RefreshButtons(); return; }
        var closing = _popup;
        _popup = null;
        DOTween.Kill(closing);
        _popupGroup.DOFade(0f, _closeSeconds).SetUpdate(true).SetLink(closing.gameObject);
        _panel.DOScale(0.94f, _closeSeconds).SetEase(Ease.InCubic).SetUpdate(true).SetLink(closing.gameObject)
            .OnComplete(() => Destroy(closing.gameObject));
        RefreshButtons();
    }

    /// <summary>시안 × 표본 전체와 채움 연출·용어 시트·정보창을 PNG로 저장한다.</summary>
    public void CaptureAll(string folder) => StartCoroutine(CaptureRoutine(folder));

    // ── 데이터 ───────────────────────────────────────────────────────

    private void PrepareEntries()
    {
        var state = new LevelUpRunState();
        state.BeginRun(GetInstanceID());
        var formatter = new SelectionDescriptionFormatter();
        foreach (var card in _cards)
        {
            if (card == null) continue;
            var e = new Entry { Card = card, Fallback = card.description };
            try { e.Snapshot = formatter.Capture(state.Prepare(card, UnityEngine.Random.Range), card.simpleDescription); }
            catch (Exception ex) { Debug.LogWarning($"[SelectionInventoryLab] {card.chooseId} 설명 계산 실패: {ex.Message}"); }

            var feedback = card.Composition?.Feedback;
            if (feedback != null && feedback.Count > 0 && feedback[0] != null) e.Target = feedback[0].Target;
            else if (PatchedTargets.TryGetValue(card.chooseId, out var patched)) { e.Target = patched; e.Patched = true; }
            else e.Target = SelectionFeedbackTarget.AllUnits;
            e.Instant = card.Composition != null && card.Composition.Commands != null && card.Composition.Commands.Count > 0
                        && (card.Composition.Effects == null || card.Composition.Effects.Count == 0);
            _entries.Add(e);
            _byId[card.chooseId] = e;
        }
    }

    /// <summary>레벨업 선택지와 같은 경로: 간단히/자세히 설정 → 확정 수치 → 카드에 저작된 용어만 링크.</summary>
    private string CardText(Entry e, bool detailed)
    {
        string text = e.Snapshot != null ? DescriptionFormatter.Format(e.Snapshot, detailed, null, true) : e.Fallback;
        return _linker == null ? text : _linker.AddLinks(text, e.Card.DescriptionTerms);
    }

    private string SimpleText(Entry e)
        => e.Snapshot != null ? DescriptionFormatter.Format(e.Snapshot, false, null, false) : e.Card.simpleDescription;

    private List<Entry> Owned()
    {
        if (_sample >= 2) return new List<Entry>(_entries);
        var ids = _sample == 0 ? EarlySample : MidSample;
        var list = new List<Entry>();
        foreach (int id in ids) if (_byId.TryGetValue(id, out var e)) list.Add(e);
        return list;
    }

    private string TargetName(SelectionFeedbackTarget target) => target switch
    {
        SelectionFeedbackTarget.ChiefSkill => _chief != null ? $"{_chief.DisplayName}(족장)" : "족장",
        SelectionFeedbackTarget.Betan => _betan != null ? _betan.unitName : "베탕",
        SelectionFeedbackTarget.Gamman => _gamman != null ? _gamman.unitName : "감망",
        SelectionFeedbackTarget.Deltan => _deltan != null ? _deltan.unitName : "델탕",
        SelectionFeedbackTarget.Zeltan => _zeltan != null ? _zeltan.unitName : "젤탕",
        SelectionFeedbackTarget.CombatDroneOwner => "드론 보유 유닛",
        _ => "공용",
    };

    private Sprite TargetFace(SelectionFeedbackTarget target) => target switch
    {
        SelectionFeedbackTarget.ChiefSkill => _chief != null ? _chief.Icon : null,
        SelectionFeedbackTarget.Betan => _betan != null ? _betan.icon : null,
        SelectionFeedbackTarget.Gamman => _gamman != null ? _gamman.icon : null,
        SelectionFeedbackTarget.Deltan => _deltan != null ? _deltan.icon : null,
        SelectionFeedbackTarget.Zeltan => _zeltan != null ? _zeltan.icon : null,
        SelectionFeedbackTarget.CombatDroneOwner => _droneIcon,
        _ => null,
    };

    private UnitData[] TargetUnits(SelectionFeedbackTarget target) => target switch
    {
        SelectionFeedbackTarget.Betan => new[] { _betan },
        SelectionFeedbackTarget.Gamman => new[] { _gamman },
        SelectionFeedbackTarget.Deltan => new[] { _deltan },
        SelectionFeedbackTarget.Zeltan => new[] { _zeltan },
        SelectionFeedbackTarget.CombatDroneOwner => new[] { _betan, _gamman, _deltan },
        _ => Array.Empty<UnitData>(),
    };

    private static string TierName(Tier tier) => tier switch
    {
        Tier.Rare => "레어", Tier.Epic => "에픽", Tier.Legend => "전설", _ => tier.ToString(),
    };

    private static Color TierText(Tier tier) => tier switch
    {
        Tier.Epic => new Color32(0xF2, 0xB5, 0x33, 0xFF),
        Tier.Legend => new Color32(0xFF, 0x6A, 0x5E, 0xFF),
        _ => new Color32(0xC0, 0xA0, 0xF9, 0xFF),
    };

    // UnitTierPalette 배경색과 같은 계열
    private static Color TierFill(Tier tier) => tier switch
    {
        Tier.Epic => new Color32(0x7A, 0x4F, 0x22, 0xFF),
        Tier.Legend => new Color32(0x88, 0x38, 0x38, 0xFF),
        _ => new Color32(0x47, 0x33, 0x4C, 0xFF),
    };

    // ── 용어 ─────────────────────────────────────────────────────────

    // 정보창(30) 위에 별도 캔버스 층을 두고 선택지와 같은 TermPopup(바텀 시트 + 영상 필름)을 띄운다
    private void BuildTermLayer()
    {
        if (_glossary == null) return;
        var layer = Rect("TermLayer", _canvas);
        Stretch(layer);
        var canvas = layer.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = TermLayerOrder;
        layer.gameObject.AddComponent<GraphicRaycaster>();
        var fontSource = Text("FontSource", layer, _bodyFont, 26f, Color.white, TextAlignmentOptions.Left);
        fontSource.gameObject.SetActive(false);
        _termPopup = TermPopup.Create(layer, fontSource, DescriptionToggleView.TrackSprite, _blurShader);
        _termMode.Set(_descriptionSettings.Detailed ? DescriptionMode.Detailed : DescriptionMode.Brief);
        _termPopup.ConfigureDescriptionMode(_termMode);
        _termMode.OnChanged += ChangeDescriptionMode;
        _descriptionSettings.Changed += RefreshDescriptionMode;
        _termPopup.SiblingTapped += ShowTerm;
    }

    private void ChangeDescriptionMode(DescriptionMode mode) => _descriptionSettings.SetDetailed(mode == DescriptionMode.Detailed);

    private void RefreshDescriptionMode()
    {
        _termMode.Set(_descriptionSettings.Detailed ? DescriptionMode.Detailed : DescriptionMode.Brief);
        if (_detailText != null && _selected != null) SetDetailText(_selected);
    }

    private void OnTermTapped(TermLinkTapTarget source, string linkId)
    {
        if (linkId == null || !linkId.StartsWith(TermLinker.LinkPrefix, StringComparison.Ordinal)) return;
        ShowTerm(linkId.Substring(TermLinker.LinkPrefix.Length));
    }

    private void ShowTerm(string id)
    {
        if (_termPopup == null || _selected == null || !_glossary.TryFind(id, out var entry)) return;
        var siblings = new List<TermEntry>();
        foreach (var termId in _selected.Card.DescriptionTerms)
            if (_glossary.TryFind(termId, out var other)) siblings.Add(other);
        _termPopup.Show(entry, siblings);
    }

    // ── 화면 ─────────────────────────────────────────────────────────

    private void BuildHud()
    {
        // 실제 게임의 ≡ 메뉴 버튼 위치(토템 버튼 위)를 흉내낸다
        var menu = Box("MenuButton", _canvas, _rounded, MenuBlue);
        Anchor(menu.rectTransform, new Vector2(1f, 0f), new Vector2(-82f, 330f), new Vector2(78f, 78f));
        AddOutline(menu, Ink);
        for (int i = 0; i < 3; i++)
        {
            var bar = Box($"Bar{i}", menu.rectTransform, _rounded, Color.white);
            Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f - 16f * i), new Vector2(44f, 7f));
            bar.raycastTarget = false;
        }
        Clickable(menu.gameObject, Open);
        var tag = Text("MenuHint", _canvas, _bodyFont, 22f, Color.white, TextAlignmentOptions.Right);
        Anchor(tag.rectTransform, new Vector2(1f, 0f), new Vector2(-82f - 39f - 14f - 160f, 330f), new Vector2(320f, 40f));
        tag.text = "≡ 메뉴 → 선택지";
        tag.outlineWidth = 0.25f;
        tag.outlineColor = Color.black;
    }

    private void BuildControls()
    {
        var rowStyles = Row("Styles", -24f);
        for (int i = 0; i < Labels.Length; i++)
        {
            int index = i;
            _styleButtons.Add(ControlButton(rowStyles, Labels[i], () => SelectStyle(index)));
        }
        var rowSamples = Row("Samples", -96f);
        for (int i = 0; i < SampleLabels.Length; i++)
        {
            int index = i;
            _sampleButtons.Add(ControlButton(rowSamples, SampleLabels[i], () => SelectSample(index)));
        }
        ControlButton(rowSamples, "닫기(≡ 보기)", Close);

        var statusBack = Box("StatusBack", _canvas, _rounded, new Color(0f, 0f, 0f, 0.6f));
        statusBack.raycastTarget = false;
        statusBack.rectTransform.anchorMin = new Vector2(0f, 1f);
        statusBack.rectTransform.anchorMax = new Vector2(1f, 1f);
        statusBack.rectTransform.pivot = new Vector2(0.5f, 1f);
        statusBack.rectTransform.sizeDelta = new Vector2(-24f, 92f);
        statusBack.rectTransform.anchoredPosition = new Vector2(0f, -162f);
        _status = Text("Status", _canvas, _bodyFont, 22f, Color.white, TextAlignmentOptions.TopLeft);
        _status.rectTransform.anchorMin = new Vector2(0f, 1f);
        _status.rectTransform.anchorMax = new Vector2(1f, 1f);
        _status.rectTransform.pivot = new Vector2(0.5f, 1f);
        _status.rectTransform.sizeDelta = new Vector2(-40f, 80f);
        _status.rectTransform.anchoredPosition = new Vector2(0f, -170f);
        _status.textWrappingMode = TextWrappingModes.Normal;
    }

    private void RefreshButtons()
    {
        for (int i = 0; i < _styleButtons.Count; i++) _styleButtons[i].color = i == _style ? ButtonSelected : ButtonIdle;
        for (int i = 0; i < _sampleButtons.Count; i++) _sampleButtons[i].color = i == _sample ? ButtonSelected : ButtonIdle;
        if (_status == null) return;
        int patched = 0;
        foreach (var e in _entries) if (e.Patched) patched++;
        _status.text = $"{Labels[_style]} — {Descriptions[_style]}\n<size=80%><color=#FFD23F>대상 유닛 = SO Composition.Feedback.Target. 미기입 {patched}장(9121~9124)은 설명 기준 임시 보정</color></size>";
    }

    private void BuildPopup(List<Entry> owned)
    {
        _popup = Rect("SelectionInventory", _canvas);
        Stretch(_popup);
        _popup.SetSiblingIndex(_canvas.childCount - 1);
        var termLayer = _canvas.Find("TermLayer");
        if (termLayer != null) termLayer.SetAsLastSibling();
        _popupGroup = _popup.gameObject.AddComponent<CanvasGroup>();
        _dim = Box("Dim", _popup, null, new Color(0f, 0f, 0f, DimAlpha));
        Stretch(_dim.rectTransform);
        Clickable(_dim.gameObject, Close);

        var panel = Box("Panel", _popup, _rounded, PanelFill);
        Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, PanelY), new Vector2(PanelWidth, PanelHeight));
        AddOutline(panel, Ink);
        Clickable(panel.gameObject, null); // 패널 안쪽 클릭이 Dim으로 새지 않게
        _panel = panel.rectTransform;

        // 머리글: 제목 + 등급별 장수 + 닫기
        var title = Text("Title", _panel, _titleFont, 50f, Color.white, TextAlignmentOptions.Left);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(Pad + 230f, -70f), new Vector2(460f, 80f));
        title.outlineWidth = 0.2f;
        title.outlineColor = Ink;
        CountUp(title, owned.Count);
        var tally = Text("Tally", _panel, _bodyFont, 24f, Muted, TextAlignmentOptions.Right);
        Anchor(tally.rectTransform, new Vector2(1f, 1f), new Vector2(-Pad - 230f, -72f), new Vector2(360f, 50f));
        tally.text = Tally(owned);
        var close = Box("Close", _panel, _circle, new Color32(0xE8, 0x53, 0x4D, 0xFF));
        Anchor(close.rectTransform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(76f, 76f));
        AddOutline(close, Ink);
        var x = Text("X", close.rectTransform, _titleFont, 40f, Color.white, TextAlignmentOptions.Center);
        Stretch(x.rectTransform);
        x.text = "X";
        Clickable(close.gameObject, Close);

        bool hasDetail = _style != 1;
        var well = Box("Well", _panel, _rounded, PanelWell);
        well.rectTransform.anchorMin = Vector2.zero;
        well.rectTransform.anchorMax = Vector2.one;
        well.rectTransform.offsetMin = new Vector2(Pad, Pad + (hasDetail ? DetailHeight + 16f : 0f));
        well.rectTransform.offsetMax = new Vector2(-Pad, -HeaderHeight);
        _body = ScrollArea(well.rectTransform);

        _appearIndex = 0;
        _appearCount = owned.Count;
        if (owned.Count == 0 && _style != 0)
        {
            var empty = Text("Empty", _body, _bodyFont, 28f, Muted, TextAlignmentOptions.Center);
            empty.text = "아직 획득한 선택지가 없습니다";
            Fixed(empty.gameObject, 200f);
        }
        else if (_style == 0) BuildGrid(owned);
        else if (_style == 1) BuildList(owned);
        else BuildGroups(owned);

        _detail = null;
        _detailText = null;
        if (hasDetail)
        {
            var detailBox = Box("Detail", _panel, _rounded, PanelWell);
            detailBox.rectTransform.anchorMin = new Vector2(0f, 0f);
            detailBox.rectTransform.anchorMax = new Vector2(1f, 0f);
            detailBox.rectTransform.pivot = new Vector2(0.5f, 0f);
            detailBox.rectTransform.offsetMin = new Vector2(Pad, Pad);
            detailBox.rectTransform.offsetMax = new Vector2(-Pad, Pad + DetailHeight);
            _detail = detailBox.rectTransform;
            ShowDetail(_selected, false);
        }
    }

    private static string Tally(List<Entry> owned)
    {
        int rare = 0, epic = 0, legend = 0;
        foreach (var e in owned)
        {
            if (e.Card.tier == Tier.Legend) legend++;
            else if (e.Card.tier == Tier.Epic) epic++;
            else rare++;
        }
        return $"<color=#C0A0F9>레어 {rare}</color>  <color=#F2B533>에픽 {epic}</color>  <color=#FF6A5E>전설 {legend}</color>";
    }

    // A — 왼쪽 위부터 채워지는 보관함: 4열 고정, 빈칸은 옅게 깔아 "채워지는 중"으로 읽히게 한다
    private void BuildGrid(List<Entry> owned)
    {
        var grid = _body.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(GridCell, GridCell);
        grid.spacing = new Vector2(GridGap, GridGap + 4f);
        float inner = PanelWidth - Pad * 2f;
        int side = Mathf.RoundToInt((inner - (GridColumns * GridCell + (GridColumns - 1) * GridGap)) * 0.5f);
        grid.padding = new RectOffset(side, side, 24, 24);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = GridColumns;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        foreach (var e in owned) Slot(_body, e, GridCell, true);
        int total = Mathf.Max(MinGridSlots, Mathf.CeilToInt(owned.Count / (float)GridColumns) * GridColumns);
        for (int i = owned.Count; i < total; i++)
        {
            var empty = Box($"Empty_{i}", _body, _rounded, EmptySlot);
            empty.raycastTarget = false;
            var group = empty.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.DOFade(1f, 0.25f).SetDelay(_fillDelay * 0.5f).SetUpdate(true).SetLink(empty.gameObject);
        }
    }

    // B — 줄 목록
    private void BuildList(List<Entry> owned)
    {
        var list = _body.gameObject.AddComponent<VerticalLayoutGroup>();
        list.padding = new RectOffset(18, 18, 18, 18);
        list.spacing = 12f;
        list.childControlHeight = true;
        list.childControlWidth = true;
        list.childForceExpandHeight = false;
        list.childForceExpandWidth = true;
        for (int i = owned.Count - 1; i >= 0; i--) ListRow(owned[i]); // 최근 획득이 위
    }

    // C — 유닛별 묶음
    private void BuildGroups(List<Entry> owned)
    {
        var list = _body.gameObject.AddComponent<VerticalLayoutGroup>();
        list.padding = new RectOffset(18, 18, 18, 18);
        list.spacing = 18f;
        list.childControlHeight = true;
        list.childControlWidth = true;
        list.childForceExpandHeight = false;
        list.childForceExpandWidth = true;
        foreach (var target in GroupOrder)
        {
            var members = owned.FindAll(e => e.Target == target);
            if (members.Count == 0) continue;

            var header = Rect($"Group_{target}", _body);
            Fixed(header.gameObject, 84f);
            var face = Face(header, target, 76f);
            if (face != null)
            {
                Anchor((RectTransform)face.transform, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(76f, 76f));
                var t = target;
                Clickable(face.gameObject, () => OpenTargetInfo(t, 0));
            }
            var name = Text("Name", header, _titleFont, 32f, Color.white, TextAlignmentOptions.Left);
            Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(face != null ? 340f : 260f, 0f), new Vector2(480f, 60f));
            name.text = $"{TargetName(target)} <color=#B8B5E6><size=80%>{members.Count}</size></color>";
            var line = Box("Line", header, null, new Color(1f, 1f, 1f, 0.12f));
            line.rectTransform.anchorMin = new Vector2(0f, 0f);
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            line.raycastTarget = false;

            var icons = Rect("Icons", _body);
            var grid = icons.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(GroupCell, GroupCell);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(4, 4, 0, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            int rows = Mathf.CeilToInt(members.Count / 5f);
            Fixed(icons.gameObject, rows * GroupCell + (rows - 1) * 14f);
            foreach (var e in members) Slot(icons, e, GroupCell, false);
        }
    }

    private void Slot(RectTransform parent, Entry e, float size, bool showFace)
    {
        var frame = Box($"Slot_{e.Card.chooseId}", parent, _rounded, TierFill(e.Card.tier));
        var ring = AddOutline(frame, TierText(e.Card.tier));
        var icon = Box("Icon", frame.rectTransform, e.Card.icon, Color.white);
        Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(size * 0.78f, size * 0.78f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        if (e.Instant) icon.color = new Color(1f, 1f, 1f, 0.45f);
        Image face = null;
        if (showFace)
        {
            face = Face(frame.rectTransform, e.Target, FaceBadge);
            if (face != null)
            {
                Anchor((RectTransform)face.transform, new Vector2(1f, 0f), new Vector2(-FaceBadge * 0.32f, FaceBadge * 0.32f), new Vector2(FaceBadge, FaceBadge));
                face.raycastTarget = false;
            }
        }
        _selectables.Add((e, ring, frame.rectTransform));
        Clickable(frame.gameObject, () => { if (_selected != e) { _selected = e; ShowDetail(e, true); } else Punch(frame.rectTransform); });
        Appear(frame.rectTransform, face != null ? (RectTransform)face.transform : null);
    }

    private void ListRow(Entry e)
    {
        var row = Box($"Row_{e.Card.chooseId}", _body, _rounded, new Color(1f, 1f, 1f, 0.05f));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        var frame = Box("IconFrame", row.rectTransform, _rounded, TierFill(e.Card.tier));
        AddOutline(frame, TierText(e.Card.tier));
        Fixed(frame.gameObject, RowIcon, RowIcon);
        var icon = Box("Icon", frame.rectTransform, e.Card.icon, Color.white);
        Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RowIcon * 0.8f, RowIcon * 0.8f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        if (e.Instant) icon.color = new Color(1f, 1f, 1f, 0.45f);

        var column = Rect("Text", row.rectTransform);
        var colLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        colLayout.spacing = 4f;
        colLayout.childControlHeight = true;
        colLayout.childControlWidth = true;
        colLayout.childForceExpandHeight = false;
        column.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var name = Text("Name", column, _titleFont, 32f, TierText(e.Card.tier), TextAlignmentOptions.Left);
        name.text = e.Instant ? $"{e.Card.chooseName} <size=70%><color=#B8B5E6>즉시 효과</color></size>" : e.Card.chooseName;
        var desc = Text("Desc", column, _bodyFont, 24f, Color.white, TextAlignmentOptions.Left);
        desc.textWrappingMode = TextWrappingModes.Normal;
        desc.text = SimpleText(e);

        var faceHolder = Rect("Who", row.rectTransform);
        Fixed(faceHolder.gameObject, RowFace + 20f, RowFace + 30f);
        var face = Face(faceHolder, e.Target, RowFace);
        if (face != null)
        {
            Anchor((RectTransform)face.transform, new Vector2(0.5f, 1f), new Vector2(0f, -RowFace * 0.5f), new Vector2(RowFace, RowFace));
            var t = e.Target;
            Clickable(face.gameObject, () => OpenTargetInfo(t, 0));
        }
        var who = Text("WhoName", faceHolder, _bodyFont, 18f, Muted, TextAlignmentOptions.Center);
        Anchor(who.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(RowFace + 40f, 26f));
        who.text = face != null ? ShortName(e.Target) : "공용";

        bool detailed = false;
        Clickable(row.gameObject, () =>
        {
            detailed = !detailed;
            desc.text = detailed ? CardText(e, true) : SimpleText(e);
            desc.color = detailed ? Highlight : Color.white;
        });
        Appear(row.rectTransform, null);
    }

    private void ShowDetail(Entry e, bool animate)
    {
        foreach (var (entry, ring, slot) in _selectables)
        {
            bool on = entry == e;
            ring.color = on ? Highlight : TierText(entry.Card.tier);
            if (on && animate) Punch(slot);
        }
        if (_detail == null) return;
        _termPopup?.HideImmediately();
        for (int i = _detail.childCount - 1; i >= 0; i--) Destroy(_detail.GetChild(i).gameObject);
        _detailText = null;
        if (e == null)
        {
            var hint = Text("Hint", _detail, _bodyFont, 26f, Muted, TextAlignmentOptions.Center);
            Stretch(hint.rectTransform);
            hint.text = "레벨업에서 고른 선택지가 여기에 모입니다";
            return;
        }

        // 내용을 한 덩어리로 묶어 바꿀 때 살짝 밀려 들어오게 한다
        var content = Rect("Content", _detail);
        Stretch(content);
        var group = content.gameObject.AddComponent<CanvasGroup>();

        var frame = Box("IconFrame", content, _rounded, TierFill(e.Card.tier));
        AddOutline(frame, TierText(e.Card.tier));
        Anchor(frame.rectTransform, new Vector2(0f, 1f), new Vector2(26f + 85f, -26f - 85f), new Vector2(170f, 170f));
        var icon = Box("Icon", frame.rectTransform, e.Card.icon, Color.white);
        Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var name = Text("Name", content, _titleFont, 40f, TierText(e.Card.tier), TextAlignmentOptions.TopLeft);
        name.rectTransform.anchorMin = new Vector2(0f, 1f);
        name.rectTransform.anchorMax = new Vector2(1f, 1f);
        name.rectTransform.pivot = new Vector2(0f, 1f);
        name.rectTransform.offsetMin = new Vector2(222f, -80f);
        name.rectTransform.offsetMax = new Vector2(-24f, -24f);
        name.text = $"{e.Card.chooseName}  <size=60%><color=#B8B5E6>{TierName(e.Card.tier)}{(e.Instant ? " · 즉시 효과" : "")}</color></size>";

        _detailText = Text("Desc", content, _bodyFont, 26f, Color.white, TextAlignmentOptions.TopLeft);
        _detailText.rectTransform.anchorMin = new Vector2(0f, 0f);
        _detailText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _detailText.rectTransform.offsetMin = new Vector2(222f, 96f);
        _detailText.rectTransform.offsetMax = new Vector2(-24f, -86f);
        _detailText.textWrappingMode = TextWrappingModes.Normal;
        var tap = _detailText.gameObject.AddComponent<TermLinkTapTarget>();
        SetDetailText(e);
        tap.Configure(_detail.gameObject, OnTermTapped, TermHitPadding);

        // 적용 대상: 작은 얼굴 칩(눌러서 정보창). 공용은 글자만.
        var label = Text("WhoLabel", content, _bodyFont, 22f, Muted, TextAlignmentOptions.Left);
        Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(222f + 50f, 48f), new Vector2(100f, 40f));
        label.text = "적용";
        var units = TargetUnits(e.Target);
        float xPos = 222f + 110f;
        if (e.Target == SelectionFeedbackTarget.ChiefSkill)
            Chip(content, e.Target, -1, TargetName(e.Target), ref xPos);
        else if (units.Length == 0)
        {
            var common = Text("Common", content, _bodyFont, 22f, Color.white, TextAlignmentOptions.Left);
            Anchor(common.rectTransform, new Vector2(0f, 0f), new Vector2(xPos + 150f, 48f), new Vector2(300f, 40f));
            common.text = e.Instant ? "획득 즉시 1회 적용" : "모든 유닛";
        }
        else for (int i = 0; i < units.Length; i++)
            if (units[i] != null) Chip(content, e.Target, i, units[i].unitName, ref xPos);
        if (e.Patched)
        {
            var warn = Text("Patched", content, _bodyFont, 18f, Highlight, TextAlignmentOptions.Right);
            Anchor(warn.rectTransform, new Vector2(1f, 0f), new Vector2(-24f - 140f, 18f), new Vector2(280f, 26f));
            warn.text = "실험실 임시 보정(SO 미기입)";
        }

        if (!animate) return;
        group.alpha = 0f;
        group.DOFade(1f, 0.18f).SetUpdate(true).SetLink(content.gameObject);
        content.anchoredPosition = new Vector2(28f, 0f);
        content.DOAnchorPosX(0f, 0.24f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(content.gameObject);
    }

    private void SetDetailText(Entry e)
    {
        _detailText.text = CardText(e, _descriptionSettings.Detailed);
        _detailText.GetComponent<TermLinkTapTarget>()?.RefreshHitArea();
    }

    private void Chip(RectTransform parent, SelectionFeedbackTarget target, int unitIndex, string label, ref float xPos)
    {
        var chip = Box("Chip", parent, _rounded, new Color(1f, 1f, 1f, 0.1f));
        var text = Text("Label", chip.rectTransform, _bodyFont, 22f, Color.white, TextAlignmentOptions.Left);
        text.text = label;
        float width = 64f + 16f + text.preferredWidth + 20f;
        Anchor(chip.rectTransform, new Vector2(0f, 0f), new Vector2(xPos + width * 0.5f, 48f), new Vector2(width, 60f));
        Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(64f + 12f + text.preferredWidth * 0.5f, 0f), new Vector2(text.preferredWidth + 4f, 40f));
        Sprite face = unitIndex >= 0 ? TargetUnits(target)[unitIndex].icon : TargetFace(target);
        var faceImg = FaceImage(chip.rectTransform, face, 56f);
        Anchor((RectTransform)faceImg.transform, new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(56f, 56f));
        faceImg.raycastTarget = false;
        Clickable(chip.gameObject, () => { Punch(chip.rectTransform); OpenTargetInfo(target, Mathf.Max(0, unitIndex)); });
        xPos += width + 12f;
    }

    private static string ShortName(SelectionFeedbackTarget target) => target switch
    {
        SelectionFeedbackTarget.ChiefSkill => "족장",
        SelectionFeedbackTarget.CombatDroneOwner => "드론 보유",
        SelectionFeedbackTarget.Betan => "베탕",
        SelectionFeedbackTarget.Gamman => "감망",
        SelectionFeedbackTarget.Deltan => "델탕",
        SelectionFeedbackTarget.Zeltan => "젤탕",
        _ => "공용",
    };

    // ── 연출 ─────────────────────────────────────────────────────────

    private void PlayOpen(int count)
    {
        _popupGroup.alpha = 0f;
        _popupGroup.DOFade(1f, _openSeconds * 0.6f).SetUpdate(true).SetLink(_popup.gameObject);
        _panel.localScale = Vector3.one * 0.9f;
        _panel.DOScale(1f, _openSeconds).SetEase(Ease.OutBack, 1.6f).SetUpdate(true).SetLink(_popup.gameObject);
    }

    // 왼쪽 위부터 순서대로 튀어나온다. 칸이 많으면 간격을 줄여 전체 시간을 묶는다.
    private void Appear(RectTransform target, RectTransform badge)
    {
        float step = _appearCount > 1 ? Mathf.Min(_fillStagger, _fillMaxSeconds / (_appearCount - 1)) : 0f;
        float delay = _fillDelay + step * _appearIndex++;
        var group = target.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.DOFade(1f, _popSeconds * 0.5f).SetDelay(delay).SetUpdate(true).SetLink(target.gameObject);
        target.localScale = Vector3.one * 0.55f;
        target.DOScale(1f, _popSeconds).SetDelay(delay).SetEase(Ease.OutBack, 1.8f).SetUpdate(true).SetLink(target.gameObject);
        if (badge == null) return;
        badge.localScale = Vector3.zero;
        badge.DOScale(1f, 0.22f).SetDelay(delay + _popSeconds * 0.6f).SetEase(Ease.OutBack, 2.5f).SetUpdate(true).SetLink(badge.gameObject);
    }

    private static void Punch(RectTransform target)
    {
        target.DOComplete();
        target.localScale = Vector3.one;
        target.DOPunchScale(Vector3.one * 0.08f, 0.26f, 6, 0.6f).SetUpdate(true).SetLink(target.gameObject);
    }

    private void CountUp(TMP_Text title, int count)
    {
        int shown = 0;
        title.text = Title(0);
        float seconds = _fillDelay + Mathf.Min(_fillMaxSeconds, _fillStagger * Mathf.Max(0, count - 1)) + _popSeconds * 0.5f;
        DOTween.To(() => shown, v => { shown = v; title.text = Title(v); }, count, seconds)
            .SetEase(Ease.Linear).SetUpdate(true).SetLink(title.gameObject);
    }

    private static string Title(int count) => $"획득한 선택지 <color=#FFD23F>{count}</color>";

    // ── 정보창 ───────────────────────────────────────────────────────

    private void OpenTargetInfo(SelectionFeedbackTarget target, int unitIndex)
    {
        CloseMini();
        if (target == SelectionFeedbackTarget.ChiefSkill) { OpenChiefInfo(); return; }
        if (target == SelectionFeedbackTarget.CombatDroneOwner) { OpenDroneOwners(); return; }
        var units = TargetUnits(target);
        if (units.Length == 0 || _infoPanel == null) return;
        var data = units[Mathf.Clamp(unitIndex, 0, units.Length - 1)];
        if (data != null) _infoPanel.SetData(data);
    }

    // 족장은 그리드 유닛이 아니라 Unit_Info_Panel을 쓸 수 없어 간이 창으로 대신한다
    private void OpenChiefInfo()
    {
        var box = Mini(300f);
        var face = FaceImage(box, _chief != null ? _chief.Icon : null, 180f);
        Anchor((RectTransform)face.transform, new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(180f, 180f));
        var text = Text("Text", box, _bodyFont, 26f, Color.white, TextAlignmentOptions.Left);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(260f, 30f);
        text.rectTransform.offsetMax = new Vector2(-30f, -30f);
        text.textWrappingMode = TextWrappingModes.Normal;
        var skill = _chief != null ? _chief.AlphanSkill : null;
        string name = _chief != null ? _chief.DisplayName : "족장";
        text.text = skill != null
            ? $"<size=140%>{name}</size>  <color=#B8B5E6>족장</color>\n족장 스킬 쿨타임 {skill.CooldownSeconds:0.#}초\n드론 1기당 피해 {skill.DamagePerDrone:0.#}"
            : $"{name}  족장";
    }

    // "드론 보유 유닛" — 해당 유닛 얼굴을 고르게 한다
    private void OpenDroneOwners()
    {
        var box = Mini(260f);
        var title = Text("Title", box, _bodyFont, 24f, Muted, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 40f));
        title.text = "전투 드론을 가진 유닛";
        var units = TargetUnits(SelectionFeedbackTarget.CombatDroneOwner);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] == null) continue;
            int index = i;
            var face = FaceImage(box, units[i].icon, 120f);
            Anchor((RectTransform)face.transform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 180f, -16f), new Vector2(120f, 120f));
            Clickable(face.gameObject, () => { CloseMini(); _infoPanel?.SetData(units[index]); });
            var name = Text("Name", (RectTransform)face.transform, _bodyFont, 22f, Color.white, TextAlignmentOptions.Center);
            Anchor(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(160f, 30f));
            name.text = units[i].unitName;
        }
    }

    private RectTransform Mini(float height)
    {
        _miniPopup = Rect("MiniPopup", _canvas);
        Stretch(_miniPopup);
        var dim = Box("Dim", _miniPopup, null, new Color(0f, 0f, 0f, 0.35f));
        Stretch(dim.rectTransform);
        Clickable(dim.gameObject, CloseMini);
        var box = Box("Box", _miniPopup, _rounded, PanelFill);
        Anchor(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, PanelY), new Vector2(760f, height));
        AddOutline(box, Ink);
        Clickable(box.gameObject, CloseMini);
        box.rectTransform.localScale = Vector3.one * 0.85f;
        box.rectTransform.DOScale(1f, 0.22f).SetEase(Ease.OutBack, 2f).SetUpdate(true).SetLink(box.gameObject);
        return box.rectTransform;
    }

    private void CloseMini()
    {
        if (_miniPopup != null) Destroy(_miniPopup.gameObject);
        _miniPopup = null;
    }

    // ── 캡처 ─────────────────────────────────────────────────────────

    private IEnumerator CaptureRoutine(string folder)
    {
        Directory.CreateDirectory(folder);
        for (int s = 0; s < Labels.Length; s++)
        for (int n = 0; n < SampleLabels.Length; n++)
        {
            _style = s;
            _sample = n;
            Open();
            yield return new WaitForSecondsRealtime(1.3f);
            yield return new WaitForEndOfFrame();
            Save(Path.Combine(folder, $"{"ABC"[s]}_{n}_{Labels[s].Split(' ')[1]}_{SampleLabels[n]}.png"));
        }

        // 채움 연출 중간 프레임 (24장)
        _style = 0; _sample = 2;
        Open();
        yield return new WaitForSecondsRealtime(_fillDelay + _fillMaxSeconds * 0.45f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "A_fill_mid.png"));

        // 용어: 영상 용어(해킹) / 글자 용어(식량)
        _sample = 1;
        Open();
        yield return new WaitForSecondsRealtime(1.2f);
        if (_byId.TryGetValue(9107, out var hacking)) { _selected = hacking; ShowDetail(hacking, true); }
        yield return new WaitForSecondsRealtime(0.4f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "T_0_links.png"));
        ShowTerm("hack_stack");
        yield return new WaitForSecondsRealtime(1.6f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "T_1_video_hack.png"));
        _termPopup?.HideImmediately();
        if (_byId.TryGetValue(9106, out var food)) { _selected = food; ShowDetail(food, true); }
        yield return new WaitForSecondsRealtime(0.4f);
        ShowTerm("food");
        yield return new WaitForSecondsRealtime(0.8f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "T_2_text_food.png"));
        _termPopup?.HideImmediately();

        // 정보창 연결 확인용: 델탕 정보창
        Open();
        yield return new WaitForSecondsRealtime(1.2f);
        OpenTargetInfo(SelectionFeedbackTarget.Deltan, 0);
        yield return new WaitForSecondsRealtime(0.8f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "Z_info_deltan.png"));
        Debug.Log($"[SelectionInventoryLab] 캡처 완료: {folder}");
    }

    private static void Save(string path)
    {
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, shot.EncodeToPNG());
        Destroy(shot);
    }

    // ── UI 도우미 ────────────────────────────────────────────────────

    private RectTransform Row(string name, float y)
    {
        var row = Rect(name, _canvas);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-24f, 62f);
        row.anchoredPosition = new Vector2(0f, y);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        return row;
    }

    private Image ControlButton(RectTransform row, string label, Action onClick)
    {
        var bg = Box(label, row, _rounded, ButtonIdle);
        var text = Text("Label", bg.rectTransform, _bodyFont, 22f, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.text = label;
        Clickable(bg.gameObject, onClick);
        return bg;
    }

    private Image Face(RectTransform parent, SelectionFeedbackTarget target, float size)
    {
        var sprite = TargetFace(target);
        return sprite == null ? null : FaceImage(parent, sprite, size);
    }

    // 흰 테두리 원 안에 캐릭터 상반신
    private Image FaceImage(RectTransform parent, Sprite sprite, float size)
    {
        var ring = Box("Face", parent, _circle, Color.white);
        ring.rectTransform.sizeDelta = new Vector2(size, size);
        var inner = Box("Inner", ring.rectTransform, _circle, new Color32(0x3A, 0x37, 0x86, 0xFF));
        Anchor(inner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - 6f, size - 6f));
        inner.raycastTarget = false;
        inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        if (sprite != null)
        {
            var art = Box("Art", inner.rectTransform, sprite, Color.white);
            Anchor(art.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -size * 0.12f), new Vector2(size * 1.25f, size * 1.25f));
            art.preserveAspect = true;
            art.raycastTarget = false;
        }
        return ring;
    }

    private RectTransform ScrollArea(RectTransform well)
    {
        var view = Rect("Viewport", well);
        Stretch(view);
        view.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", view);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = well.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = view;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;
        return content;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image Box(string name, Transform parent, Sprite sprite, Color color)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    private TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static Graphic AddOutline(Image image, Color color)
    {
        // 테두리는 같은 모양의 뒤판을 살짝 크게 깔아 표현 (등급색·선택 강조를 바꾸기 쉽게)
        var back = Box("Border", image.rectTransform, image.sprite, color);
        back.rectTransform.anchorMin = Vector2.zero;
        back.rectTransform.anchorMax = Vector2.one;
        back.rectTransform.offsetMin = new Vector2(-Outline, -Outline);
        back.rectTransform.offsetMax = new Vector2(Outline, Outline);
        back.raycastTarget = false;
        back.transform.SetAsFirstSibling();
        // 뒤판이 자식이라 부모보다 앞에 그려지므로, 원래 채움을 자식으로 한 장 더 깐다
        var fill = Box("Fill", image.rectTransform, image.sprite, image.color);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        fill.raycastTarget = false;
        fill.transform.SetSiblingIndex(1);
        image.color = new Color(0f, 0f, 0f, 0f);
        return back;
    }

    private static void Clickable(GameObject go, Action onClick)
    {
        var graphic = go.GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = true;
        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (onClick != null) button.onClick.AddListener(() => onClick());
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Fixed(GameObject go, float height, float width = -1f)
    {
        var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        if (width > 0f) { element.minWidth = width; element.preferredWidth = width; }
    }
}
