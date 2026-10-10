using System;
using VContainer;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ════════════════════════════════════════════════════════
// LevelUpUI
//
// ─ Scene 구성 ────────────────────────────────────────
//   LevelUpPanel  ← 이 GameObject에 LevelUpUI 컴포넌트 부착
//     ├── CardContainer     ← HorizontalLayoutGroup (Inspector 연결)
//     └── SelectionTimer    ← TMP_Text 카운트다운 표시 (Inspector 연결)
//   카드 클릭 시 즉시 선택+확정되므로 별도 확인 버튼은 없다.
//
// ─ Inspector 연결 ─────────────────────────────────────
//   cardContainer, cardPrefab, selectionTimerText 연결 필요
// ════════════════════════════════════════════════════════
public class LevelUpUI : MonoBehaviour
{
    [Inject] private LevelUpManager _levelUpManager;
    [Inject] private TimerController _timerManager;
    [Inject] private GridManager _gridManager;
    [Inject] private GameManager _gameManager;
    [Inject] private TimeScaleService _timeScale;
    [Inject] private FieldPauseVisuals _fieldPause;
    [Inject] private DescriptionDisplaySettings _descriptionSettings;
    [Inject] private DescriptionTermCatalog _descriptionCatalog;
    [Inject] private DebuffInfoPresenter _descriptionDebuffs;
    private IDescriptionTermResolver _descriptionResolver;
    private IDescriptionPopup _descriptionPopup;
    private Toggle _descriptionToggle;
    private readonly Dictionary<LevelUpCardUI, DescriptionSnapshot> _descriptions = new();
    private bool _descriptionSubscribed;
    public bool DescriptionBlocksInput => _descriptionPopup?.BlocksOwnerInput == true;
    public bool FieldPreviewBlocksInput => obj != null && obj.GetComponentInChildren<UI_Peekthrough>(true)?.BlocksSelection == true;
    public float SelectionTimeRemaining { get; private set; }

    [SerializeField] private GameObject    obj;
    [SerializeField] private Transform     cardContainer;
    [SerializeField] private LevelUpCardUI cardPrefab;

    [Header("Selection Timer")]
    [SerializeField] private TMP_Text selectionTimerText;
    [SerializeField] private float    selectionSeconds = 30f;
    [SerializeField] private bool _disableSelectionTimer;
    [SerializeField] private GaeGGUL.Tutorial.IngameTutorialSettings _tutorialSettings;

    private const int ChoiceCount = 3;

    private readonly List<LevelUpCardUI> _spawnedCards = new();
    private          LevelUpCardUI       _selectedCard;
    private          CancellationTokenSource _selectionCts;

    // 연출 컴포넌트 (IngameUI/LevelUpPresentation) — 모두 선택 사항. 없으면 즉시 표시/즉시 정지.
    private LevelUpRevealSequence _reveal;
    private LevelUpSelectSequence _select;
    private LevelUpPeekHighlighter _peek;
    private LevelUpTimeDirector _time;
    private CanvasGroup _panelGroup;
    private bool _isRerolling;
    private bool _ownsPause;
    private bool _closingNormally;
    /// <summary>True after the choice entrance animation permits selection.</summary>
    public bool IsReadyForSelection { get; private set; }
    /// <summary>Choice area used by scene-owned interaction guides.</summary>
    public RectTransform ChoiceArea => cardContainer as RectTransform;
    /// <summary>Tutorials need one applicable unit card to teach hold-to-preview.</summary>
    public bool RequireUnitPreview { get; set; }

    private void Awake()
    {
        _reveal = GetComponent<LevelUpRevealSequence>();
        _select = GetComponent<LevelUpSelectSequence>();
        _peek   = GetComponent<LevelUpPeekHighlighter>();
        _time   = GetComponent<LevelUpTimeDirector>();
        if (_reveal != null && obj != null)
        {
            _panelGroup = obj.GetComponent<CanvasGroup>();
            if (_panelGroup == null) _panelGroup = obj.AddComponent<CanvasGroup>();
        }
    }

    // ── 열기 ───────────────────────────────────────────────────

    public void Show()
    {
        if (_gameManager?.IsFinished == true) return;
        IsReadyForSelection = false;
        StopSelectionTimer();
        var layout = cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = true;

        ClearCards();
        _selectedCard = null;

        var choices = _tutorialSettings != null
            ? new List<LevelUpData>(_tutorialSettings.LevelUpChoices)
            : _levelUpManager.GetRandomChoices(ChoiceCount);
        if (_tutorialSettings == null && RequireUnitPreview && choices.Count > 0)
        {
            var targets = new List<UnitBase>();
            bool HasUnits(LevelUpData data) => LevelUpFeedbackTargets.Resolve(data, _gridManager, targets) == LevelUpFeedbackDestination.Units;
            if (!choices.Any(HasUnits))
            {
                var preview = _levelUpManager.LevelUpPool.FirstOrDefault(c => c != null && c.spawnRate > 0 &&
                    !_levelUpManager.ChosenIds.Contains(c.chooseId) && HasUnits(c));
                if (preview != null) choices[0] = preview;
            }
        }
        if (choices.Count == 0)
        {
            // 풀 소진/설정 누락 시 빈 패널에서 게임이 정지하지 않도록 선택 단계를 마친다.
            Hide();
            return;
        }
        foreach (var data in choices)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.ConfigurePeek(obj.GetComponentInChildren<UI_Peekthrough>(true));
            EnsureDescriptions(card.DescriptionText);
            var snapshot = new ChoiceDescriptionAdapter(data, _levelUpManager).Capture();
            _descriptions.Add(card, snapshot);
            card.Setup(data, OnCardClicked, DescriptionFormatter.Format(snapshot, _descriptionSettings.Detailed, _descriptionResolver, true));
            card.ConfigureDescription(OpenDescriptionTerm, () => !IsReadyForSelection || DescriptionBlocksInput ||
                _spawnedCards.Any(other => other != null && other != card && other.HasActivePress));
            if (_peek != null) card.OnPeekChanged += OnCardPeekChanged;
            _spawnedCards.Add(card);
        }

        bool fromGauge = !_isRerolling;
        _isRerolling = false;
        if (_reveal != null) _reveal.Prepare(_spawnedCards, _panelGroup, fromGauge);

        obj.SetActive(true);
        gameObject.SetActive(true);

        bool freezeImmediately = _ownsPause || _fieldPause?.MustFreezeImmediately == true;
        _ownsPause = true;
        _fieldPause?.HoldAttacks(this);

        // 게이지 레벨업이면 유예 → 슬로우모션으로 서서히 멈춘 뒤 화면만 살린다(대기 모션·이펙트).
        if (_time != null && fromGauge && !freezeImmediately)
            _time.SlowDownTime(PauseField);
        else
        {
            if (_time != null) _time.PauseNow();
            else _timeScale.Pause(this);
            PauseField();
        }

        OpenAsync(layout, fromGauge).Forget();
    }

    /// <summary>카드를 꾹 눌러 필드보기 중일 때 그 카드의 대상 유닛을 강조한다.</summary>
    private void OnCardPeekChanged(LevelUpCardUI card, bool peeking)
    {
        if (peeking) _peek.Show(card.GetData());
        else _peek.Clear();
    }

    /// <summary>완전히 멈춘 뒤: 화면(대기 모션·드론·보스 대기·이펙트)은 실제 시간으로 유지.</summary>
    private void PauseField() => _fieldPause?.Enter(this);

    /// <summary>레이아웃 확정 → 등장 연출 → 입력 허용 → 선택 타이머 순으로 진행한다.</summary>
    private async UniTaskVoid OpenAsync(LayoutGroup layout, bool fromGauge)
    {
        StopSelectionTimer();
        _selectionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var token = _selectionCts.Token;
        var cards = new List<LevelUpCardUI>(_spawnedCards);
        SetCardsInteractable(cards, false);
        if (selectionTimerText != null)
            selectionTimerText.text = _disableSelectionTimer ? string.Empty : $"{Mathf.CeilToInt(selectionSeconds)}";

        try
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            if (layout != null) layout.enabled = false;

            if (_reveal != null)
                await _reveal.PlayAsync(cards, _panelGroup, fromGauge, token);

            SetCardsInteractable(cards, true);
            IsReadyForSelection = true;
            await RunSelectionTimerAsync(token);
        }
        catch (OperationCanceledException) { }
    }

    private static void SetCardsInteractable(List<LevelUpCardUI> cards, bool interactable)
    {
        foreach (var card in cards)
        {
            if (card == null) continue;
            var group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = interactable;
        }
    }

    // ── 카드 클릭 ──────────────────────────────────────────────

    private void OnCardClicked(LevelUpCardUI clicked)
    {
        if (DescriptionBlocksInput) return;
        IsReadyForSelection = false;
        if (!_spawnedCards.Contains(clicked)) return;
        if (_selectedCard == clicked) return;
        _selectedCard?.Deselect();
        _selectedCard = clicked;
        _selectedCard.Select();

        if (_select != null) ConfirmAfterSelectAsync(clicked).Forget();
        else OnConfirmClicked();
    }

    /// <summary>선택 연출(선택 카드 강조, 나머지 퇴장, 패널 페이드 아웃)을 보여준 뒤 확정한다.</summary>
    private async UniTaskVoid ConfirmAfterSelectAsync(LevelUpCardUI selected)
    {
        StopSelectionTimer();
        _selectionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var token = _selectionCts.Token;
        var cards = new List<LevelUpCardUI>(_spawnedCards);
        SetCardsInteractable(cards, false);

        // 리롤 카드는 곧바로 새 카드가 나오므로 패널을 닫지 않는다.
        bool reroll = selected.GetData()?.specialEffect == LevelUpSpecialEffect.RerollChoices;
        try
        {
            await _select.PlayAsync(selected, cards, reroll ? null : _panelGroup, !reroll, token);
            OnConfirmClicked();
        }
        catch (OperationCanceledException) { }
    }

    // ── 확인 버튼 ──────────────────────────────────────────────

    public void OnConfirmClicked()
    {
        if (_selectedCard == null) return;

        var data = _selectedCard.GetData();
        if (data != null)
            _levelUpManager.ApplyEffect(data);

        if (data != null && data.specialEffect == LevelUpSpecialEffect.RerollChoices)
        {
            _isRerolling = true; // 리롤은 게이지 버스트 없이 카드만 다시 등장한다.
            Show();
            return;
        }

        Hide();
    }

    // ── 선택 타이머 ────────────────────────────────────────────

    private async UniTask RunSelectionTimerAsync(CancellationToken token)
    {
        if (_disableSelectionTimer)
        {
            if (selectionTimerText != null) selectionTimerText.text = string.Empty;
            return;
        }
        SelectionTimeRemaining = selectionSeconds;
        while (SelectionTimeRemaining > 0f || DescriptionBlocksInput)
        {
            if (selectionTimerText != null)
                selectionTimerText.text = $"{Mathf.CeilToInt(SelectionTimeRemaining)}";

            bool blocked = DescriptionBlocksInput;
            float started = Time.unscaledTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            if (!blocked && !DescriptionBlocksInput) SelectionTimeRemaining = Mathf.Max(0, SelectionTimeRemaining - (Time.unscaledTime - started));
        }

        // 시간 초과 — 첫 번째 카드 자동 선택 및 확인
        if (!DescriptionBlocksInput && _spawnedCards.Count > 0)
        {
            OnCardClicked(_spawnedCards[0]);
        }
    }

    private void StopSelectionTimer()
    {
        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
        _selectionCts = null;
    }

    private void OnDisable()
    {
        _descriptionPopup?.HideImmediately();
        if (_closingNormally) return;
        ReleaseForcedPause();
    }

    private void OnDestroy()
    {
        ReleaseForcedPause();
        if (_descriptionSubscribed) _descriptionSettings.Changed -= RefreshDescriptions;
        if (_descriptionPopup is DebuffInfoPopup popup && popup != null) Destroy(popup.gameObject);
    }

    /// <summary>Run-end cleanup invalidates entrance, selection and timers without applying a card.</summary>
    public void CancelPendingSelection()
    {
        // StopRun can be raised by WaveManager.OnDestroy after this UI has been destroyed.
        if (this == null) return;
        ReleaseForcedPause();
        _peek?.Clear();
        ClearCards();
        if (obj != null) obj.SetActive(false);
        gameObject.SetActive(false);
    }

    private void ReleaseForcedPause()
    {
        _descriptionPopup?.HideImmediately();
        StopSelectionTimer();
        IsReadyForSelection = false;
        if (!_ownsPause) return;
        _ownsPause = false;
        _time?.CancelAndRelease();
        _timeScale?.Release(this);
        _fieldPause?.Exit(this, _gameManager?.IsFinished != true);
        if (_gameManager != null && _gameManager.CurrentState == GameManager.GameState.LevelUp)
            _gameManager.OnLevelUpChoiceMade();
    }

    // ── 닫기 ───────────────────────────────────────────────────

    private void Hide()
    {
        IsReadyForSelection = false;
        StopSelectionTimer();
        if (_peek != null) _peek.Clear();
        if (_panelGroup != null) { _panelGroup.DOKill(); _panelGroup.alpha = 1f; }
        ClearCards();
        _closingNormally = true;
        try { obj.SetActive(false); gameObject.SetActive(false); }
        finally { _closingNormally = false; }

        // 연쇄 레벨업 여부를 먼저 확인 — FlushPendingLevelUp이 새 Show()를 열 수 있음
        _gameManager.OnLevelUpChoiceMade();

        // 새 레벨업 패널이 열리지 않았을 때만 루프 재개
        if (_gameManager.CurrentState != GameManager.GameState.LevelUp)
        {
            _ownsPause = false;
            _fieldPause?.Exit(this);
            if (_time != null) _time.SpeedUpTime();
            else _timeScale.Release(this);
        }
    }

    // ── 유틸 ───────────────────────────────────────────────────

    private void ClearCards()
    {
        _descriptionPopup?.HideImmediately();
        _descriptions.Clear();
        foreach (var card in _spawnedCards)
            if (card != null) Destroy(card.gameObject);

        _spawnedCards.Clear();
        _selectedCard = null;
    }

    private void EnsureDescriptions(TMP_Text source)
    {
        if (_descriptionResolver == null) _descriptionResolver = new DescriptionTermResolver(_descriptionCatalog, _descriptionDebuffs);
        if (_descriptionPopup == null) _descriptionPopup = DebuffInfoPopup.Create(this, obj, source);
        if (_descriptionToggle == null) _descriptionToggle = DescriptionToggleView.Create(obj.transform, source, value =>
        {
            if (CanChangeDescriptionMode()) _descriptionSettings.SetDetailed(value);
            else _descriptionToggle.SetIsOnWithoutNotify(_descriptionSettings.Detailed);
        });
        _descriptionToggle.SetIsOnWithoutNotify(_descriptionSettings.Detailed);
        if (!_descriptionSubscribed) { _descriptionSettings.Changed += RefreshDescriptions; _descriptionSubscribed = true; }
    }
    private void RefreshDescriptions()
    {
        if (_descriptionToggle != null) _descriptionToggle.SetIsOnWithoutNotify(_descriptionSettings.Detailed);
        foreach (var pair in _descriptions)
            if (pair.Key != null) pair.Key.SetDescription(DescriptionFormatter.Format(pair.Value, _descriptionSettings.Detailed, _descriptionResolver, true));
    }
    private void OpenDescriptionTerm(string id)
    {
        if (DescriptionBlocksInput || !IsReadyForSelection || !_descriptionResolver.TryResolve(id, out var term)) return;
        foreach (var card in _spawnedCards) if (card != null) card.CancelDescriptionPress();
        _descriptionPopup.Show(term.DisplayName, term.Body);
    }
    private void Update()
    {
        if (obj != null && !obj.activeInHierarchy) _descriptionPopup?.HideImmediately();
        if (_descriptionToggle == null) return;
        _descriptionToggle.interactable = CanChangeDescriptionMode();
    }
    private bool CanChangeDescriptionMode()
    {
        var field = obj.GetComponentInChildren<UI_Peekthrough>(true);
        return IsReadyForSelection && !DescriptionBlocksInput && (field == null || !field.BlocksSelection) &&
            !_spawnedCards.Any(card => card != null && (card.HasActivePress || card.TutorialPreviewOnly));
    }
}
