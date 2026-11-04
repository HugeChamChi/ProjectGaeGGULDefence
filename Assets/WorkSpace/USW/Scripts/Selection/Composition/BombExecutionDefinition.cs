using System;

/// <summary>자폭 선택지 BombExecution의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class BombExecutionDefinition : SelectionEffectDefinition
{
    /// <summary>BombExecutionThreshold 설정.</summary>
    public float HpThreshold = .3f;
    /// <summary>BombExecutionBonus 설정.</summary>
    public float DamageBonus = .2f;
}
