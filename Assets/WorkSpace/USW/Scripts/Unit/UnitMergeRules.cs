/// <summary>토템 임시 레벨을 제외한 기본 레벨 기준 합성 조건. 기존 비레벨 유닛은 등급 규칙을 유지한다.</summary>
public static class UnitMergeRules
{
    /// <summary>레벨 유닛은 동일 데이터와 기본 레벨을 비교한다. 토템 보너스와 자기 자신은 합성 재료로 쓰지 않는다.</summary>
    public static bool CanPair(UnitBase first, UnitBase second)
    {
        if (first == null || second == null || first == second ||
            first.OriginalData == null || second.OriginalData == null) return false;
        if (first.UsesTotemLevels || second.UsesTotemLevels)
            return first.UsesTotemLevels && second.UsesTotemLevels &&
                first.OriginalData == second.OriginalData && first.BaseLevel == second.BaseLevel &&
                first.BaseLevel < UnitBase.MaxUnitLevel;
        var tier = first.OriginalTier;
        if (tier < Tier.Normal || tier >= Tier.Legend || second.OriginalTier != tier) return false;
        if (first.IsWildcardMergeUnit || second.IsWildcardMergeUnit)
            return tier == Tier.Normal && first.IsWildcardMergeUnit != second.IsWildcardMergeUnit;
        return first.OriginalData == second.OriginalData;
    }
}
