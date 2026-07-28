public enum LevelUpSpecialEffect
{
    None = 0,

    // ── 즉시 효과 ────────────────────────────────────────────
    GiveFoodAmount = 1,             // 3015, 3029: 식량 specialValue개 즉시 획득
    PopulationIncrease = 2,         // 3023, 3045: 최대 인구수 specialValue 증가
    TriggerTotemSelection = 3,      // 3030: 토템 선택지 즉시 출력
    GainRandomUnit = 4,             // 3022: Normal~Rare 무작위 기물 획득

    // ── 영구 패시브 (UnitBase가 참조) ────────────────────────
    AttackEveryNHits = 12,           // 3016, 3020, 3043: specialValue회 공격마다 추가 공격
    RandomBonusAttack = 13,          // 3047: specialValue% 확률로 100% 추가 공격
    RandomProcAttack = 14,           // 3017: specialValue% 확률로 primaryValue% 공격력으로 추가 공격
    ExtraAttackEveryAttack = 15,     // 3053: 매 공격마다 1회 추가 공격
    ExtraAttackOnSkillFull = 16,     // 3042: 스킬 풀 시 1회 추가 공격
    BurstOnSkillFull = 17,           // 3018, 3054: 스킬 풀 → specialValue초간 공격력 primaryValue% 증가

    // ── 영구 패시브 (UnitSpawner가 참조) ────────────────────
    SummonDiscount = 18,             // 3019: 소환 비용 specialValue% 할인
    SummonFixedDiscount = 19,        // 소환 비용 specialValue만큼 고정 수치 할인
    SellBonusFood = 20,              // 3024: 판매 시 식량 specialValue개 추가
    SummonDealsDamage = 21,          // 소환 시 소환된 유닛 공격력의 specialValue% 피해
    SellDealsDamage = 22,            // 3044: 판매 시 기물 공격력 specialValue% 피해
    SellGivesRandomUnit = 23,        // 판매 시 specialValue% 확률로 노말 무작위 유닛 획득
    ChieftainGainOnSell = 24,        // 3058: 판매 시 족장 공격력+primaryValue%, 인구>2당 -secondaryValue%

    // ── 영구 패시브 (TotemSpawner가 참조) ───────────────────
    AllowTotemOverlap = 25,          // 3031: 토템 겹치기 허용

    // ── 영구 패시브 (MergeManager가 참조) ───────────────────
    MergeKeepsTribe = 26,            // 3041: 합성 시 같은 직업 유지

    // ── 특정 유닛 행동 변경 ──────────────────────────────────
    ProjectileSizeScalesAtk = 30,    // 3051: 투사체 크기 10%당 공격력 primaryValue% 증가

    // ── 파티 전용 선택지 ─────────────────────────────────────
    GrantCourageBuff = 31,           // 9001~9004: "용기" 버프 specialValue스택 전체 유닛에게 부여 (1스택=투사체 크기 10%)
}
