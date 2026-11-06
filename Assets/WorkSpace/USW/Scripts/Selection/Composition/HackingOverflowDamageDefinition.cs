using System;

/// <summary>델탕이 실제로 넘친 스택마다 주는 공격력 계수 피해.</summary>
[Serializable]
public sealed class HackingOverflowDamageDefinition : SelectionEffectDefinition
{
    /// <summary>델탕이 실제로 넘친 스택마다 주는 공격력 계수 피해.</summary>
    public float AttackCoefficient;
}
