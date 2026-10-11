using System;

/// <summary>젤탕 지급 주기와 생산 보너스 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class FoodPayoutDefinition : SelectionEffectDefinition
{
    /// <summary>재생 간격(초).</summary>
    public float PeriodSeconds;
    /// <summary>가산 비율(0.2 = 20%).</summary>
    public float BonusRatio;
}
