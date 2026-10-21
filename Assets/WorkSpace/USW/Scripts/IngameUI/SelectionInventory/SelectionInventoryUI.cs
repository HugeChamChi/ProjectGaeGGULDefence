using System;
using System.Collections.Generic;
using DG.Tweening;
using GaeGGUL.UI.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// ≡ 메뉴의 "선택지"로 여는 획득 선택지 보관함 (FxLab_SelectionInventory A안을 프리팹화).
/// 4열 격자를 왼쪽 위부터 채우고, 칸 모서리 얼굴로 대상 유닛을 보여 준다. 칸을 누르면 아래 상세에 확정 설명이 나오고,
/// 설명 속 용어는 레벨업 선택지와 같은 TermLinker/TermLinkTapTarget/TermPopup으로 눌러 볼 수 있다(영상 용어는 필름 재생).
/// 대상 얼굴 칩을 누르면 씬의 유닛 정보창, 족장은 간이 카드를 띄운다. 열려 있는 동안 TimeScaleService로 게임을 멈춘다.
/// </summary>
public sealed class SelectionInventoryUI : MonoBehaviour
{
    /// <summary>적용 대상별 얼굴·이름·정보창 유닛. 족장은 비워 두면 선택된 족장을 쓴다.</summary>
    [Serializable]
    public sealed class TargetFace
    {
        public SelectionFeedbackTarget Target;
        public string Label;
        [Tooltip("모서리 얼굴. 비우면 Units[0]의 아이콘")]
        public Sprite Face;
        [Tooltip("상세 칩으로 보여 줄 유닛 (누르면 정보창)")]
        public UnitData[] Units = Array.Empty<UnitData>();
    }

    [Header("연결")]
    [Tooltip("같은 프리팹/씬의 ≡ 메뉴. 선택지 버튼을 누르면 열린다")]
    [SerializeField] private IngameHudMenu _menu;
    [Tooltip("비우면 처음 쓸 때 씬에서 찾는다")]
    [SerializeField] private UI_UnitInfoPanel _infoPanel;
    [SerializeField] private TermGlossary _glossary;
    [SerializeField] private Shader _blurShader;
    [SerializeField] private TargetFace[] _targets = Array.Empty<TargetFace>();
    [Tooltip("ChieftainSelection이 없을 때(실험실) 쓰는 족장")]
    [SerializeField] private ChieftainData _fallbackChief;

    [Header("창")]
    [SerializeField] private GameObject _window;
    [SerializeField] private CanvasGroup _windowGroup;
    [SerializeField] private Button _dimButton;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _tally;
    [SerializeField] private Button _closeButton;

    [Header("보관함")]
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private RectTransform _gridContent;
    [SerializeField] private SelectionInventorySlot _slotPrefab;
    [SerializeField, Min(1)] private int _minSlots = 16;
    [SerializeField, Min(1)] private int _columns = 4;

    [Header("상세")]
    [SerializeField] private TMP_Text _detailHint;
    [SerializeField] private RectTransform _detailContent;
    [SerializeField] private CanvasGroup _detailGroup;
    [SerializeField] private Image _detailBorder;
    [SerializeField] private Image _detailFill;
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TMP_Text _detailName;
    [SerializeField] private TMP_Text _detailDescription;
    [SerializeField] private RectTransform _chipRow;
    [SerializeField] private Button _chipTemplate;
    [SerializeField] private TMP_Text _commonLabel;

    [Header("족장 카드")]
    [SerializeField] private GameObject _chiefCard;
    [SerializeField] private Button _chiefDismiss;
    [SerializeField] private SelectionInventoryFace _chiefFace;
    [SerializeField] private TMP_Text _chiefText;

    [Header("용어 층 (정보창 위)")]
    [SerializeField] private RectTransform _termLayer;
    [SerializeField] private TMP_Text _termFontSource;

    [Header("색")]
    [SerializeField] private Color _rareText = new Color32(0xC0, 0xA0, 0xF9, 0xFF);
    [SerializeField] private Color _epicText = new Color32(0xF2, 0xB5, 0x33, 0xFF);
    [SerializeField] private Color _legendText = new Color32(0xFF, 0x6A, 0x5E, 0xFF);
    [SerializeField] private Color _rareFill = new Color32(0x47, 0x33, 0x4C, 0xFF);
    [SerializeField] private Color _epicFill = new Color32(0x7A, 0x4F, 0x22, 0xFF);
    [SerializeField] private Color _legendFill = new Color32(0x88, 0x38, 0x38, 0xFF);
    [SerializeField] private Color _highlight = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
    [Tooltip("레벨업 선택지와 같은 계열, 어두운 바탕용으로 밝힘")]
    [SerializeField] private Color _termColor = new Color(0.36f, 0.75f, 0.95f);
    [SerializeField] private Color _videoTermColor = new Color(1f, 0.62f, 0.1f);

    [Header("연출")]
    [SerializeField, Min(0.01f)] private float _openSeconds = 0.28f;
    [SerializeField, Min(0.01f)] private float _closeSeconds = 0.16f;
    [Tooltip("첫 칸이 나오기 시작하는 시점")]
    [SerializeField, Min(0f)] private float _fillDelay = 0.14f;
    [Tooltip("칸마다 간격. 칸이 많으면 전체가 Fill Max Seconds 안에 끝나도록 줄인다")]
    [SerializeField, Min(0f)] private float _fillStagger = 0.045f;
    [SerializeField, Min(0.05f)] private float _fillMaxSeconds = 0.6f;
    [SerializeField, Min(0.01f)] private float _popSeconds = 0.3f;

    [Inject] private LevelUpManager _levelUp;
    [Inject] private TimeScaleService _timeScale;
    [Inject] private DescriptionDisplaySettings _descriptionSettings;
    [Inject] private ChieftainSelection _chieftainSelection;

    private readonly List<SelectionInventorySlot> _slots = new();
    private readonly List<Button> _chips = new();
    private readonly DescriptionModeState _termMode = new();
    private TermLinker _linker;
    private TermPopup _termPopup;
    private TermLinkTapTarget _descriptionTap;
    private SelectionInventoryItem _selected;
    private bool _initialized;
    private bool _paused;

    /// <summary>창이 열려 있는지.</summary>
    public bool IsOpen => _window != null && _window.activeSelf;
    /// <summary>현재 상세에 표시 중인 칸 (검증용).</summary>
    public SelectionInventoryItem Selected => _selected;
    /// <summary>용어 시트 (검증용).</summary>
    public TermPopup TermPopup => _termPopup;
    /// <summary>보관함 창 영역 (튜토리얼 안내 위치).</summary>
    public RectTransform PanelArea => _panel;
    /// <summary>현재 표시 중인 칸 수 (검증용, 빈칸 제외).</summary>
    public int ItemCount { get { int n = 0; foreach (var s in _slots) if (s.gameObject.activeSelf && s.Item != null) n++; return n; } }

    private void Start()
    {
        EnsureInitialized();
        if (_menu != null) _menu.SelectionListRequested.AddListener(Open);
    }

    private void OnDestroy()
    {
        if (_menu != null) _menu.SelectionListRequested.RemoveListener(Open);
        if (_descriptionSettings != null) _descriptionSettings.Changed -= RefreshDescriptionMode;
        _termMode.OnChanged -= ChangeDescriptionMode;
        ReleasePause();
    }

    private void OnDisable()
    {
        if (_window != null) DOTween.Kill(_window);
        _termPopup?.HideImmediately();
        if (_window != null) _window.SetActive(false);
        ReleasePause();
    }

    /// <summary>이번 판에 획득한 선택지를 획득 순서대로 모아 연다.</summary>
    public void Open() => Show(CollectAcquired());

    /// <summary>주어진 칸들로 연다 (실험실·검사용). 마지막 칸이 처음 선택된다.</summary>
    public void Show(IReadOnlyList<SelectionInventoryItem> items)
    {
        EnsureInitialized();
        _termPopup?.HideImmediately();
        _chiefCard.SetActive(false);
        DOTween.Kill(_window);
        _window.SetActive(true);
        if (!_paused && _timeScale != null) { _timeScale.Pause(this); _paused = true; }

        BuildSlots(items);
        _tally.text = Tally(items);
        _selected = items.Count > 0 ? items[items.Count - 1] : null;
        RefreshSelection(false);
        ShowDetail(_selected, false);
        _scroll.verticalNormalizedPosition = 1f;
        PlayOpen(items.Count);
    }

    /// <summary>닫는다. 용어 시트·족장 카드도 함께 닫힌다.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        _termPopup?.HideImmediately();
        _chiefCard.SetActive(false);
        DOTween.Kill(_window);
        _windowGroup.DOFade(0f, _closeSeconds).SetUpdate(true).SetLink(_window).SetTarget(_window);
        _panel.DOScale(0.94f, _closeSeconds).SetEase(Ease.InCubic).SetUpdate(true).SetLink(_window).SetTarget(_window)
            .OnComplete(() => _window.SetActive(false));
        ReleasePause();
    }

    // ── 준비 ─────────────────────────────────────────────────────────

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        _descriptionSettings ??= new DescriptionDisplaySettings();
        _linker = _glossary != null ? new TermLinker(_glossary, _termColor, _videoTermColor) : null;
        _dimButton.onClick.AddListener(Close);
        _closeButton.onClick.AddListener(Close);
        _chiefDismiss.onClick.AddListener(() => _chiefCard.SetActive(false));
        _chipTemplate.gameObject.SetActive(false);
        _chiefCard.SetActive(false);
        _window.SetActive(false);

        _descriptionTap = _detailDescription.GetComponent<TermLinkTapTarget>();
        if (_descriptionTap == null) _descriptionTap = _detailDescription.gameObject.AddComponent<TermLinkTapTarget>();
        _descriptionTap.Configure(_detailContent.gameObject, OnTermTapped, 18f);

        if (_glossary != null && _termLayer != null && _termFontSource != null)
        {
            _termPopup = TermPopup.Create(_termLayer, _termFontSource, DescriptionToggleView.TrackSprite, _blurShader);
            _termMode.Set(_descriptionSettings.Detailed ? DescriptionMode.Detailed : DescriptionMode.Brief);
            _termPopup.ConfigureDescriptionMode(_termMode);
            _termMode.OnChanged += ChangeDescriptionMode;
            _descriptionSettings.Changed += RefreshDescriptionMode;
            _termPopup.SiblingTapped += ShowTerm;
        }
    }

    private List<SelectionInventoryItem> CollectAcquired()
    {
        var items = new List<SelectionInventoryItem>();
        if (_levelUp == null) return items;
        var cards = new Dictionary<int, LevelUpData>();
        foreach (var card in _levelUp.LevelUpPool) if (card != null) cards[card.chooseId] = card;
        foreach (int id in _levelUp.ChosenIds)
            if (cards.TryGetValue(id, out var card))
                items.Add(new SelectionInventoryItem(card, _levelUp.GetChoiceDescriptionSnapshot(card)));
        return items;
    }

    // ── 보관함 ───────────────────────────────────────────────────────

    private void BuildSlots(IReadOnlyList<SelectionInventoryItem> items)
    {
        int total = Mathf.Max(_minSlots, Mathf.CeilToInt(items.Count / (float)_columns) * _columns);
        while (_slots.Count < total) _slots.Add(Instantiate(_slotPrefab, _gridContent));
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            slot.gameObject.SetActive(i < total);
            if (i >= total) continue;
            if (i < items.Count)
            {
                var item = items[i];
                slot.Bind(item, TierText(item.Card.tier), TierFill(item.Card.tier), FaceOf(item.Target), OnSlotClicked);
            }
            else slot.BindEmpty();
        }
    }

    private void OnSlotClicked(SelectionInventorySlot slot)
    {
        bool changed = _selected != slot.Item;
        _selected = slot.Item;
        RefreshSelection(true);
        if (changed) ShowDetail(_selected, true);
    }

    private void RefreshSelection(bool punch)
    {
        foreach (var slot in _slots)
            if (slot.gameObject.activeSelf) slot.SetSelected(slot.Item != null && slot.Item == _selected, _highlight, punch);
    }

    private string Tally(IReadOnlyList<SelectionInventoryItem> items)
    {
        int rare = 0, epic = 0, legend = 0;
        foreach (var item in items)
        {
            if (item.Card.tier == Tier.Legend) legend++;
            else if (item.Card.tier == Tier.Epic) epic++;
            else rare++;
        }
        return $"<color=#{Hex(_rareText)}>레어 {rare}</color>  <color=#{Hex(_epicText)}>에픽 {epic}</color>  <color=#{Hex(_legendText)}>전설 {legend}</color>";
    }

    // ── 상세 ─────────────────────────────────────────────────────────

    private void ShowDetail(SelectionInventoryItem item, bool animate)
    {
        _termPopup?.HideImmediately();
        _chiefCard.SetActive(false);
        _detailHint.gameObject.SetActive(item == null);
        _detailContent.gameObject.SetActive(item != null);
        if (item == null) return;

        var tier = item.Card.tier;
        _detailBorder.color = TierText(tier);
        _detailFill.color = TierFill(tier);
        _detailIcon.sprite = item.Card.icon;
        _detailName.color = TierText(tier);
        _detailName.text = $"{item.Card.chooseName}  <size=60%><color=#B8B5E6>{TierName(tier)}{(item.Instant ? " · 즉시 효과" : "")}</color></size>";
        RefreshDescription();
        BuildChips(item);

        if (!animate) { _detailGroup.alpha = 1f; _detailContent.anchoredPosition = Vector2.zero; return; }
        DOTween.Kill(_detailContent);
        _detailGroup.alpha = 0f;
        _detailGroup.DOFade(1f, 0.18f).SetUpdate(true).SetLink(_detailContent.gameObject).SetTarget(_detailContent);
        _detailContent.anchoredPosition = new Vector2(28f, 0f);
        _detailContent.DOAnchorPosX(0f, 0.24f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(_detailContent.gameObject);
    }

    private void RefreshDescription()
    {
        if (_selected == null) return;
        _detailDescription.text = _selected.FormatDescription(_descriptionSettings.Detailed, _linker);
        _descriptionTap.RefreshHitArea();
    }

    // 적용 대상 칩: 얼굴 + 이름, 누르면 정보창. 공용은 글자만.
    private void BuildChips(SelectionInventoryItem item)
    {
        foreach (var chip in _chips) Destroy(chip.gameObject);
        _chips.Clear();
        var entry = Find(item.Target);
        bool chief = item.Target == SelectionFeedbackTarget.ChiefSkill;
        bool hasUnits = entry != null && entry.Units != null && entry.Units.Length > 0;
        _commonLabel.gameObject.SetActive(!chief && !hasUnits);
        _commonLabel.text = item.Instant ? "획득 즉시 1회 적용" : "모든 유닛";
        if (chief)
        {
            var data = SelectedChief();
            AddChip(data != null ? $"{data.DisplayName}(족장)" : "족장", data != null ? data.Icon : null, ShowChiefCard);
            return;
        }
        if (!hasUnits) return;
        foreach (var unit in entry.Units)
        {
            if (unit == null) continue;
            var target = unit;
            AddChip(unit.unitName, unit.icon, () => OpenUnitInfo(target));
        }
    }

    private void AddChip(string label, Sprite face, Action onClick)
    {
        var chip = Instantiate(_chipTemplate, _chipRow);
        chip.gameObject.SetActive(true);
        chip.GetComponentInChildren<TMP_Text>(true).text = label;
        chip.GetComponentInChildren<SelectionInventoryFace>(true)?.Set(face);
        chip.onClick.AddListener(() =>
        {
            chip.transform.DOComplete();
            chip.transform.DOPunchScale(Vector3.one * 0.08f, 0.26f, 6, 0.6f).SetUpdate(true).SetLink(chip.gameObject);
            onClick();
        });
        _chips.Add(chip);
    }

    private void OpenUnitInfo(UnitData data)
    {
        if (_infoPanel == null) _infoPanel = FindFirstObjectByType<UI_UnitInfoPanel>(FindObjectsInactive.Include);
        _infoPanel?.SetData(data);
    }

    // 족장은 그리드 유닛이 아니라 유닛 정보창 대신 간이 카드로 보여 준다
    private void ShowChiefCard()
    {
        var data = SelectedChief();
        _chiefFace.Set(data != null ? data.Icon : null);
        var skill = data != null ? data.AlphanSkill : null;
        string name = data != null ? data.DisplayName : "족장";
        _chiefText.text = skill != null
            ? $"<size=140%>{name}</size>  <color=#B8B5E6>족장</color>\n기본 쿨타임 {skill.CooldownSeconds:0.#}초\n기본 피해: 참가 드론 공격력 합 ×{skill.RallyAttackCoefficient:0.##}"
            : $"<size=140%>{name}</size>  <color=#B8B5E6>족장</color>";
        _chiefCard.SetActive(true);
        _chiefCard.transform.localScale = Vector3.one * 0.85f;
        _chiefCard.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack, 2f).SetUpdate(true).SetLink(_chiefCard);
    }

    private ChieftainData SelectedChief() => _chieftainSelection != null && _chieftainSelection.SelectedChieftain != null
        ? _chieftainSelection.SelectedChieftain : _fallbackChief;

    // ── 용어 ─────────────────────────────────────────────────────────

    private void OnTermTapped(TermLinkTapTarget source, string linkId)
    {
        if (linkId == null || !linkId.StartsWith(TermLinker.LinkPrefix, StringComparison.Ordinal)) return;
        ShowTerm(linkId.Substring(TermLinker.LinkPrefix.Length));
    }

    /// <summary>현재 상세 카드의 용어 시트를 연다 (검증용으로도 쓴다).</summary>
    public void ShowTerm(string id)
    {
        if (_termPopup == null || _selected == null || !_glossary.TryFind(id, out var entry)) return;
        var siblings = new List<TermEntry>();
        foreach (var termId in _selected.Card.DescriptionTerms)
            if (_glossary.TryFind(termId, out var other)) siblings.Add(other);
        _termPopup.Show(entry, siblings);
    }

    private void ChangeDescriptionMode(DescriptionMode mode) => _descriptionSettings.SetDetailed(mode == DescriptionMode.Detailed);

    private void RefreshDescriptionMode()
    {
        _termMode.Set(_descriptionSettings.Detailed ? DescriptionMode.Detailed : DescriptionMode.Brief);
        RefreshDescription();
    }

    // ── 연출 ─────────────────────────────────────────────────────────

    private void PlayOpen(int count)
    {
        _windowGroup.alpha = 0f;
        _windowGroup.DOFade(1f, _openSeconds * 0.6f).SetUpdate(true).SetLink(_window).SetTarget(_window);
        _panel.localScale = Vector3.one * 0.9f;
        _panel.DOScale(1f, _openSeconds).SetEase(Ease.OutBack, 1.6f).SetUpdate(true).SetLink(_window).SetTarget(_window);

        // 왼쪽 위부터 차례로. 칸이 많으면 간격을 줄여 전체 시간을 묶는다. 빈칸은 함께 옅게 떠오른다.
        float step = count > 1 ? Mathf.Min(_fillStagger, _fillMaxSeconds / (count - 1)) : 0f;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].gameObject.activeSelf) continue;
            float delay = i < count ? _fillDelay + step * i : _fillDelay * 0.5f;
            _slots[i].Appear(delay, _popSeconds);
        }

        int shown = 0;
        _title.text = TitleText(0);
        float seconds = _fillDelay + step * Mathf.Max(0, count - 1) + _popSeconds * 0.5f;
        DOTween.To(() => shown, v => { shown = v; _title.text = TitleText(v); }, count, seconds)
            .SetEase(Ease.Linear).SetUpdate(true).SetLink(_window).SetTarget(_window);
    }

    private string TitleText(int count) => $"획득한 선택지 <color=#{Hex(_highlight)}>{count}</color>";

    private void ReleasePause()
    {
        if (!_paused) return;
        _paused = false;
        _timeScale?.Release(this);
    }

    // ── 도우미 ───────────────────────────────────────────────────────

    private TargetFace Find(SelectionFeedbackTarget target)
    {
        foreach (var entry in _targets) if (entry != null && entry.Target == target) return entry;
        return null;
    }

    private Sprite FaceOf(SelectionFeedbackTarget target)
    {
        if (target == SelectionFeedbackTarget.ChiefSkill) { var chief = SelectedChief(); return chief != null ? chief.Icon : null; }
        var entry = Find(target);
        if (entry == null) return null;
        if (entry.Face != null) return entry.Face;
        return entry.Units != null && entry.Units.Length > 0 && entry.Units[0] != null ? entry.Units[0].icon : null;
    }

    private Color TierText(Tier tier) => tier == Tier.Legend ? _legendText : tier == Tier.Epic ? _epicText : _rareText;
    private Color TierFill(Tier tier) => tier == Tier.Legend ? _legendFill : tier == Tier.Epic ? _epicFill : _rareFill;
    private static string TierName(Tier tier) => tier switch { Tier.Epic => "에픽", Tier.Legend => "전설", _ => "레어" };
    private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
}
