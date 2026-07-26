using Alchemy.Inspector;

/// <summary>이 그룹으로 묶인 PerTierFloat/PerTierInt 필드들을 등급 탭 하나로 함께 전환한다.</summary>
public sealed class TierTabGroupAttribute : PropertyGroupAttribute
{
    public TierTabGroupAttribute(string groupPath) : base(groupPath) { }
}
