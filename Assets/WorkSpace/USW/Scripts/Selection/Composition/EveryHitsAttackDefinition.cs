using System;

/// <summary>N회 공격마다 추가 공격 지속 설정.</summary>
[Serializable]
public sealed class EveryHitsAttackDefinition : SelectionEffectDefinition
{
    /// <summary>HitCount 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public int HitCount;
}
