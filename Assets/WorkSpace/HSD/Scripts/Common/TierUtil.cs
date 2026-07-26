/// <summary>PerTierFloat/PerTierInt가 실제로 값을 갖는 4개 등급(족장 제외) 목록.</summary>
public static class TierUtil
{
    public static readonly Tier[] All = { Tier.Normal, Tier.Rare, Tier.Epic, Tier.Legend };

    /// <summary>PerTierFloat/PerTierInt의 normal/rare/epic/legend 필드명과 일치한다.</summary>
    public static string FieldName(Tier tier) => tier switch
    {
        Tier.Rare => "rare",
        Tier.Epic => "epic",
        Tier.Legend => "legend",
        _ => "normal",
    };
}
