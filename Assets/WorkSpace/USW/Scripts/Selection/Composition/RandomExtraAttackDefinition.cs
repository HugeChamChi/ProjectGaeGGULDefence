using System;

/// <summary>확률 추가 공격 지속 설정.</summary>
[Serializable]
public sealed class RandomExtraAttackDefinition : SelectionEffectDefinition
{
    /// <summary>Chance 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float Chance;
}
