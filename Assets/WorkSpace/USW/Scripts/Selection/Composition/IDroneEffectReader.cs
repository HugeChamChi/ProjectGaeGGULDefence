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
    /// <summary>StackSkillReduction 선택지 기여.</summary>
    float StackSkillReduction { get; }
    /// <summary>MaximumStackSkillReduction 선택지 기여.</summary>
    float MaximumStackSkillReduction { get; }
    /// <summary>HasEmptyStackDamage 선택지 기여.</summary>
    bool HasEmptyStackDamage { get; }
    /// <summary>HasFoodPayoutAnimation 선택지 기여.</summary>
    bool HasFoodPayoutAnimation { get; }
    /// <summary>FoodPayoutInterval 선택지 기여.</summary>
    float FoodPayoutInterval { get; }
    /// <summary>FoodProductionBonus 선택지 기여.</summary>
    float FoodProductionBonus { get; }
    /// <summary>FleetFoodProductionBonus 선택지 기여.</summary>
    float FleetFoodProductionBonus { get; }
    /// <summary>HackingProductionFlat 선택지 기여.</summary>
    int HackingProductionFlat { get; }
    /// <summary>HackingProductionBonus 선택지 기여.</summary>
    float HackingProductionBonus { get; }
    /// <summary>ManaRequirementReduction 선택지 기여.</summary>
    float ManaRequirementReduction { get; }
    /// <summary>마나 필요 횟수 감소 기능 존재 여부. 비율 0에도 기존 최소 1회 감소 규칙을 보존한다.</summary>
    bool HasManaRequirementReduction { get; }
    /// <summary>ExplosionSkillRecoverySeconds 선택지 기여.</summary>
    float ExplosionSkillRecoverySeconds { get; }
    /// <summary>ExtraCombatDroneCount 선택지 기여.</summary>
    int ExtraCombatDroneCount { get; }
    /// <summary>OverflowManaPerStack 선택지 기여.</summary>
    int OverflowManaPerStack { get; }
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
