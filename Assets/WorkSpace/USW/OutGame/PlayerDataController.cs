using System;
using System.Collections;
using UnityEngine;
using BackEnd;

public class PlayerDataController : MonoBehaviour
{
    [SerializeField] private PlayerDataView _view;

    private PlayerData _data;
    public PlayerData Data => _data;

    public Action<PlayerData> OnPlayerProfilePopupUpdated;
    public Action<PlayerData> OnUpdateUI;
    public Action<int> OnStaminaRecoveryTimer;

    private Coroutine _staminaTimer;

    private void Start()
    {
        OnUpdateUI += _view.UpdateUI;
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
        if (_staminaTimer != null)
            StopCoroutine(_staminaTimer);
        _staminaTimer = StartCoroutine(StaminaTimerCoroutine());
    }

    private void StopStaminaTimer()
    {
        if (_staminaTimer != null)
        {
            StopCoroutine(_staminaTimer);
            _staminaTimer = null;
        }
    }

    private void UpdateStaminaTimer()
    {
        if (_data != null && _data.Stamina < _data.MaxStamina)
        {
            int timeUntilNext = BackendGameData.Instance.GetTimeUntilNextStaminaRecovery(_data.LastStaminaRecoveryTime);
            _staminaTimer = null;
            StartStaminaTimer();
        }
    }

    private IEnumerator StaminaTimerCoroutine()
    {
        while (true)
        {
            if (_data != null && _data.Stamina < _data.MaxStamina)
            {
                int timeUntilNext = BackendGameData.Instance.GetTimeUntilNextStaminaRecovery(_data.LastStaminaRecoveryTime);
                OnStaminaRecoveryTimer?.Invoke(timeUntilNext);

                if (timeUntilNext <= 1)
                    StartCoroutine(RecoverStaminaCoroutine());
            }
            else
            {
                OnStaminaRecoveryTimer?.Invoke(0);
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator RecoverStaminaCoroutine()
    {
        var (newStamina, newRecoveryTime) = BackendGameData.Instance.CalculateStaminaRecovery(
            _data.Stamina, _data.LastStaminaRecoveryTime);

        if (newStamina != _data.Stamina)
        {
            _data.Stamina = newStamina;
            _data.LastStaminaRecoveryTime = newRecoveryTime;

            BackendGameData.Instance.userData.Stamina = newStamina;
            BackendGameData.Instance.userData.LastStaminaRecoveryTime = newRecoveryTime;
            BackendGameData.Instance.GameDataUpdate();

            RefreshUI(_data);
            Debug.Log($"스태미나 회복: {_data.Stamina}/{_data.MaxStamina}");
        }

        yield return null;
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
        BackendGameData.Instance.userData.Stamina = _data.Stamina;
        BackendGameData.Instance.GameDataUpdate();
        RefreshUI(_data);
        return true;
    }
}
