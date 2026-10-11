/// <summary>combat 선택지 효과의 읽기 계약. 획득/해제/추첨 권한을 제공하지 않는다.</summary>
public interface ISelectionCombatReader
{
    /// <summary>AttackBonus 선택지 기여.</summary>
    float AttackBonus { get; }
    /// <summary>AttackSpeedBonus 선택지 기여.</summary>
    float AttackSpeedBonus { get; }
    /// <summary>CritChance 선택지 기여.</summary>
    float CritChance { get; }
    /// <summary>CritDamageMultiplier 선택지 기여.</summary>
    float CritDamageMultiplier { get; }
    /// <summary>ProjectileSizeBonus 선택지 기여.</summary>
    float ProjectileSizeBonus { get; }
    /// <summary>GaugeSpeedBonus 선택지 기여.</summary>
    float GaugeSpeedBonus { get; }
    /// <summary>FrontAttackBonus 선택지 기여.</summary>
    float FrontAttackBonus { get; }
    /// <summary>BackAttackBonus 선택지 기여.</summary>
    float BackAttackBonus { get; }
    /// <summary>FrontSpeedMultiplier 선택지 기여.</summary>
    float FrontSpeedMultiplier { get; }
    /// <summary>BackSpeedMultiplier 선택지 기여.</summary>
    float BackSpeedMultiplier { get; }
    /// <summary>RandomExtraAttackChance 선택지 기여.</summary>
    float RandomExtraAttackChance { get; }
    /// <summary>HasRandomProcAttack 선택지 기여.</summary>
    bool HasRandomProcAttack { get; }
    /// <summary>RandomProcChance 선택지 기여.</summary>
    float RandomProcChance { get; }
    /// <summary>RandomProcDamagePct 선택지 기여.</summary>
    float RandomProcDamagePct { get; }
    /// <summary>HasExtraAttackEveryAttack 선택지 기여.</summary>
    bool HasExtraAttackEveryAttack { get; }
    /// <summary>HasExtraAttackOnSkillFull 선택지 기여.</summary>
    bool HasExtraAttackOnSkillFull { get; }
    /// <summary>HasBurstOnSkillFull 선택지 기여.</summary>
    bool HasBurstOnSkillFull { get; }
    /// <summary>BurstAttackBonus 선택지 기여.</summary>
    float BurstAttackBonus { get; }
    /// <summary>BurstDurationSeconds 선택지 기여.</summary>
    float BurstDurationSeconds { get; }
    /// <summary>HasProjectileSizeScalesAtk 선택지 기여.</summary>
    bool HasProjectileSizeScalesAtk { get; }
    /// <summary>ProjectileSizeAtkPerUnit 선택지 기여.</summary>
    float ProjectileSizeAtkPerUnit { get; }
    /// <summary>공격 횟수 트리거 목록.</summary>
    System.Collections.Generic.IReadOnlyList<int> BonusAttackEveryNHits { get; }
    /// <summary>행 공격 배율.</summary>
    float GetRowAttackMultiplier(int row, int rows);
    /// <summary>행 공격 빈도 배율.</summary>
    float GetRowSpeedMultiplier(int row, int rows);
}
