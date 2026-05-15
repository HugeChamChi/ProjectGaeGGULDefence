public enum LevelUpEffectType
{
    None,
    AttackPercent,          // 전체 공격력 N% (양수=증가, 음수=감소)
    AttackSpeedPercent,     // 전체 공격속도 N% (양수=빠름, 음수=느림)
    TotemEfficiencyPercent, // 토템 효율 N%
    CritChancePercent,      // 치명타 확률 N%
    CritDamagePercent,      // 치명타 데미지 N%
    FoodProductionPercent,  // 식량 생산속도 N% (양수=빠름, 음수=느림)
    ProjectileSizePercent,  // 투사체 크기 N%
    GaugeSpeedPercent,      // 게이지 속도(쿨타임) N% 감소
    ExpGainPercent,         // 경험치 획득량 N%
    FrontRowAttackPercent,  // 전방 2줄 공격력 N%
    BackRowAttackPercent,   // 후방 2줄 공격력 N%
    FrontRowSpeedPercent,   // 전방 2줄 공격속도 N%
    BackRowSpeedPercent,    // 후방 2줄 공격속도 N%
    ChieftainAttackPercent, // 족장 공격력 N%
}
