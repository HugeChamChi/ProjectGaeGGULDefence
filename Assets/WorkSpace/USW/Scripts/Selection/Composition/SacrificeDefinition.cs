using System;

/// <summary>C03 제물의 보너스 수령 후 지출과 런 공격력 성장.</summary>
[Serializable]
public sealed class SacrificeDefinition : SelectionEffectDefinition
{
    /// <summary>처치 보상 수령 직후 지불할 초.</summary>
    public float CostSeconds = 10f;
    /// <summary>성공 결제당 가산할 런 공격력 비율.</summary>
    public float AttackBonusPerPayment = 0.02f;
}
