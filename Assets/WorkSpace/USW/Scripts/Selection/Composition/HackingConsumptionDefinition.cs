using System;

/// <summary>감망의 최대 소비량 배율과 소비 스택당 피해 배율.</summary>
[Serializable]
public sealed class HackingConsumptionDefinition : SelectionEffectDefinition
{
    /// <summary>감망의 최대 소비량 배율과 소비 스택당 피해 배율.</summary>
    public float ConsumptionMultiplier = 1f;
    /// <summary>기본 스킬 피해를 제외한 스택 추가분 배율.</summary>
    public float StackDamageMultiplier = 1f;
}
