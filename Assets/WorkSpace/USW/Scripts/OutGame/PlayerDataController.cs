using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class PlayerDataController : MonoBehaviour
{
    private PlayerData _data;
    public PlayerData Data => _data;

    public Action<PlayerData> OnPlayerProfilePopupUpdated;
    public Action<PlayerData> OnUpdateUI;
    public Action<int> OnStaminaRecoveryTimer;

    private CancellationTokenSource _staminaLoopCts;

    private void Start()
    {
        InitData();
    }

    private void OnEnable()
    {
        StartStaminaTimer();
    }

    private void OnDisable()
    {
        StopStaminaTimer();
    }

    private void OnDestroy()
    {
        StopStaminaTimer();
    }

    // -------------------------
    // 데이터 초기화
    // -------------------------
    public void InitData()
    {
        BackendGameData.Instance.GameDataGet((data) =>
        {
            _data = data;
            RefreshUI(_data);
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
        // OnDisable에서 수동 취소 가능하고,
        // 오브젝트 Destroy 시에도 자동 취소되도록 DestroyToken과 연결
        _staminaLoopCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        StaminaTimerAsync(_staminaLoopCts.Token).Forget(Debug.LogException);
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

                if (_data != null && _data.Stamina < _data.MaxStamina)
                {
                    int timeUntilNext = BackendGameData.Instance
                        .GetTimeUntilNextStaminaRecovery(_data.LastStaminaRecoveryTime);
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
        var (newStamina, newRecoveryTime) = BackendGameData.Instance.CalculateStaminaRecovery(
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
        BackendGameData.Instance.GameDataUpdate(_data);
        RefreshUI(_data);
    }
}
