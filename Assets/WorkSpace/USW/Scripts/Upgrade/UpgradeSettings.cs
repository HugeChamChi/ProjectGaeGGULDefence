using System;
using UnityEngine;

/// <summary>인게임 0~10강의 비용과 단계별 가산 배율을 보관하는 원본 데이터.</summary>
[CreateAssetMenu(fileName = "UpgradeSettings", menuName = "Game/UpgradeSettings")]
public sealed class UpgradeSettings : ScriptableObject
{
    /// <summary>강화 버튼과 전투 계산이 공유하는 최대 강화 수.</summary>
    public const int MaximumLevel = 10;

    /// <summary>다음 단계에 한 번 결제하고 적용할 증가량. 퍼센트 단위 (10 = 10%).</summary>
    [Serializable]
    public struct Step
    {
        public int Cost;
        public float AttackPercent;
        public float AttackSpeedPercent;
    }

    /// <summary>0번 원소는 0→1강, 9번 원소는 9→10강이다.</summary>
    public Step[] Steps = Array.Empty<Step>();
    /// <summary>독립적으로 강화 레벨을 갖는 대상 키.</summary>
    public string[] TargetKeys = Array.Empty<string>();

    /// <summary>등록된 강화 대상인지 확인한다.</summary>
    public bool ContainsTarget(string key) => !string.IsNullOrEmpty(key) && Array.IndexOf(TargetKeys, key) >= 0;

    /// <summary>현재 강화 수에 해당하는 다음 단계의 유효한 데이터를 반환한다.</summary>
    public bool TryGetStep(int currentLevel, out Step step)
    {
        step = default;
        if (Steps == null || currentLevel < 0 || currentLevel >= MaximumLevel || currentLevel >= Steps.Length) return false;
        step = Steps[currentLevel];
        return step.Cost >= 0 && !float.IsNaN(step.AttackPercent) && !float.IsInfinity(step.AttackPercent) &&
            !float.IsNaN(step.AttackSpeedPercent) && !float.IsInfinity(step.AttackSpeedPercent);
    }
}
