using System;
using VContainer;
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
///     → _totemSelectManager.Show(onChoiceMade)
///     → 카드 3장 표시 + 30초 타이머
///     → 선택(또는 타임아웃) → TotemSpawner.SpawnTotemByData()
///     → onChoiceMade 콜백 → 다음 보스/웨이브 진행
/// </summary>
public class TotemSelectUI : InGameSingleton<TotemSelectUI>
{

    [Inject] private TimerController _timerManager;
    [Inject] private GridManager _gridManager;
    [Inject] private TotemSpawner _totemManager;
    [Inject] private CurrencyManager _currencyManager;

    [SerializeField] private Transform         cardContainer;
    [SerializeField] private TotemSelectCardUI[] cardPrefabs;
    [SerializeField] private Button            confirmButton;

    [Header("Selection Timer")]
    [SerializeField] private TMP_Text selectionTimerText;
    [SerializeField] private float    selectionSeconds = 30f;

    [Header("토템 풀 (랜덤 3개 대상)")]
    [SerializeField] private TotemData[] totemPool;

    public TotemData[] TotemPool => totemPool;

    [Header("빈 셀 없을 때 대체 식량")]
    [SerializeField] private float fallbackFood = 500f;

    [Header("Reroll")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private float rerollCost = 10f;
    [SerializeField] private TMP_Text rerollCostText;

    private const int ChoiceCount = 3;

    private readonly List<TotemSelectCardUI> _spawnedCards = new();
    private          TotemSelectCardUI       _selectedCard;
    private          CancellationTokenSource _selectionCts;
    private          Action                  _onChoiceMade;
    
    private readonly HashSet<int> _chosenTotems = new HashSet<int>();

    protected override void Awake()
    {
        // base.Awake();
        confirmButton?.onClick.AddListener(OnConfirmClicked);
        rerollButton?.onClick.AddListener(OnRerollClicked);
        SetConfirmInteractable(false);
    }

    // ── 열기 ───────────────────────────────────────────────────

    public async void Show(Action onChoiceMade)
    {
        _onChoiceMade = onChoiceMade;
        
        var layout = cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = true;
        
        if (rerollCostText != null)
            rerollCostText.text = rerollCost.ToString();

        _selectedCard = null;
        ClearCards();
        SetConfirmInteractable(false);

        var choices = GetRandomChoices(ChoiceCount);
        if (choices.Count == 0)
        {
            onChoiceMade?.Invoke();
            return;
        }

        var loadTasks = new List<UniTask>();
        foreach (var data in choices)
        {
            if (data != null) loadTasks.Add(data.LoadAssetsAsync());
        }
        await UniTask.WhenAll(loadTasks);

        foreach (var data in choices)
        {
            var prefab = cardPrefabs[(int)data.tier];
            var card = Instantiate(prefab, cardContainer);
            card.Setup(data, OnCardClicked);
            _spawnedCards.Add(card);
        }

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

    private void OnCardClicked(TotemSelectCardUI clicked)
    {
        if (_selectedCard == clicked) return;
        _selectedCard?.Deselect();
        _selectedCard = clicked;
        _selectedCard.Select();
        SetConfirmInteractable(true);
    }

    // ── 확인 버튼 ──────────────────────────────────────────────

    public async void OnConfirmClicked()
    {
        if (_selectedCard == null) return;

        var data = _selectedCard.GetData();
        if (data != null)
        {
            _chosenTotems.Add(data.totemId); // 중복 방지 캐싱
            
            bool placed = await _totemManager.SpawnTotemByData(data);
            if (!placed)
            {
                Debug.Log($"[TotemSelectUI] 빈 셀 없음 — 식량 {fallbackFood} 지급");
                _currencyManager.AddCurrency(fallbackFood);
            }
        }

        Hide();
    }

    // ── 리롤 버튼 ──────────────────────────────────────────────
    public async void OnRerollClicked()
    {
        if (!_currencyManager.Spend(rerollCost))
        {
            Debug.Log("[TotemSelectUI] 식량이 부족하여 리롤할 수 없습니다.");
            return;
        }
        
        var layout = cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = true;

        _selectedCard = null;
        ClearCards();
        SetConfirmInteractable(false);

        var choices = GetRandomChoices(ChoiceCount);
        if (choices.Count == 0)
        {
            Hide();
            return;
        }

        var loadTasks = new List<UniTask>();
        foreach (var data in choices)
        {
            if (data != null) loadTasks.Add(data.LoadAssetsAsync());
        }
        await UniTask.WhenAll(loadTasks);

        foreach (var data in choices)
        {
            var prefab = cardPrefabs[(int)data.tier];
            var card = Instantiate(prefab, cardContainer);
            card.Setup(data, OnCardClicked);
            _spawnedCards.Add(card);
        }

        FreezeLayoutAsync(layout).Forget();

        RunSelectionTimer().Forget();
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
        _timerManager.ResumeTimer();

        foreach (var cell in _gridManager.GetOccupiedCells())
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
            Debug.LogWarning("[TotemSelectUI] totemPool 비어있음");
            return new List<TotemData>();
        }

        var filtered = new List<TotemData>();
        foreach (var data in totemPool)
        {
            if (data != null && !_chosenTotems.Contains(data.totemId))
                filtered.Add(data);
        }

        var tierGroups = new Dictionary<Tier, List<TotemData>>();
        foreach (var d in filtered)
        {
            if (!tierGroups.ContainsKey(d.tier)) tierGroups[d.tier] = new List<TotemData>();
            tierGroups[d.tier].Add(d);
        }

        if (tierGroups.Count == 0) return new List<TotemData>();

        float roll = UnityEngine.Random.Range(0f, filtered.Count);
        float cumul = 0f;
        Tier selectedTier = Tier.Normal;

        foreach (var kvp in tierGroups)
        {
            cumul += kvp.Value.Count;
            if (roll <= cumul)
            {
                selectedTier = kvp.Key;
                break;
            }
        }

        var group = tierGroups[selectedTier];
        var result = new List<TotemData>();
        int pickCount = Mathf.Min(count, group.Count);

        for (int i = 0; i < pickCount; i++)
        {
            int idx = UnityEngine.Random.Range(0, group.Count);
            result.Add(group[idx]);
            group.RemoveAt(idx);
        }

        filtered.RemoveAll(x => result.Contains(x));
        while (result.Count < count && filtered.Count > 0)
        {
            int idx = UnityEngine.Random.Range(0, filtered.Count);
            result.Add(filtered[idx]);
            filtered.RemoveAt(idx);
        }

        return result;
    }

    private void ClearCards()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }
        _spawnedCards.Clear();
        _selectedCard = null;
    }

    private void SetConfirmInteractable(bool on)
    {
        if (confirmButton != null)
            confirmButton.interactable = on;
    }
}
