/// <summary>economy 선택지 효과의 읽기 계약. 획득/해제/추첨 권한을 제공하지 않는다.</summary>
public interface ISelectionEconomyReader
{
    /// <summary>TotemEfficiencyBonus 선택지 기여.</summary>
    float TotemEfficiencyBonus { get; }
    /// <summary>FoodSpeedBonus 선택지 기여.</summary>
    float FoodSpeedBonus { get; }
    /// <summary>ExpGainMultiplier 선택지 기여.</summary>
    float ExpGainMultiplier { get; }
    /// <summary>SummonDiscountRate 선택지 기여.</summary>
    float SummonDiscountRate { get; }
    /// <summary>SummonFixedDiscountAmount 선택지 기여.</summary>
    float SummonFixedDiscountAmount { get; }
    /// <summary>SellBonusFoodAmount 선택지 기여.</summary>
    float SellBonusFoodAmount { get; }
    /// <summary>HasSummonDealsDamage 선택지 기여.</summary>
    bool HasSummonDealsDamage { get; }
    /// <summary>SummonDamagePct 선택지 기여.</summary>
    float SummonDamagePct { get; }
    /// <summary>HasSellDealsDamage 선택지 기여.</summary>
    bool HasSellDealsDamage { get; }
    /// <summary>SellDamagePct 선택지 기여.</summary>
    float SellDamagePct { get; }
    /// <summary>HasSellGivesRandomUnit 선택지 기여.</summary>
    bool HasSellGivesRandomUnit { get; }
    /// <summary>SellGivesUnitChance 선택지 기여.</summary>
    float SellGivesUnitChance { get; }
    /// <summary>HasMergeKeepsTribe 선택지 기여.</summary>
    bool HasMergeKeepsTribe { get; }
    /// <summary>HasAllowTotemOverlap 선택지 기여.</summary>
    bool HasAllowTotemOverlap { get; }
    /// <summary>MergeSupportChance 선택지 기여.</summary>
    float MergeSupportChance { get; }
    /// <summary>출처 카드별 강화 할인.</summary>
    System.Collections.Generic.IReadOnlyDictionary<int,float> UpgradeDiscounts { get; }
}
