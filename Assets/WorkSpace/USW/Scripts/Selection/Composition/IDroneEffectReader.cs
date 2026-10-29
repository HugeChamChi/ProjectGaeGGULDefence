/// <summary>drone 선택지 효과의 읽기 계약. 획득/해제/추첨 권한을 제공하지 않는다.</summary>
public interface IDroneEffectReader
{
    /// <summary>PeriodicBombInterval 선택지 기여.</summary>
    float PeriodicBombInterval { get; }
    /// <summary>PeriodicBombCount 선택지 기여.</summary>
    int PeriodicBombCount { get; }
    /// <summary>FleetBombInterval 선택지 기여.</summary>
    float FleetBombInterval { get; }
    /// <summary>FleetBombCount 선택지 기여.</summary>
    int FleetBombCount { get; }
    /// <summary>DroneAttackSpeedBonus 선택지 기여.</summary>
    float DroneAttackSpeedBonus { get; }
    /// <summary>전투 드론 1기당 감망 스택 추가 피해 증가 비율.</summary>
    float StackDamageBonusPerDrone { get; }
    /// <summary>스택 추가 피해 증가 상한. 0이면 무상한.</summary>
    float MaximumStackDamageBonus { get; }
    /// <summary>HasEmptyStackDamage 선택지 기여.</summary>
    bool HasEmptyStackDamage { get; }
    /// <summary>젤탕 자체·군단 생산의 발생분을 함께 누적해 주기적으로 지급한다.</summary>
    bool HasBatchedFoodPayout { get; }
    /// <summary>FoodPayoutInterval 선택지 기여.</summary>
    float FoodPayoutInterval { get; }
    /// <summary>FoodProductionBonus 선택지 기여.</summary>
    float FoodProductionBonus { get; }
    /// <summary>WaveFoodDividendRatio 선택지 기여.</summary>
    float WaveFoodDividendRatio { get; }
    /// <summary>HackingProductionFlat 선택지 기여.</summary>
    int HackingProductionFlat { get; }
    /// <summary>HackingProductionBonus 선택지 기여.</summary>
    float HackingProductionBonus { get; }
    /// <summary>압축 업로드의 기본 완충량. 0이면 유닛 저작값을 사용한다.</summary>
    int HackingCompressionRequiredAttacks { get; }
    /// <summary>압축 업로드의 생산량 배수. 미획득 시 1.</summary>
    int HackingCompressionMultiplier { get; }
    /// <summary>ManaRequirementReduction 선택지 기여.</summary>
    float ManaRequirementReduction { get; }
    /// <summary>마나 필요 횟수 감소 기능 존재 여부. 비율 0에도 기존 최소 1회 감소 규칙을 보존한다.</summary>
    bool HasManaRequirementReduction { get; }
    /// <summary>델탕 해킹 1회가 생산량 배수를 얻을 확률(9108 넹 !).</summary>
    float HackingProductionProcChance { get; }
    /// <summary>발동 시 생산량 배수.</summary>
    int HackingProductionProcMultiplier { get; }
    /// <summary>이전 여운 정의 호환 여부. 현행 9109는 압축 업로드를 사용한다.</summary>
    bool HasGammanResidue { get; }
    /// <summary>기폭 스택 추가 피해에 곱하는 여운 총량 비율.</summary>
    float GammanResidueRatio { get; }
    /// <summary>여운 지속 시간(초).</summary>
    float GammanResidueDuration { get; }
    /// <summary>여운 틱 수.</summary>
    int GammanResidueTicks { get; }
    /// <summary>ExplosionSkillRecoverySeconds 선택지 기여.</summary>
    float ExplosionSkillRecoverySeconds { get; }
    /// <summary>ExtraCombatDroneCount 선택지 기여.</summary>
    int ExtraCombatDroneCount { get; }
    /// <summary>OverflowManaPerCast 선택지 기여.</summary>
    int OverflowManaPerCast { get; }
    /// <summary>HasBombHacking 선택지 기여.</summary>
    bool HasBombHacking { get; }
    /// <summary>HackingStacksPerBomb 선택지 기여.</summary>
    int HackingStacksPerBomb { get; }
    /// <summary>HackingCarryoverRatio 선택지 기여.</summary>
    float HackingCarryoverRatio { get; }
    /// <summary>BonusManaChance 선택지 기여.</summary>
    float BonusManaChance { get; }
    /// <summary>BonusManaCount 선택지 기여.</summary>
    int BonusManaCount { get; }
}
