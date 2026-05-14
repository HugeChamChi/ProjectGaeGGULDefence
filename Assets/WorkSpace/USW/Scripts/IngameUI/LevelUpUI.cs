using System;
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
//     ├── SelectionTimer    ← TMP_Text 카운트다운 표시 (Inspector 연결)
//     └── ConfirmButton     ← Button (Inspector 연결)
//
// ─ Inspector 연결 ─────────────────────────────────────
//   cardContainer, cardPrefab, selectionTimerText 연결 필요
// ════════════════════════════════════════════════════════
public class LevelUpUI : InGameSingleton<LevelUpUI>
{
    [SerializeField] private Transform     cardContainer;
    [SerializeField] private LevelUpCardUI cardPrefab;
    [SerializeField] private Button        confirmButton;

    [Header("Selection Timer")]
    [SerializeField] private TMP_Text selectionTimerText;
    [SerializeField] private float    selectionSeconds = 30f;

    private const int ChoiceCount = 3;

    private readonly List<LevelUpCardUI> _spawnedCards = new();
    private          LevelUpCardUI       _selectedCard;
    private          CancellationTokenSource _selectionCts;

    protected override void Awake()
    {
        base.Awake();

        confirmButton?.onClick.AddListener(OnConfirmClicked);
        SetConfirmInteractable(false);

        gameObject.SetActive(false);
    }

    // ── 열기 ───────────────────────────────────────────────────

    public void Show()
    {
        ClearCards();
        _selectedCard = null;
        SetConfirmInteractable(false);

        var choices = Manager.LevelUp.GetRandomChoices(ChoiceCount);
        foreach (var data in choices)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.Setup(data, OnCardClicked);
            _spawnedCards.Add(card);
        }

        gameObject.SetActive(true);
        Time.timeScale = 0f;
        Manager.Timer.StopTimer();

        foreach (var cell in Manager.Grid.GetOccupiedCells())
            cell.OccupyingUnit?.PauseLoops();

        RunSelectionTimer().Forget();
    }

    // ── 카드 클릭 ──────────────────────────────────────────────

    private void OnCardClicked(LevelUpCardUI clicked)
    {
        if (_selectedCard == clicked) return;

        _selectedCard?.Deselect();
        _selectedCard = clicked;
        _selectedCard.Select();

        SetConfirmInteractable(true);
    }

    // ── 확인 버튼 ──────────────────────────────────────────────

    public void OnConfirmClicked()
    {
        if (_selectedCard == null) return;

        var data = _selectedCard.GetData();
        if (data != null)
            Manager.LevelUp.ApplyEffect(data);

        Hide();
    }

    // ── 선택 타이머 ────────────────────────────────────────────

    private async UniTaskVoid RunSelectionTimer()
    {
        StopSelectionTimer();
        _selectionCts = new CancellationTokenSource();
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

            // 시간 초과 — 첫 번째 카드 자동 선택 후 확인
            if (_selectedCard == null && _spawnedCards.Count > 0)
            {
                _selectedCard = _spawnedCards[0];
                _selectedCard.Select();
            }
            OnConfirmClicked();
        }
        catch (OperationCanceledException) { }
    }

    private void StopSelectionTimer()
    {
        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
        _selectionCts = null;
    }

    // ── 닫기 ───────────────────────────────────────────────────

    private void Hide()
    {
        StopSelectionTimer();
        ClearCards();
        gameObject.SetActive(false);
        Time.timeScale = 1f;
        Manager.Timer.ResumeTimer();

        foreach (var cell in Manager.Grid.GetOccupiedCells())
            cell.OccupyingUnit?.ResumeLoops();

        Manager.Game.OnLevelUpChoiceMade();
    }

    // ── 유틸 ───────────────────────────────────────────────────

    private void ClearCards()
    {
        foreach (var card in _spawnedCards)
            if (card != null) Destroy(card.gameObject);

        _spawnedCards.Clear();
        _selectedCard = null;
    }

    private void SetConfirmInteractable(bool on)
    {
        if (confirmButton != null)
            confirmButton.interactable = on;
    }
}
