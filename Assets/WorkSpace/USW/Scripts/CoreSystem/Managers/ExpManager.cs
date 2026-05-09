using UnityEngine;
using System;

public class ExpManager : InGameSingleton<ExpManager>
{
    public event Action<float> OnExpChanged;
    public event Action        OnLevelUp;

    // 시트 기준: Lv.1→2=100, 이후 레벨당 +20. 인덱스 i = Lv.(i+1) 에서 다음 레벨까지 필요 EXP
    private static readonly float[] ExpTable =
    {
        100, 120, 140, 160, 180,
        200, 220, 240, 260, 280,
        300, 320, 340, 360, 380,
        400, 420, 440, 460
    };

    public const int MaxLevel = 20;

    public float CurrentExp   { get; private set; }
    public int   CurrentLevel { get; private set; } = 1;
    public bool  IsMaxLevel   => CurrentLevel >= MaxLevel;

    /// <summary>현재 레벨에서 다음 레벨까지 필요한 EXP.</summary>
    public float ExpToLevelUp => ExpTable[Mathf.Min(CurrentLevel - 1, ExpTable.Length - 1)];

    private bool _pendingLevelUp;

    /// <summary>보스 데미지로부터 EXP 획득 (데미지 ÷ 100)</summary>
    public void AddExpFromDamage(float damage) => AddExp(damage / 100f);

    /// <summary>EXP 직접 추가. 초과분은 다음 레벨로 이월.</summary>
    public void AddExp(float amount)
    {
        if (IsMaxLevel) return;

        CurrentExp += amount;
        OnExpChanged?.Invoke(CurrentExp);

        if (CurrentExp >= ExpToLevelUp)
        {
            CurrentExp -= ExpToLevelUp;
            CurrentLevel++;

            if (IsMaxLevel)
                CurrentExp = 0f;

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