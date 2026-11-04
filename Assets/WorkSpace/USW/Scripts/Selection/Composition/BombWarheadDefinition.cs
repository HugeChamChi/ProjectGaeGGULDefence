using System;

/// <summary>자폭 선택지 BombWarhead의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class BombWarheadDefinition : SelectionEffectDefinition
{
    /// <summary>BombDamageMultiplier 설정.</summary>
    public float DamageMultiplier = 1.2f;
    /// <summary>BombWarheadCycleMultiplier 설정.</summary>
    public float CycleMultiplier = 1.15f;
}
