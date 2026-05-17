using UnityEngine;
using System;

public class ExpManager : InGameSingleton<ExpManager>
{
    public event Action<float> OnExpChanged;
    public event Action        OnLevelUp;

    // GameDataManager 로드 전 폴백값 (시트: 1,10,20,30,50,100,150,200,250,300,350,400,450,500,550,600,650,700,750,800)
    private static readonly float[] FallbackExpTable =
    {
        1, 10, 20, 30, 50,
        100, 150, 200, 250, 300,
        350, 400, 450, 500, 550,
        600, 650, 700, 750, 800
    };

    public const int MaxLevel = 20;

    public float CurrentExp   { get; private set; }
    public int   CurrentLevel { get; private set; } = 1;
    public bool  IsMaxLevel   => CurrentLevel >= MaxLevel;

    /// <summary>현재 레벨에서 다음 레벨까지 필요한 EXP. GameDataManager 우선, 폴백은 FallbackExpTable.</summary>
    public float ExpToLevelUp
    {
        get
        {
            if (Manager.GameData != null && Manager.GameData.IsLoaded)
                return Manager.GameData.GetExpRequired(CurrentLevel);
            return FallbackExpTable[Mathf.Min(CurrentLevel - 1, FallbackExpTable.Length - 1)];
        }
    }

    private bool _pendingLevelUp;

    /// <summary>보스 데미지로부터 획득할 EXP 양 계산.</summary>
    public float CalculateExpFromDamage(float damage)
    {
        float multiplier    = Manager.GameData != null && Manager.GameData.IsLoaded
            ? Manager.GameData.GetCurrentExpMultiplier()
            : 0.01f;
        float levelUpMult   = Manager.LevelUp?.ExpGainMultiplier ?? 1f;
        return damage * multiplier * levelUpMult;
    }

    /// <summary>보스 데미지로부터 EXP 획득. 즉시 추가됨.</summary>
    public void AddExpFromDamage(float damage)
    {
        AddExp(CalculateExpFromDamage(damage));
    }

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