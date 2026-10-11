using System;

/// <summary>런별 정수 퍼센트 생산 보너스 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class HackingProductionRollDefinition : SelectionEffectDefinition
{
    /// <summary>최소 비율(정수 퍼센트).</summary>
    public float MinimumRatio;
    /// <summary>최대 비율(정수 퍼센트).</summary>
    public float MaximumRatio;
}
