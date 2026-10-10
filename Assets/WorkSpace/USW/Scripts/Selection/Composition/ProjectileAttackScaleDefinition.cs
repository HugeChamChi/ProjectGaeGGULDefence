using System;

/// <summary>투사체 크기 10%당 공격 가산 지속 설정.</summary>
[Serializable]
public sealed class ProjectileAttackScaleDefinition : SelectionEffectDefinition
{
    /// <summary>AttackPerSizeStep 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float AttackPerSizeStep;
}
