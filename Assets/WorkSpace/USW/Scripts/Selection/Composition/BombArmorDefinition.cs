using System;

/// <summary>자폭 선택지 BombArmor의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class BombArmorDefinition : SelectionEffectDefinition
{
    /// <summary>BombArmorReduction 설정.</summary>
    public float ReductionRatio = .2f;
    /// <summary>BombArmorDuration 설정.</summary>
    public float DurationSeconds = 3f;
}
