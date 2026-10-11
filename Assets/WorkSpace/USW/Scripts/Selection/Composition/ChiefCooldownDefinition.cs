using System;

/// <summary>족장 스킬 주기 감소 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class ChiefCooldownDefinition : SelectionEffectDefinition
{
    /// <summary>감소 비율.</summary>
    public float ReductionRatio;
}
