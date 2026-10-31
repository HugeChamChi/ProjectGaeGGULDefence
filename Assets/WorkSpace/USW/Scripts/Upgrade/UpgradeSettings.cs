using System;
using UnityEngine;

/// <summary>모든 인게임 유닛의 반복 강화 비용과 단계별 효과를 보관하는 원본 데이터.</summary>
[CreateAssetMenu(fileName = "UpgradeSettings", menuName = "Game/UpgradeSettings")]
public sealed class UpgradeSettings : ScriptableObject
{
    /// <summary>전투 능력 대신 전투 드론 1기당 식량 생산 계수를 강화하는 대상 키.</summary>
    public const string ZeltanTarget = "Zeltan";

    /// <summary>첫 강화에 필요한 식량.</summary>
    [Min(1f)] public float InitialCost = 100f;
    /// <summary>다음 단계마다 곱하는 비용 배율.</summary>
    [Min(1f)] public float CostMultiplier = 2f;
    /// <summary>전투 유닛의 단계당 공격력 증가율(%).</summary>
    [Min(0f)] public float AttackPercentPerLevel = 8f;
    /// <summary>전투 유닛의 단계당 공격 빈도 증가율(%).</summary>
    [Min(0f)] public float AttackSpeedPercentPerLevel = 2f;
    /// <summary>젤탕의 단계당 전투 드론 1기당 초당 식량 증가량. 등급별 기당 계수에 더한다.</summary>
    [Min(0f)] public float ZeltanFoodPerDronePerLevel = 0.04f;
    /// <summary>젤탕 강화 단계별 비용. 길이가 최대 단계이며 공통 배율 비용을 쓰지 않는다.</summary>
    public float[] ZeltanCosts = { 100f, 400f, 800f, 1400f, 2300f };

    /// <summary>젤탕의 최대 강화 단계. 다른 대상은 단계 상한이 없다.</summary>
    public int ZeltanMaxLevel => ZeltanCosts?.Length ?? 0;

    /// <summary>현재 단계에서 더 강화할 수 없는지 확인한다. 젤탕만 상한을 가진다.</summary>
    public bool IsMaxLevel(string key, int currentLevel) => key == ZeltanTarget && currentLevel >= ZeltanMaxLevel;

    /// <summary>다음 단계에 한 번 결제하고 적용할 증가량. 퍼센트 단위 (10 = 10%).</summary>
    [Serializable]
    public struct Step
    {
        /// <summary>이번 단계에 결제할 식량.</summary>
        public float Cost;
        /// <summary>이번 단계의 공격력 증가율(%).</summary>
        public float AttackPercent;
        /// <summary>이번 단계의 공격 빈도 증가율(%).</summary>
        public float AttackSpeedPercent;
    }

    /// <summary>독립적으로 강화 레벨을 갖는 대상 키.</summary>
    public string[] TargetKeys = Array.Empty<string>();

    /// <summary>등록된 강화 대상인지 확인한다.</summary>
    public bool ContainsTarget(string key) => !string.IsNullOrEmpty(key) && Array.IndexOf(TargetKeys, key) >= 0;

    /// <summary>다음 강화의 비용과 효과를 계산한다. 젤탕은 단계별 비용표 끝에서 멈추고,
    /// 다른 대상은 단계 제한 없이 공통 배율 비용을 쓴다. 표현 불가능한 비용은 결제를 거절한다.</summary>
    public bool TryGetStep(string key, int currentLevel, out Step step)
    {
        step = default;
        if (!ContainsTarget(key) || currentLevel < 0) return false;
        bool food = key == ZeltanTarget;
        float cost;
        if (food)
        {
            if (IsMaxLevel(key, currentLevel)) return false;
            cost = ZeltanCosts[currentLevel];
            if (!IsFinite(cost) || cost < 0f) return false;
        }
        else
        {
            if (!IsFinite(InitialCost) || InitialCost <= 0f || !IsFinite(CostMultiplier) || CostMultiplier < 1f) return false;
            cost = (float)(InitialCost * Math.Pow(CostMultiplier, currentLevel));
            if (!IsFinite(cost)) return false;
        }
        step = new Step { Cost = cost, AttackPercent = food ? 0f : AttackPercentPerLevel,
            AttackSpeedPercent = food ? 0f : AttackSpeedPercentPerLevel };
        return IsFinite(step.AttackPercent) && step.AttackPercent >= 0f
            && IsFinite(step.AttackSpeedPercent) && step.AttackSpeedPercent >= 0f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
