/// <summary>chief 선택지 효과의 읽기 계약. 획득/해제/추첨 권한을 제공하지 않는다.</summary>
public interface IChiefSelectionReader
{
    /// <summary>ChiefCooldownReduction 선택지 기여.</summary>
    float ChiefCooldownReduction { get; }
    /// <summary>ChiefVolleyDelay 선택지 기여.</summary>
    float ChiefVolleyDelay { get; }
    /// <summary>ChiefVolleyDamageRatio 선택지 기여.</summary>
    float ChiefVolleyDamageRatio { get; }
    /// <summary>ChiefVolleyCount 선택지 기여.</summary>
    int ChiefVolleyCount { get; }
    /// <summary>RallyDamageBonus 선택지 기여.</summary>
    float RallyDamageBonus { get; }
    /// <summary>집결 피해 배율 정의의 활성 여부.</summary>
    bool HasRallyDamage { get; }
}
