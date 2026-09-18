using UnityEngine;
using VContainer;
using System;

public class ExpManager : MonoBehaviour
{
 
    public void Init()
    {

        
    }

    [Inject] private BossManager _bossManager;
    [Inject] private LevelUpManager _levelUpManager;
    [Inject] private GameManager _gameManager;

    [Tooltip("레벨업 필요 경험치 테이블의 정본. 비워두면 아래 폴백값을 사용한다(구글 시트는 더 이상 조회하지 않음).")]
    [SerializeField] private ExpLevelData expLevelData;

    public event Action<float> OnExpChanged;
    public event Action        OnLevelUp;

    // expLevelData가 비어있을 때만 쓰는 최후의 폴백값
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

    /// <summary>현재 레벨에서 다음 레벨까지 필요한 EXP. ExpLevelData(SO)가 정본이며, 미할당 시에만 FallbackExpTable을 쓴다.</summary>
    public float ExpToLevelUp
    {
        get
        {
            if (expLevelData != null) return expLevelData.GetExpRequired(CurrentLevel);
            return FallbackExpTable[Mathf.Min(CurrentLevel - 1, FallbackExpTable.Length - 1)];
        }
    }

    private bool _pendingLevelUp;

    /// <summary>보스 데미지로부터 획득할 EXP 양 계산. 배율은 현재 보스의 BossData(SO)가 정본이다.</summary>
    public float CalculateExpFromDamage(float damage)
    {
        float multiplier  = _bossManager?.CurrentBoss?.ExpMultiplier ?? 0.01f;
        float levelUpMult = _levelUpManager?.ExpGainMultiplier ?? 1f;
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
        var state = _gameManager.CurrentState;

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