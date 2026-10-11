using System;

/// <summary>스킬 완충 공격 버스트 지속 설정.</summary>
[Serializable]
public sealed class SkillBurstDefinition : SelectionEffectDefinition
{
    /// <summary>AttackBonus 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float AttackBonus;
    /// <summary>DurationSeconds 설정. 비율은 1=100%, 시간은 초 단위.</summary>
    public float DurationSeconds;
}
