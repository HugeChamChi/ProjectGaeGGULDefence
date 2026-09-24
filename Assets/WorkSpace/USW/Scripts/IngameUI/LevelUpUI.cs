using System;
using VContainer;
using System.Collections.Generic;
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
    [Inject] private IObjectResolver _resolver; // DroneManager는 드론 씬에서만 등록되므로 선택적으로 조회

    [SerializeField] private GameObject    obj;
    [SerializeField] private Transform     cardContainer;
    [SerializeField] private LevelUpCardUI cardPrefab;

    [Header("Selection Timer")]
    [SerializeField] private TMP_Text selectionTimerText;
    [SerializeField] private float    selectionSeconds = 30f;

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
        StopSelectionTimer();
        var layout = cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = true;

        ClearCards();
        _selectedCard = null;

        var choices = _levelUpManager.GetRandomChoices(ChoiceCount);
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
            card.Setup(data, OnCardClicked, _levelUpManager.GetChoiceDescription(data));
            if (_peek != null) card.OnPeekChanged += OnCardPeekChanged;
            _spawnedCards.Add(card);
        }

        bool fromGauge = !_isRerolling;
        _isRerolling = false;
        if (_reveal != null) _reveal.Prepare(_spawnedCards, _panelGroup, fromGauge);

        obj.SetActive(true);
        gameObject.SetActive(true);

        _timerManager.StopTimer();

        // 게이지 레벨업이면 슬로우모션으로 서서히 멈춘 뒤 유닛을 정지(대기 모션 유지)시킨다.
        if (_time != null && fromGauge)
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

    /// <summary>유닛 전투 루프 정지 + 대기 모션 유지 (얼어붙은 자세 방지).</summary>
    private void PauseField()
    {
        foreach (var cell in _gridManager.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit == null) continue;
            unit.PauseLoops();
            unit.SetPauseIdle(true);
        }
        SetDronesPauseIdle(true);
    }

    private void SetDronesPauseIdle(bool on)
    {
        if (_resolver == null || !_resolver.TryResolve<DroneManager>(out var drones) || drones == null) return;
        foreach (var drone in drones.Drones)
            if (drone != null) drone.SetPauseIdle(on);
    }

    /// <summary>레이아웃 확정 → 등장 연출 → 입력 허용 → 선택 타이머 순으로 진행한다.</summary>
    private async UniTaskVoid OpenAsync(LayoutGroup layout, bool fromGauge)
    {
        StopSelectionTimer();
        _selectionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var token = _selectionCts.Token;
        var cards = new List<LevelUpCardUI>(_spawnedCards);
        SetCardsInteractable(cards, false);
        if (selectionTimerText != null)
            selectionTimerText.text = $"{Mathf.CeilToInt(selectionSeconds)}";

        try
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            if (layout != null) layout.enabled = false;

            if (_reveal != null)
                await _reveal.PlayAsync(cards, _panelGroup, fromGauge, token);

            SetCardsInteractable(cards, true);
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
        float remaining = selectionSeconds;
        while (remaining > 0f)
        {
            if (selectionTimerText != null)
                selectionTimerText.text = $"{Mathf.CeilToInt(remaining)}";

            await UniTask.Delay(100, DelayType.Realtime, cancellationToken: token);
            remaining -= 0.1f;
        }

        // 시간 초과 — 첫 번째 카드 자동 선택 및 확인
        if (_spawnedCards.Count > 0)
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

    private void OnDestroy()
    {
        StopSelectionTimer();
    }

    // ── 닫기 ───────────────────────────────────────────────────

    private void Hide()
    {
        StopSelectionTimer();
        if (_peek != null) _peek.Clear();
        if (_panelGroup != null) { _panelGroup.DOKill(); _panelGroup.alpha = 1f; }
        ClearCards();
        obj.SetActive(false);
        gameObject.SetActive(false);
        if (_time != null) _time.SpeedUpTime(); // 정지 → 1배속으로 서서히 재개
        else _timeScale.Release(this);
        _timerManager.ResumeTimer();

        // 연쇄 레벨업 여부를 먼저 확인 — FlushPendingLevelUp이 새 Show()를 열 수 있음
        _gameManager.OnLevelUpChoiceMade();

        // 새 레벨업 패널이 열리지 않았을 때만 루프 재개
        if (_gameManager.CurrentState != GameManager.GameState.LevelUp)
        {
            foreach (var cell in _gridManager.GetOccupiedCells())
            {
                var unit = cell.OccupyingUnit;
                if (unit == null) continue;
                unit.SetPauseIdle(false);
                unit.ResumeLoops();
            }
            SetDronesPauseIdle(false);
        }
    }

    // ── 유틸 ───────────────────────────────────────────────────

    private void ClearCards()
    {
        foreach (var card in _spawnedCards)
            if (card != null) Destroy(card.gameObject);

        _spawnedCards.Clear();
        _selectedCard = null;
    }
}
