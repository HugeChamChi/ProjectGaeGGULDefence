using System;

/// <summary>자폭 선택지 BetanCycle의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class BetanCycleDefinition : SelectionEffectDefinition
{
    /// <summary>BetanCycleMultiplier 설정.</summary>
    public float TimeMultiplier = .8f;
}
