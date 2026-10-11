using System;

/// <summary>젤탕 드론당 생산 보너스 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class FleetFoodProductionDefinition : SelectionEffectDefinition
{
    /// <summary>가산 비율(0.2 = 20%).</summary>
    public float BonusRatio;
}
