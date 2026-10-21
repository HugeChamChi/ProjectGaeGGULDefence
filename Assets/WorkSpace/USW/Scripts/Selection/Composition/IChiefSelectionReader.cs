/// <summary>chief 선택지 효과의 읽기 계약. 획득/해제/추첨 권한을 제공하지 않는다.</summary>
public interface IChiefSelectionReader
{
    /// <summary>족장 스킬의 최대 보관 횟수. 선택지가 없으면1회.</summary>
    int ChiefChargeCapacity { get; }
    /// <summary>긴급 교신이 소모 스택1개당 집결 기준 피해에 더할 비율.</summary>
    float ChiefHackingDamagePerStack { get; }
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
