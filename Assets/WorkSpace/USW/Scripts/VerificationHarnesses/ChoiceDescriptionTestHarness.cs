using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>Standalone visual lab using production card input, snapshots, formatter, preference and modal.</summary>
public sealed class ChoiceDescriptionTestHarness : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private LevelUpCardUI cardPrefab;
    [SerializeField] private LevelUpData[] choices;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_Text timer;
    [SerializeField] private TMP_Text field;
    [SerializeField] private Button reset;
    [SerializeField] private Toggle previewOnly;
    [SerializeField] private LevelUpManager manager;
    [SerializeField] private Material textMaterial;
    private readonly Dictionary<LevelUpCardUI, DescriptionSnapshot> _snapshots = new();
    private DescriptionDisplaySettings _settings;
    private IDescriptionTermResolver _resolver;
    private IDescriptionPopup _popup;
    private Toggle _toggle;
    private UI_Peekthrough _peek;
    private bool _selected;
    private int _choiceOffset;
    private bool _ready;
    private CancellationTokenSource _presentation;
    private LevelUpRevealSequence _reveal;
    private LevelUpSelectSequence _select;
    public float Remaining { get; private set; }
    public bool BlocksInput => _popup?.BlocksOwnerInput == true;
    public int SelectionCount { get; private set; }
    public int TermOpenCount { get; private set; }
    public bool IsReady => _ready;
    public IReadOnlyDictionary<LevelUpCardUI, DescriptionSnapshot> Snapshots => _snapshots;

    private void Start()
    {
        _settings = new DescriptionDisplaySettings();
        _reveal = GetComponent<LevelUpRevealSequence>();
        _select = GetComponent<LevelUpSelectSequence>();
        var debuffs = Resources.Load<DebuffSettings>("DebuffSettings");
        _resolver = new DescriptionTermResolver(Resources.Load<DescriptionTermCatalog>("DescriptionTermCatalog"),
            new DebuffInfoPresenter(debuffs, new DebuffCatalog(debuffs)));
        _popup = DebuffInfoPopup.Create(this, panel, status);
        _toggle = DescriptionToggleView.Create(panel.transform, status, value =>
        {
            if (!_selected && !BlocksInput && (_peek == null || !_peek.BlocksSelection) && !previewOnly.isOn && !_snapshots.Keys.Any(c => c.HasActivePress)) _settings.SetDetailed(value);
            else _toggle.SetIsOnWithoutNotify(_settings.Detailed);
        });
        _settings.Changed += RefreshDescriptions;
        _peek = panel.GetComponentInChildren<UI_Peekthrough>(true);
        reset.onClick.AddListener(() =>
        {
            _choiceOffset = (_choiceOffset + 3) % choices.Length;
            ShowChoices();
        });
        previewOnly.onValueChanged.AddListener(value =>
        {
            foreach (var card in _snapshots.Keys) card.SetTutorialPreviewOnly(value);
            status.text = value ? "튜토리얼 미리보기 전용: 용어 위에서도 길게 누를 수 있습니다. 짧은 탭으로 선택되지 않습니다." : "용어를 짧게 눌러 설명을 열거나 카드 일반 영역을 길게 눌러 필드를 확인하세요.";
        });
        EnsureCardPrefab();
        ShowChoices();
    }

    private void EnsureCardPrefab()
    {
        if (cardPrefab != null) return;
#if UNITY_EDITOR
        var loadedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/USW/Prefabs/UI/LevelupSelectCardUi.prefab");
        if (loadedPrefab != null) cardPrefab = loadedPrefab.GetComponent<LevelUpCardUI>();
#endif
    }

    public void ShowChoices()
    {
        if (_settings == null) return;
        _presentation?.Cancel();
        _presentation?.Dispose();
        _presentation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _ready = false;
        panel.SetActive(true);
        var layout = cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = true;
        EnsureCardPrefab();
        if (cardPrefab == null)
        {
            Debug.LogError("[ChoiceDescriptionTestHarness] cardPrefab is missing.");
            return;
        }
        if (_popup is UnityEngine.Object popupObj && popupObj != null) _popup.HideImmediately();
        foreach (var card in _snapshots.Keys) if (card != null) Destroy(card.gameObject);
        _snapshots.Clear();
        _selected = false;
        Remaining = 30f;
        for (int i = 0; i < Mathf.Min(3, choices.Length); i++)
        {
            var data = choices[(_choiceOffset + i) % choices.Length];
            if (data == null) continue;
            var card = Instantiate(cardPrefab, cardContainer);
            var snapshot = new ChoiceDescriptionAdapter(data, manager).Capture();
            _snapshots.Add(card, snapshot);
            card.Setup(data, SelectCard);
            card.ConfigurePeek(_peek);
            card.ConfigureDescription(OpenTerm, () => !_ready || _selected || BlocksInput || _snapshots.Keys.Any(other => other != card && other != null && other.HasActivePress));
            card.SetTutorialPreviewOnly(previewOnly.isOn);
            card.OnPeekChanged += (_, peeking) => field.text = peeking
                ? "필드 미리보기 중\n실제 카드의 0.4초 hold · release 복귀 보호막"
                : "필드 영역\n카드의 용어가 아닌 영역을 길게 누르면 이 영역이 보입니다.";
        }
        RefreshDescriptions();
        RevealAsync(_presentation.Token).Forget();
        status.text = $"기존 선택지 {choices.Length}개 중 3장 표시 · 다음 선택지 버튼으로 넘겨보세요.\n상세/간단을 바꿔도 카드와 확정 랜덤 수치는 유지됩니다.";
    }

    private async UniTask RevealAsync(CancellationToken token)
    {
        var cards = _snapshots.Keys.ToList();
        var group = panel.GetComponent<CanvasGroup>();
        foreach (var card in cards) card.AllowSelection = false;
        _reveal.Prepare(cards, group, true);
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        var layout = cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        await _reveal.PlayAsync(cards, group, true, token);
        foreach (var card in cards) card.AllowSelection = true;
        _ready = true;
    }

    private void RefreshDescriptions()
    {
        _toggle.SetIsOnWithoutNotify(_settings.Detailed);
        foreach (var pair in _snapshots)
            pair.Key.SetDescription(DescriptionFormatter.Format(pair.Value, _settings.Detailed, _resolver, true));
    }
    private void OpenTerm(string id)
    {
        if (!_ready || BlocksInput || _selected || !_resolver.TryResolve(id, out var term)) return;
        foreach (var card in _snapshots.Keys) if (card != null) card.CancelDescriptionPress();
        TermOpenCount++;
        _popup.Show(term.DisplayName, term.Body);
    }
    private void SelectCard(LevelUpCardUI card)
    {
        if (!_ready || _selected || BlocksInput || previewOnly.isOn) return;
        _selected = true;
        SelectionCount++;
        card.Select();
        SelectAsync(card, _presentation.Token).Forget();
        status.text = "선택 기록 (항상 상세)\n" + card.GetData().chooseName + "\n" +
            DescriptionFormatter.Format(_snapshots[card], true, _resolver, false) + "\n다시 표시 버튼으로 계속 테스트하세요.";
    }
    private async UniTask SelectAsync(LevelUpCardUI card, CancellationToken token)
    {
        _ready = false;
        foreach (var item in _snapshots.Keys) item.AllowSelection = false;
        await _select.PlayAsync(card, _snapshots.Keys.ToList(), panel.GetComponent<CanvasGroup>(), true, token);
        panel.SetActive(false);
    }
    private void Update()
    {
        if (_settings == null) return;
        // Same frame-boundary integration as the production selection timer; no combat pause is requested.
        if (_ready && !_selected && !BlocksInput) Remaining = Mathf.Max(0, Remaining - Time.unscaledDeltaTime);
        timer.text = _selected ? "선택 완료" : $"선택 남은 시간 {Remaining:0.0}초" + (BlocksInput ? " · 설명 읽는 동안 정지" : "");
        if (!_selected && Remaining <= 0 && !BlocksInput && !previewOnly.isOn && _snapshots.Count > 0) SelectCard(_snapshots.Keys.First());
        _toggle.interactable = _ready && !_selected && !BlocksInput && !_peek.BlocksSelection && !previewOnly.isOn && !_snapshots.Keys.Any(c => c != null && c.HasActivePress);
        reset.interactable = (_ready || _selected && !panel.activeSelf) && !BlocksInput && !_peek.BlocksSelection;
        previewOnly.interactable = !BlocksInput && !_peek.BlocksSelection && !_snapshots.Keys.Any(c => c != null && c.HasActivePress);
    }
    private void OnDisable()
    {
        if (_popup is UnityEngine.Object obj && obj == null) return;
        _popup?.HideImmediately();
    }
    private void OnDestroy()
    {
        _presentation?.Cancel();
        _presentation?.Dispose();
        if (_settings != null) _settings.Changed -= RefreshDescriptions;
        if (_popup is DebuffInfoPopup popup && popup != null) Destroy(popup.gameObject);
    }
}
