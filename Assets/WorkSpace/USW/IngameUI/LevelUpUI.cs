using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ════════════════════════════════════════════════════════
// LevelUpUI
//
// ─ Scene 구성 ────────────────────────────────────────
//   LevelUpPanel  ← 이 GameObject에 LevelUpUI 컴포넌트 부착
//     ├── CardContainer  ← HorizontalLayoutGroup (Inspector 연결)
//     └── ConfirmButton  ← Button, 자식 어딘가에 있으면 자동 탐색
//
// ─ Inspector 연결 ─────────────────────────────────────
//   cardContainer, cardPrefab 두 개만 연결하면 됩니다.
// ════════════════════════════════════════════════════════
public class LevelUpUI : InGameSingleton<LevelUpUI>
{
    [SerializeField] private Transform     cardContainer;
    [SerializeField] private LevelUpCardUI cardPrefab;
    [SerializeField] private Button        confirmButton;

    private const int ChoiceCount = 3;

    private readonly List<LevelUpCardUI> _spawnedCards = new();
    private          LevelUpCardUI       _selectedCard;

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
        Manager.Timer.StopTimer();

        foreach (var cell in Manager.Grid.GetOccupiedCells())
            cell.OccupyingUnit?.OnRemoved();
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

    // ── 닫기 ───────────────────────────────────────────────────

    private void Hide()
    {
        ClearCards();
        gameObject.SetActive(false);
        Manager.Timer.ResumeTimer();

        foreach (var cell in Manager.Grid.GetOccupiedCells())
        {
            if (cell.OccupyingUnit != null)
                cell.OccupyingUnit.OnPlaced(Manager.Currency, Manager.Boss.CurrentBoss, cell);
        }

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
