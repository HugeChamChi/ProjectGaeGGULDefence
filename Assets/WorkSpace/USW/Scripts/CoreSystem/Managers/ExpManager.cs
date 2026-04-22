using UnityEngine;
using System;

// ════════════════════════════════════════════════════════
// ExpManager — InGameSingleton 교체
// ════════════════════════════════════════════════════════
public class ExpManager : InGameSingleton<ExpManager>
{
    public event Action<float> OnExpChanged;
    public event Action        OnLevelUp;

    [SerializeField] private float expToLevelUp = 1000f;

    public float CurrentExp   { get; private set; }
    public int   CurrentLevel { get; private set; } = 1;
    public float ExpToLevelUp => expToLevelUp;

    private bool _pendingLevelUp = false;

    public void AddExp(float amount)
    {
        CurrentExp += amount;
        OnExpChanged?.Invoke(CurrentExp);

        if (CurrentExp >= expToLevelUp)
        {
            CurrentExp = 0f;
            CurrentLevel++;
            TryFireLevelUp();
        }
    }

    public void TryFireLevelUp()
    {
        var state = Manager.Game.CurrentState;

        if (state == GameManager.GameState.Playing)
        {
            _pendingLevelUp = false;
            OnLevelUp?.Invoke();
        }
        else
        {
            _pendingLevelUp = true;
            Debug.Log($"[ExpManager] 레벨업 보류 (현재 상태: {state})");
        }
    }

    public void FlushPendingLevelUp()
    {
        if (!_pendingLevelUp) return;
        _pendingLevelUp = false;
        Debug.Log("[ExpManager] 보류된 레벨업 발동");
        OnLevelUp?.Invoke();
    }
}