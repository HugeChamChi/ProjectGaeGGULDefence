using System;
using VContainer;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 구글 시트에서 강화 데이터를 로드하고, 강화 상태·비용·스탯 조회를 제공합니다.
/// _upgradeManager 로 접근합니다.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
 
    public void Init()
    {

        
    }

    [Inject] private CurrencyManager _currencyManager;
    [Inject] private GameDataManager _gameDataManager;

    private const int MaxUpgradeLevel = 11; // 1~11 (Base is 1, max is 11, using 10 upgrades)

    public bool   IsLoaded { get; private set; }
    public event Action OnLoaded;

    private readonly Dictionary<string, int> _jobLevel = new();
    private readonly Dictionary<int, float> _discounts = new();
    private readonly HashSet<int> _refundedCards = new();
    /// <summary>이번 런에서 성공한 강화에 실제 결제한 식량의 합계. 환급은 지출 이력을 줄이지 않는다.</summary>
    public float TotalSpent { get; private set; }
    /// <summary>강화 가격 변경 시 표시를 갱신한다.</summary>
    public event Action OnCostChanged;

    /// <summary>카드 할인과 해당 시점 실제 누적 지출에 대한 일회성 환급을 적용한다.</summary>
    public void ApplyDiscountAndRefund(int cardId, float rate)
    {
        if (float.IsNaN(rate) || float.IsInfinity(rate)) return;
        rate = Mathf.Clamp01(rate);
        _discounts[cardId] = rate;
        if (_refundedCards.Add(cardId)) _currencyManager?.AddCurrency(TotalSpent * rate);
        OnCostChanged?.Invoke();
    }

    /// <summary>할인만 제거한다. 이미 지급한 환급과 환급 여부는 보존한다.</summary>
    public void RemoveDiscount(int cardId)
    {
        if (_discounts.Remove(cardId)) OnCostChanged?.Invoke();
    }

    private async UniTaskVoid Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        
        // GameDataManager 로딩 대기
        if (_gameDataManager != null)
        {
            await UniTask.WaitUntil(() => _gameDataManager.IsLoaded, cancellationToken: token);

            _jobLevel.Clear();
            foreach (var jobType in _gameDataManager.UpgradeTypes)
            {
                _jobLevel[jobType] = 1; // 기본 1레벨
            }
        }

        IsLoaded = true;
        OnLoaded?.Invoke();
    }

    /// <summary>characterId에 해당하는 직업 타입 문자열 반환. 데이터 없으면 빈 문자열.</summary>
    public string GetJobType(int characterId)
    {
        if (_gameDataManager == null) return string.Empty;
        var row = _gameDataManager.GetCharacterRow(characterId);
        if (row == null) return string.Empty;

        return row.CharacterType;
    }

    /// <summary>직업 타입의 현재 강화 레벨 반환. 없으면 1.</summary>
    public int GetJobLevel(string jobType)
        => _jobLevel.TryGetValue(jobType, out var lv) ? lv : 1;

    public int GetUpgradeLevel(string upgradeTarget)
    {
        return _jobLevel.TryGetValue(upgradeTarget, out var lv) ? lv : 1;
    }

    /// <summary>현재 강화 레벨 기준 공격력 배율(1.0 = 강화 없음)을 반환합니다. 호출자가 자신의 기준 공격력에 곱해서 사용합니다.</summary>
    public float GetAtkUpgradeMultiplier(int characterId)
    {
        if (_gameDataManager == null || !_gameDataManager.IsLoaded) return 1f;

        string jobType = GetJobType(characterId);
        int level = GetUpgradeLevel(jobType);

        float mult = 1f;
        for (int i = 1; i < level; i++)
        {
            var upg = _gameDataManager.GetUpgradeRow(i);
            if (upg != null) mult += upg.AtkIncreaseRate / 100f;
        }

        return mult;
    }

    /// <summary>현재 강화 레벨 기준 공격 속도 배율(1.0 = 강화 없음, 클수록 빠름)을 반환합니다. 호출자가 자신의 기준 공격 간격을 이 값으로 나눠서 사용합니다.</summary>
    public float GetAttackSpeedUpgradeMultiplier(int characterId)
    {
        if (_gameDataManager == null || !_gameDataManager.IsLoaded) return 1f;

        string jobType = GetJobType(characterId);
        int level = GetUpgradeLevel(jobType);

        float speedMult = 1f;
        for (int i = 1; i < level; i++)
        {
            var upg = _gameDataManager.GetUpgradeRow(i);
            if (upg != null) speedMult += upg.AtkSpeedIncreaseRate / 100f;
        }

        return speedMult;
    }

    /// <summary>다음 강화 비용을 반환합니다. 최대 레벨이거나 데이터 없으면 -1.</summary>
    public int GetUpgradeCost(string upgradeTarget)
    {
        if (_gameDataManager == null || !_gameDataManager.IsLoaded) return -1;

        int currentLevel = GetUpgradeLevel(upgradeTarget);

        // MaxUpgradeLevel(10)에 도달하면 더 이상 강화 불가
        if (currentLevel >= MaxUpgradeLevel) return -1;
        
        var upg = _gameDataManager.GetUpgradeRow(currentLevel);
        if (upg == null) return -1;

        float rate = 0f;
        foreach (float discount in _discounts.Values) rate += discount;
        return Mathf.Max(0, Mathf.RoundToInt(upg.UpgradeCost * (1f - Mathf.Clamp01(rate))));
    }

    /// <summary>강화를 시도합니다. 재화 부족 또는 최대 레벨이면 false.</summary>
    public bool TryUpgrade(string upgradeTarget)
    {
        if (upgradeTarget == null || !_jobLevel.ContainsKey(upgradeTarget)) return false;
        int cost = GetUpgradeCost(upgradeTarget);
        if (cost < 0) return false;
        if (_currencyManager == null || !_currencyManager.Spend(cost)) return false;
        TotalSpent += cost;

        if (_jobLevel.ContainsKey(upgradeTarget))
            _jobLevel[upgradeTarget] = Mathf.Min(_jobLevel[upgradeTarget] + 1, MaxUpgradeLevel + 1);

        return true;
    }
}
