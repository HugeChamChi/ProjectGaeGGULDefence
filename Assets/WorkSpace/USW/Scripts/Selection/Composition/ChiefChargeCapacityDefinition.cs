using System;

/// <summary>족장 스킬이 순차 충전하여 보관할 수 있는 최대 사용 횟수.</summary>
[Serializable]
public sealed class ChiefChargeCapacityDefinition : SelectionEffectDefinition
{
    /// <summary>한 번에 보관할 수 있는 충전 완료 횟수.</summary>
    public int MaximumCharges;
}
