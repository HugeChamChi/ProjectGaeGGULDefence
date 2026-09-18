using System;
using VContainer;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
public class LevelUpUI : InGameSingleton<LevelUpUI>
{
    [Inject] private LevelUpManager _levelUpManager;
    [Inject] private TimerController _timerManager;
    [Inject] private GridManager _gridManager;
    [Inject] private GameManager _gameManager;

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

    protected override void Awake()
    {
        // base.Awake(); // Removed to prevent double call
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
            card.Setup(data, OnCardClicked, _levelUpManager.GetChoiceDescription(data));
            _spawnedCards.Add(card);
        }

        obj.SetActive(true);
        gameObject.SetActive(true);

        FreezeLayoutAsync(layout).Forget();

        Time.timeScale = 0f;
        _timerManager.StopTimer();

        foreach (var cell in _gridManager.GetOccupiedCells())
            cell.OccupyingUnit?.PauseLoops();

        RunSelectionTimer().Forget();
    }

    private async UniTaskVoid FreezeLayoutAsync(LayoutGroup layout)
    {
        if (layout == null) return;
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        if (layout != null) layout.enabled = false;
    }

    // ── 카드 클릭 ──────────────────────────────────────────────

    private void OnCardClicked(LevelUpCardUI clicked)
    {
        if (!_spawnedCards.Contains(clicked)) return;
        if (_selectedCard == clicked) return;
        _selectedCard?.Deselect();
        _selectedCard = clicked;
        _selectedCard.Select();

        OnConfirmClicked();
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
            Show();
            return;
        }

        Hide();
    }

    // ── 선택 타이머 ────────────────────────────────────────────

    private async UniTaskVoid RunSelectionTimer()
    {
        StopSelectionTimer();
        _selectionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var token = _selectionCts.Token;

        try
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
        catch (OperationCanceledException) { }
    }

    private void StopSelectionTimer()
    {
        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
        _selectionCts = null;
    }

    protected override void OnDestroy()
    {
        StopSelectionTimer();
        base.OnDestroy();
    }

    // ── 닫기 ───────────────────────────────────────────────────

    private void Hide()
    {
        StopSelectionTimer();
        ClearCards();
        obj.SetActive(false);
        gameObject.SetActive(false);
        Time.timeScale = 1f;
        _timerManager.ResumeTimer();

        // 연쇄 레벨업 여부를 먼저 확인 — FlushPendingLevelUp이 새 Show()를 열 수 있음
        _gameManager.OnLevelUpChoiceMade();

        // 새 레벨업 패널이 열리지 않았을 때만 루프 재개
        if (_gameManager.CurrentState != GameManager.GameState.LevelUp)
        {
            foreach (var cell in _gridManager.GetOccupiedCells())
                cell.OccupyingUnit?.ResumeLoops();
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
