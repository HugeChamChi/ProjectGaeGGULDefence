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
///     → 선택(또는 타임아웃) → TotemInventory.TryAdd(), 가득 차면 식량 전환
///     → onChoiceMade 콜백 → 다음 보스/웨이브 진행
/// </summary>
public class TotemSelectUI : MonoBehaviour
{

    [Inject] private TimerController _timerManager;
    [Inject] private TimeScaleService _timeScale;
    [Inject] private GridManager _gridManager;
    [Inject] private CurrencyManager _currencyManager;
    [Inject] private TotemInventory _inventory;

    [SerializeField] private Transform         cardContainer;
    [SerializeField] private TotemSelectCardUI[] cardPrefabs;
    [SerializeField] private Button            confirmButton;

    [Header("Selection Timer")]
    [SerializeField] private TMP_Text selectionTimerText;
    [SerializeField] private float    selectionSeconds = 30f;

    [Header("토템 풀 (랜덤 3개 대상)")]
    [SerializeField] private TotemData[] totemPool;

    /// <summary>Authored candidates used by this selection screen.</summary>
    public TotemData[] TotemPool => totemPool;

    [Header("인벤토리 가득 찼을 때 대체 식량")]
    [SerializeField] private float fallbackFood = 500f;

    [Header("Reroll")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private float rerollCost = 10f;
    [SerializeField] private TMP_Text rerollCostText;

    private const int ChoiceCount = 3;
    private const int TimerTickMilliseconds = 100;
    private const float TimerTickSeconds = TimerTickMilliseconds / 1000f;
    private bool _loading;
    private bool _ownsPause;

    private readonly List<TotemSelectCardUI> _spawnedCards = new();
    private          TotemSelectCardUI       _selectedCard;
    private          CancellationTokenSource _selectionCts;
    private          Action                  _onChoiceMade;
    
    private readonly HashSet<int> _chosenTotems = new HashSet<int>();

    private void Awake()
    {
        confirmButton?.onClick.AddListener(OnConfirmClicked);
        rerollButton?.onClick.AddListener(OnRerollClicked);
        SetConfirmInteractable(false);
    }

    // ── 열기 ───────────────────────────────────────────────────

    /// <summary>Shows a fresh selection; pending work from an older request is cancelled.</summary>
    public void Show(Action onChoiceMade) => ShowAsync(onChoiceMade).Forget();

    private async UniTaskVoid ShowAsync(Action onChoiceMade)
    {
        var token = BeginSelection();
        _onChoiceMade = onChoiceMade;
        try
        {
            if (rerollCostText != null) rerollCostText.text = rerollCost.ToString();
            if (!await LoadCardsAsync(token))
            {
                Hide();
                return;
            }
            token.ThrowIfCancellationRequested();
            gameObject.SetActive(true);
            if (!_ownsPause)
            {
                _ownsPause = true;
                _timeScale?.Pause(this);
                _timerManager?.StopTimer();
                if (_gridManager != null)
                    foreach (var cell in _gridManager.GetOccupiedCells())
                        cell.OccupyingUnit?.PauseLoops();
            }
            await FreezeLayoutAsync(token);
            _loading = false;
            RunSelectionTimer(token).Forget();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (OwnsSelection(token)) CancelSelection();
        }
    }

    private CancellationToken BeginSelection()
    {
        StopSelectionTimer();
        _selectionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _loading = true;
        ClearCards();
        SetConfirmInteractable(false);
        return _selectionCts.Token;
    }

    private bool OwnsSelection(CancellationToken token) =>
        _selectionCts != null && _selectionCts.Token == token;

    private async UniTask<bool> LoadCardsAsync(CancellationToken token)
    {
        var layout = cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = true;
        var choices = GetRandomChoices(ChoiceCount);
        if (choices.Count == 0) return false;

        var loads = new List<UniTask>();
        foreach (var data in choices)
            if (data != null) loads.Add(data.LoadAssetsAsync());
        // Shared asset loads may finish in the cache; a closed UI must not resume using them.
        await UniTask.WhenAll(loads).AttachExternalCancellation(token);
        token.ThrowIfCancellationRequested();
        foreach (var data in choices)
        {
            var card = Instantiate(cardPrefabs[(int)data.tier], cardContainer);
            card.Setup(data, OnCardClicked);
            card.ConfigurePeek(GetComponentInChildren<UI_Peekthrough>(true));
            _spawnedCards.Add(card);
        }
        return true;
    }

    private async UniTask FreezeLayoutAsync(CancellationToken token)
    {
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        var layout = cardContainer.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
    }

    // ── 카드 클릭 ──────────────────────────────────────────────

    private void OnCardClicked(TotemSelectCardUI clicked)
    {
        if (_loading || _selectedCard == clicked) return;
        _selectedCard?.Deselect();
        _selectedCard = clicked;
        _selectedCard.Select();
        SetConfirmInteractable(true);
    }

    // ── 확인 버튼 ──────────────────────────────────────────────

    /// <summary>Grants the selected totem and completes this selection once.</summary>
    public void OnConfirmClicked()
    {
        if (_loading || _selectedCard == null) return;

        var data = _selectedCard.GetData();
        if (data != null)
        {
            _chosenTotems.Add(data.totemId); // 중복 방지 캐싱
            
            bool stored = _inventory.TryAdd(data);
            if (!stored)
            {
                Debug.Log($"[TotemSelectUI] 토템 인벤토리 가득 참 — 식량 {fallbackFood} 지급");
                _currencyManager.AddCurrency(fallbackFood);
            }
        }

        Hide();
    }

    // ── 리롤 버튼 ──────────────────────────────────────────────
    /// <summary>Rerolls once; clicks during loading do not spend additional currency.</summary>
    public void OnRerollClicked()
    {
        if (_loading || _selectionCts == null) return;
        if (!_currencyManager.Spend(rerollCost))
        {
            Debug.Log("[TotemSelectUI] 식량이 부족하여 리롤할 수 없습니다.");
            return;
        }
        RerollAsync(BeginSelection()).Forget();
    }

    private async UniTaskVoid RerollAsync(CancellationToken token)
    {
        try
        {
            if (!await LoadCardsAsync(token))
            {
                Hide();
                return;
            }
            token.ThrowIfCancellationRequested();
            await FreezeLayoutAsync(token);
            _loading = false;
            RunSelectionTimer(token).Forget();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (OwnsSelection(token)) CancelSelection();
        }
    }

    // ── 선택 타이머 ────────────────────────────────────────────

    private async UniTaskVoid RunSelectionTimer(CancellationToken token)
    {

        try
        {
            float remaining = selectionSeconds;
            while (remaining > 0f)
            {
                if (selectionTimerText != null)
                    selectionTimerText.text = $"{Mathf.CeilToInt(remaining)}";

                await UniTask.Delay(TimerTickMilliseconds, DelayType.Realtime, cancellationToken: token);
                remaining -= TimerTickSeconds;
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
        var source = _selectionCts;
        _selectionCts = null;
        source?.Cancel();
        source?.Dispose();
    }

    // ── 닫기 ───────────────────────────────────────────────────

    private void Hide()
    {
        var callback = _onChoiceMade;
        CancelSelection();
        gameObject.SetActive(false);
        callback?.Invoke();
    }

    private void CancelSelection()
    {
        StopSelectionTimer();
        _loading = false;
        _onChoiceMade = null;
        ClearCards();
        if (!_ownsPause) return;
        _ownsPause = false;
        _timeScale?.Release(this);
        _timerManager?.ResumeTimer();
        if (_gridManager != null)
            foreach (var cell in _gridManager.GetOccupiedCells())
                cell.OccupyingUnit?.ResumeLoops();
    }

    private void OnDisable() => CancelSelection();
    private void OnDestroy() => CancelSelection();

    // ── 유틸 ───────────────────────────────────────────────────

    private List<TotemData> GetRandomChoices(int count)
    {
        if (totemPool == null || totemPool.Length == 0)
        {
            Debug.LogWarning("[TotemSelectUI] totemPool 비어있음");
            return new List<TotemData>();
        }

        return TotemChoiceRoller.Roll(totemPool, _chosenTotems, count);
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
