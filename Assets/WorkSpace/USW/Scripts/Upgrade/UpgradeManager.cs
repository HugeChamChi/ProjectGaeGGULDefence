using System;
using VContainer;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SO 기반 인게임 강화 상태·비용·전투 배율을 제공합니다.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
 
    /// <summary>씬 진입 시 강화 상태를 0강으로 초기화한다. 시트 로딩과 무관하다.</summary>
    public void Init()
    {
        _jobLevel.Clear();
        _discounts.Clear();
        _refundedCards.Clear();
        TotalSpent = 0f;
        IsLoaded = _settings != null;
        OnLoaded?.Invoke();
        OnCostChanged?.Invoke();
    }

    [Inject] private CurrencyManager _currencyManager;
    [Inject] private UpgradeKeyOverrides _keyOverrides;
    [Inject] private UpgradeSettings _settings;

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

    /// <summary>SO에 등록된 characterId의 독립 강화 키를 반환한다. 미등록 유닛은 강화 보정을 받지 않는다.</summary>
    public string GetJobType(int characterId)
    {
        if (_keyOverrides != null && _keyOverrides.TryGetOverride(characterId, out var overrideKey))
            return overrideKey;

        return string.Empty;
    }

    /// <summary>직업 타입의 현재 강화 수 반환. 미강화는 0.</summary>
    public int GetJobLevel(string jobType)
        => GetUpgradeLevel(jobType);

    /// <summary>현재 강화 수를 0~10 범위로 반환한다.</summary>
    public int GetUpgradeLevel(string upgradeTarget)
    {
        return upgradeTarget != null && _jobLevel.TryGetValue(upgradeTarget, out var lv)
            ? Mathf.Clamp(lv, 0, UpgradeSettings.MaximumLevel) : 0;
    }

    /// <summary>비용 데이터 유무와 구분하여 최대 강화 여부를 반환한다.</summary>
    public bool IsMaxLevel(string target) => GetUpgradeLevel(target) >= UpgradeSettings.MaximumLevel;

    /// <summary>현재 강화 레벨 기준 공격력 배율(1.0 = 강화 없음)을 반환합니다. 호출자가 자신의 기준 공격력에 곱해서 사용합니다.</summary>
    public float GetAtkUpgradeMultiplier(int characterId)
    {
        if (_settings == null) return 1f;

        string jobType = GetJobType(characterId);
        int level = GetUpgradeLevel(jobType);

        float mult = 1f;
        for (int i = 0; i < level; i++)
        {
            if (_settings.TryGetStep(i, out var step)) mult += step.AttackPercent / 100f;
        }

        return mult;
    }

    /// <summary>현재 강화 레벨 기준 공격 속도 배율(1.0 = 강화 없음, 클수록 빠름)을 반환합니다. 호출자가 자신의 기준 공격 간격을 이 값으로 나눠서 사용합니다.</summary>
    public float GetAttackSpeedUpgradeMultiplier(int characterId)
    {
        if (_settings == null) return 1f;

        string jobType = GetJobType(characterId);
        int level = GetUpgradeLevel(jobType);

        float speedMult = 1f;
        for (int i = 0; i < level; i++)
        {
            if (_settings.TryGetStep(i, out var step)) speedMult += step.AttackSpeedPercent / 100f;
        }

        return speedMult;
    }

    /// <summary>다음 강화 비용을 반환합니다. 최대 레벨이거나 데이터 없으면 -1.</summary>
    public int GetUpgradeCost(string upgradeTarget)
    {
        if (_settings == null || !_settings.ContainsTarget(upgradeTarget)) return -1;

        int currentLevel = GetUpgradeLevel(upgradeTarget);

        if (!_settings.TryGetStep(currentLevel, out var step)) return -1;

        float rate = 0f;
        foreach (float discount in _discounts.Values) rate += discount;
        return Mathf.Max(0, Mathf.RoundToInt(step.Cost * (1f - Mathf.Clamp01(rate))));
    }

    /// <summary>강화를 시도합니다. 재화 부족 또는 최대 레벨이면 false.</summary>
    public bool TryUpgrade(string upgradeTarget)
    {
        int cost = GetUpgradeCost(upgradeTarget);
        if (cost < 0) return false;
        if (_currencyManager == null || !_currencyManager.Spend(cost)) return false;
        TotalSpent += cost;

        _jobLevel[upgradeTarget] = GetUpgradeLevel(upgradeTarget) + 1;
        OnCostChanged?.Invoke();

        return true;
    }
}
