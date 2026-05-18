using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 처치 후 표시되는 토템 선택 UI
///
/// ─ Scene 구성 ────────────────────────────────────────────
///   TotemSelectPanel  (이 컴포넌트)
///     ├── CardContainer     — HorizontalLayoutGroup
///     ├── SelectionTimer    — TMP_Text (카운트다운)
///     └── ConfirmButton     — Button
///
/// ─ Inspector 연결 필수 ───────────────────────────────────
///   cardContainer, cardPrefab, confirmButton, selectionTimerText
///   totemPool — 선택지 뽑기 대상 TotemData 배열 (40개 등록 권장)
///
/// ─ 흐름 ─────────────────────────────────────────────────
///   WaveManager.OnSingleBossDefeated()
///     → Manager.TotemSelect.Show(onChoiceMade)
///     → 카드 3장 표시 + 30초 타이머
///     → 선택(또는 타임아웃) → TotemSpawner.SpawnTotemByData()
///     → onChoiceMade 콜백 → 다음 보스/웨이브 진행
/// </summary>
public class TotemSelectUI : InGameSingleton<TotemSelectUI>
{
    [SerializeField] private Transform         cardContainer;
    [SerializeField] private TotemSelectCardUI cardPrefab;
    [SerializeField] private Button            confirmButton;

    [Header("Selection Timer")]
    [SerializeField] private TMP_Text selectionTimerText;
    [SerializeField] private float    selectionSeconds = 30f;

    [Header("토템 풀 (랜덤 3개 대상)")]
    [SerializeField] private TotemData[] totemPool;

    [Header("빈 셀 없을 때 대체 식량")]
    [SerializeField] private float fallbackFood = 500f;

    private const int ChoiceCount = 3;

    private readonly List<TotemSelectCardUI> _spawnedCards = new();
    private          TotemSelectCardUI       _selectedCard;
    private          CancellationTokenSource _selectionCts;
    private          Action                  _onChoiceMade;

    protected override void Awake()
    {
        base.Awake();
        confirmButton?.onClick.AddListener(OnConfirmClicked);
        SetConfirmInteractable(false);
    }

    // ── 열기 ───────────────────────────────────────────────────

    public void Show(Action onChoiceMade)
    {
        _onChoiceMade = onChoiceMade;
        ClearCards();
        _selectedCard = null;
        SetConfirmInteractable(false);

        var choices = GetRandomChoices(ChoiceCount);
        if (choices.Count == 0)
        {
            onChoiceMade?.Invoke();
            return;
        }

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

    private void OnCardClicked(TotemSelectCardUI clicked)
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
        {
            bool placed = Manager.Totem.SpawnTotemByData(data);
            if (!placed)
            {
                Debug.Log($"[TotemSelectUI] 빈 셀 없음 — 식량 {fallbackFood} 지급");
                Manager.Currency.AddCurrency(fallbackFood);
            }
        }

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

            // 타임아웃 — 첫 번째 카드 자동 선택
            if (_spawnedCards.Count == 0)
            {
                Hide();
                return;
            }

            if (_selectedCard == null)
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

        var cb = _onChoiceMade;
        _onChoiceMade = null;
        cb?.Invoke();
    }

    // ── 유틸 ───────────────────────────────────────────────────

    private List<TotemData> GetRandomChoices(int count)
    {
        if (totemPool == null || totemPool.Length == 0)
        {
            Debug.LogWarning("[TotemSelectUI] totemPool 비어있음 — Inspector에서 TotemData 배열 등록 필요");
            return new List<TotemData>();
        }

        var pool   = new List<TotemData>(totemPool);
        var result = new List<TotemData>();
        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        return result;
    }

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
