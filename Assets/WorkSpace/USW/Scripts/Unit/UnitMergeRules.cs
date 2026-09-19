/// <summary>임시 강화와 무관한 소유 등급 기준 합성 조건.</summary>
public static class UnitMergeRules
{
    /// <summary>동일 데이터/등급 또는 노말 조커 조합인지 확인한다. 자기 자신은 제외한다.</summary>
    public static bool CanPair(UnitBase first, UnitBase second)
    {
        if (first == null || second == null || first == second ||
            first.OriginalData == null || second.OriginalData == null) return false;
        var tier = first.OriginalTier;
        if (tier < Tier.Normal || tier >= Tier.Legend || second.OriginalTier != tier) return false;
        if (first.IsWildcardMergeUnit || second.IsWildcardMergeUnit)
            return tier == Tier.Normal && first.IsWildcardMergeUnit != second.IsWildcardMergeUnit;
        return first.OriginalData == second.OriginalData;
    }
}
