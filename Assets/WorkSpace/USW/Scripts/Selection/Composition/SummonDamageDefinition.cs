using System;

/// <summary>소환 피해 비율 지속 설정.</summary>
[Serializable]
public sealed class SummonDamageDefinition : SelectionEffectDefinition
{
    /// <summary>DamageRatio 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float DamageRatio;
}
