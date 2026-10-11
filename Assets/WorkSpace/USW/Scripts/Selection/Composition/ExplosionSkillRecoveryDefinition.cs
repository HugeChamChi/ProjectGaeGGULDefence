using System;

/// <summary>베탕 자폭 시 스킬 회복 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class ExplosionSkillRecoveryDefinition : SelectionEffectDefinition
{
    /// <summary>회복 시간(초).</summary>
    public float Seconds;
}
