public enum LevelUpSpecialEffect
{
    None,

    // ── 즉시 효과 ────────────────────────────────────────────
    GiveFoodAmount,             // 3015, 3029: 식량 specialValue개 즉시 획득
    PopulationIncrease,         // 3023, 3045: 최대 인구수 specialValue 증가
    TriggerTotemSelection,      // 3030: 토템 선택지 즉시 출력
    GainRandomUnit,             // 3022: Normal~Rare 무작위 기물 획득
    GainGunnerUnit,             // 3025: Rare~Epic 총잡이 기물 획득
    GainNinjaUnit,              // 3026: Rare~Epic 닌자 기물 획득
    GainWizardUnit,             // 3027: Rare~Epic 마법사 기물 획득
    GainUnemployedUnit,         // 3028: Rare~Epic 무직 기물 획득
    BuffNinjaTribe,             // 3048: 닌자 전체 공격력+primaryValue%, 공격속도+secondaryValue%
    BuffGunnerTribe,            // 3049: 총잡이 전체 공격력+primaryValue%, 공격속도+secondaryValue%
    BuffWizardTribe,            // 3050: 마법사 전체 공격력+primaryValue%, 쿨타임-secondaryValue%

    // ── 영구 패시브 (UnitBase가 참조) ────────────────────────
    AttackEveryNHits,           // 3016, 3020, 3043: specialValue회 공격마다 추가 공격
    RandomBonusAttack,          // 3047: specialValue% 확률로 100% 추가 공격
    RandomProcAttack,           // 3017: specialValue% 확률로 primaryValue% 공격력으로 추가 공격
    ExtraAttackEveryAttack,     // 3053: 매 공격마다 1회 추가 공격
    ExtraAttackOnSkillFull,     // 3042: 스킬 풀 시 1회 추가 공격
    BurstOnSkillFull,           // 3018, 3054: 스킬 풀 → specialValue초간 공격력 primaryValue% 증가

    // ── 영구 패시브 (UnitSpawner가 참조) ────────────────────
    SummonDiscount,             // 3019: 소환 비용 specialValue% 할인
    SellBonusFood,              // 3024: 판매 시 식량 specialValue개 추가
    SellDealsDamage,            // 3044: 판매 시 기물 공격력 specialValue% 피해
    ChieftainGainOnSell,        // 3058: 판매 시 족장 공격력+primaryValue%, 인구>2당 -secondaryValue%

    // ── 영구 패시브 (TotemSpawner가 참조) ───────────────────
    AllowTotemOverlap,          // 3031: 토템 겹치기 허용

    // ── 영구 패시브 (MergeManager가 참조) ───────────────────
    MergeKeepsTribe,            // 3041: 합성 시 같은 직업 유지

    // ── 특정 유닛 행동 변경 ──────────────────────────────────
    WizardLightningMode,        // 3046: 마법사 기본공격 제거, 스킬 풀 → 번개 사용
    WizardPhysicalMode,         // 3055: 마법사 스킬 제거, 공격력+공격속도 primaryValue%
    UnemployedFoodNegate,       // 3056: 무직 식량 획득 제거, 스킬 풀마다 공격력+primaryValue
    ProjectileSizeScalesAtk,    // 3051: 투사체 크기 10%당 공격력 primaryValue% 증가
}
