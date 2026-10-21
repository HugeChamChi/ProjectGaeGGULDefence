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
    [Inject] private AudioManager _audioManager;
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

    /// <summary>검증된 선택지 할인 전체를 교체한다. 환급 이력/지출은 유지하고 알림은 별도 발행한다.</summary>
    public void ReplaceSelectionDiscounts(IReadOnlyDictionary<int, float> discounts)
    {
        if (discounts == null) throw new ArgumentNullException(nameof(discounts));
        foreach (var entry in discounts)
            if (float.IsNaN(entry.Value) || float.IsInfinity(entry.Value) || entry.Value < 0f || entry.Value > 1f)
                throw new ArgumentException("Invalid selection discount.");
        _discounts.Clear();
        foreach (var entry in discounts) _discounts.Add(entry.Key, entry.Value);
    }

    /// <summary>기존 누적 실제 지출을 기준으로 카드당 한 번만 환급한다.</summary>
    public void RefundSelectionOnce(int cardId, float rate)
    {
        if (float.IsNaN(rate) || float.IsInfinity(rate) || rate < 0f || rate > 1f)
            throw new ArgumentOutOfRangeException(nameof(rate));
        if (_refundedCards.Add(cardId)) _currencyManager?.AddCurrency(TotalSpent * rate);
    }

    /// <summary>모든 지속 값 반영 후 가격 표시를 갱신한다.</summary>
    public void NotifySelectionDiscountsChanged()
    {
        if (OnCostChanged == null) return;
        foreach (System.Action handler in OnCostChanged.GetInvocationList())
            try { handler(); } catch (System.Exception error) { Debug.LogException(error); }
    }

    /// <summary>이관 중 기존 호출을 위한 할인/환급 진입점.</summary>
    public void ApplyDiscountAndRefund(int cardId, float rate)
    {
        if (float.IsNaN(rate) || float.IsInfinity(rate)) return;
        rate = Mathf.Clamp01(rate);
        _discounts[cardId] = rate;
        RefundSelectionOnce(cardId, rate);
        NotifySelectionDiscountsChanged();
    }

    /// <summary>할인만 제거한다. 이미 지급한 환급과 환급 여부는 보존한다.</summary>
    public void RemoveDiscount(int cardId)
    {
        if (_discounts.Remove(cardId)) NotifySelectionDiscountsChanged();
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

    /// <summary>현재 강화 수를 반환한다. 게임 규칙상의 단계 상한은 없다.</summary>
    public int GetUpgradeLevel(string upgradeTarget)
    {
        return upgradeTarget != null && _jobLevel.TryGetValue(upgradeTarget, out var lv)
            ? Mathf.Max(0, lv) : 0;
    }

    /// <summary>강화와 관계없는 드론 1기당 초당 식량 생산량.</summary>
    public float ZeltanFoodPerDrone => _settings != null ? _settings.ZeltanFoodPerDrone : 0f;

    /// <summary>현재 젤탕 강화로 더하는 자체 초당 식량. 선택지 보정 전 값이다.</summary>
    public float ZeltanFoodBonus => _settings != null
        ? GetUpgradeLevel(UpgradeSettings.ZeltanTarget) * _settings.ZeltanFoodPerLevel : 0f;

    /// <summary>동일 설정에서 강화 안내 문구를 만든다.</summary>
    public string GetUpgradeDescription(string target) => _settings == null ? string.Empty
        : target == UpgradeSettings.ZeltanTarget
            ? $"자체 +{_settings.ZeltanFoodPerLevel:0.##}/초\n드론 {_settings.ZeltanFoodPerDrone:0.##}/초\n고정"
            : $"공격력 +{_settings.AttackPercentPerLevel:0.##}%\n공속 +{_settings.AttackSpeedPercentPerLevel:0.##}%";

    /// <summary>현재 강화 레벨 기준 공격력 배율(1.0 = 강화 없음)을 반환합니다. 호출자가 자신의 기준 공격력에 곱해서 사용합니다.</summary>
    public float GetAtkUpgradeMultiplier(int characterId)
    {
        if (_settings == null) return 1f;

        string jobType = GetJobType(characterId);
        return !_settings.ContainsTarget(jobType) || jobType == UpgradeSettings.ZeltanTarget ? 1f
            : 1f + GetUpgradeLevel(jobType) * _settings.AttackPercentPerLevel / 100f;
    }

    /// <summary>현재 강화 레벨 기준 공격 속도 배율(1.0 = 강화 없음, 클수록 빠름)을 반환합니다. 호출자가 자신의 기준 공격 간격을 이 값으로 나눠서 사용합니다.</summary>
    public float GetAttackSpeedUpgradeMultiplier(int characterId)
    {
        if (_settings == null) return 1f;

        string jobType = GetJobType(characterId);
        return !_settings.ContainsTarget(jobType) || jobType == UpgradeSettings.ZeltanTarget ? 1f
            : 1f + GetUpgradeLevel(jobType) * _settings.AttackSpeedPercentPerLevel / 100f;
    }

    /// <summary>다음 강화 비용을 반환한다. 설정 오류나 표현 불가능한 비용이면 -1.</summary>
    public float GetUpgradeCost(string upgradeTarget)
    {
        if (_settings == null || !_settings.ContainsTarget(upgradeTarget)) return -1;

        int currentLevel = GetUpgradeLevel(upgradeTarget);

        if (!_settings.TryGetStep(upgradeTarget, currentLevel, out var step)) return -1;

        float rate = 0f;
        foreach (float discount in _discounts.Values) rate += discount;
        return Mathf.Max(0f, Mathf.Round(step.Cost * (1f - Mathf.Clamp01(rate))));
    }

    /// <summary>식량을 결제하고 한 단계 강화한다. 유효하지 않은 비용이나 재화 부족이면 false.</summary>
    public bool TryUpgrade(string upgradeTarget)
    {
        float cost = GetUpgradeCost(upgradeTarget);
        if (cost < 0 || float.IsNaN(cost) || float.IsInfinity(cost)
            || GetUpgradeLevel(upgradeTarget) == int.MaxValue || float.IsInfinity(TotalSpent + cost)) return false;
        if (_currencyManager == null || !_currencyManager.Spend(cost)) return false;
        TotalSpent += cost;

        _jobLevel[upgradeTarget] = GetUpgradeLevel(upgradeTarget) + 1;
        _audioManager?.PlayCue(GameAudioCue.UpgradeAttempt);
        OnCostChanged?.Invoke();

        return true;
    }
}
