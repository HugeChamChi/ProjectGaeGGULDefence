using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class PlayerDataController : IDisposable, Global.IClearable
{
    private PlayerData _data;
    public PlayerData Data => _data;
    public bool IsDirty { get; set; }

    public Action<PlayerData> OnPlayerProfilePopupUpdated;
    public Action<PlayerData> OnUpdateUI;
    public Action<int> OnStaminaRecoveryTimer;

    private CancellationTokenSource _staminaLoopCts;
    private bool _disposed = false;
    private BackendGameData _backendData;

    public void Inject(BackendGameData backendData)
    {
        _backendData = backendData;
    }

    public async UniTask InitalizeAsync()
    {
        var tcs = new UniTaskCompletionSource();
        InitData(() => tcs.TrySetResult());

        await tcs.Task;

        StartStaminaTimer();
    }

    public PlayerDataController()
    {
        
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopStaminaTimer();
    }

    public void Clear()
    {
        _data = null;
        StopStaminaTimer();
    }

    // -------------------------
    // 데이터 초기화
    // -------------------------
    public void InitData(Action onCompleted = null)
    {
        if (_backendData == null)
        {
            Debug.LogError("PlayerDataController: BackendGameData is not injected!");
            return;
        }

        _backendData.GameDataGet((data) =>
        {
            _data = data;
            RefreshUI(_data);

            onCompleted?.Invoke();
        });
    }

    public void RefreshUI(PlayerData data)
    {
        _data = data;
        OnUpdateUI?.Invoke(data);
        OnPlayerProfilePopupUpdated?.Invoke(data);
        UpdateStaminaTimer();
    }

    // -------------------------
    // 스태미나 시스템
    // -------------------------
    private void StartStaminaTimer()
    {
        StopStaminaTimer();
        _staminaLoopCts = new CancellationTokenSource();
        StaminaTimerAsync(_staminaLoopCts.Token).Forget(e => { if (e is not System.OperationCanceledException) UnityEngine.Debug.LogException(e); });
    }

    private void StopStaminaTimer()
    {
        _staminaLoopCts?.Cancel();
        _staminaLoopCts?.Dispose();
        _staminaLoopCts = null;
    }

    private void UpdateStaminaTimer()
    {
        if (_data != null && _data.Stamina < _data.MaxStamina)
            StartStaminaTimer();
        else
            StopStaminaTimer();
    }

    private async UniTask StaminaTimerAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (_data != null && _data.Stamina < _data.MaxStamina && _backendData != null)
                {
                    int timeUntilNext = _backendData.GetTimeUntilNextStaminaRecovery(_data.LastStaminaRecoveryTime);
                    OnStaminaRecoveryTimer?.Invoke(timeUntilNext);

                    if (timeUntilNext <= 1)
                        RecoverStamina();
                }
                else
                {
                    OnStaminaRecoveryTimer?.Invoke(0);
                }

                await UniTask.Delay(1000, cancellationToken: token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void RecoverStamina()
    {
        if (_backendData == null) return;

        var (newStamina, newRecoveryTime) = _backendData.CalculateStaminaRecovery(
            _data.Stamina, _data.LastStaminaRecoveryTime, _data.MaxStamina);

        if (newStamina != _data.Stamina)
        {
            _data.Stamina = newStamina;
            _data.LastStaminaRecoveryTime = newRecoveryTime;
            SaveAndRefresh();
            Debug.Log($"스태미나 회복: {_data.Stamina}/{_data.MaxStamina}");
        }
    }

    // -------------------------
    // 골드
    // -------------------------
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        _data.Gold += amount;
        SaveAndRefresh();
    }

    public bool SpendGold(int amount)
    {
        if (_data.Gold < amount)
        {
            Debug.LogWarning($"골드 부족: 현재 {_data.Gold}, 필요 {amount}");
            return false;
        }
        _data.Gold -= amount;
        SaveAndRefresh();
        return true;
    }

    // -------------------------
    // 다이아
    // -------------------------
    public void AddDiamond(int amount)
    {
        if (amount <= 0) return;
        _data.Diamond += amount;
        SaveAndRefresh();
    }

    public bool SpendDiamond(int amount)
    {
        if (_data.Diamond < amount)
        {
            Debug.LogWarning($"다이아 부족: 현재 {_data.Diamond}, 필요 {amount}");
            return false;
        }
        _data.Diamond -= amount;
        SaveAndRefresh();
        return true;
    }

    // -------------------------
    // 스태미나 회복 (다이아 즉시 회복 등)
    // -------------------------
    public void AddStamina(int amount)
    {
        if (amount <= 0) return;
        _data.Stamina = Mathf.Min(_data.Stamina + amount, _data.MaxStamina);
        SaveAndRefresh();
    }

    // -------------------------
    // 스태미나 소모
    // -------------------------
    public bool UseStamina(int amount)
    {
        if (_data.Stamina < amount)
        {
            Debug.LogWarning($"스태미나 부족: 현재 {_data.Stamina}, 필요 {amount}");
            return false;
        }
        _data.Stamina -= amount;
        SaveAndRefresh();
        return true;
    }

    // -------------------------
    // 공통
    // -------------------------
    private void SaveAndRefresh()
    {
        IsDirty = true;
        RefreshUI(_data);
    }

    public async UniTask SaveAsync()
    {
        if (!IsDirty || _data == null || _backendData == null) return;
        
        var tcs = new UniTaskCompletionSource();
        _backendData.GameDataUpdate(_data, () => tcs.TrySetResult());
        await tcs.Task;
        
        IsDirty = false;
    }
}
