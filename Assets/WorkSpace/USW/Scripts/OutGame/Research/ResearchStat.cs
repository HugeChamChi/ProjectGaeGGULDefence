/// <summary>
/// 연구로 올리는 스탯 종류. 나중에 인게임(UnitStatsModifier 등)과 연결할 때 이 값으로 합계를 조회한다.
/// 직렬화된 에셋이 정수로 저장하므로 값을 바꾸지 말고 뒤에 추가만 한다.
/// </summary>
public enum ResearchStat
{
    AttackPercent = 0,
    AttackSpeedPercent = 1,
    CritChance = 2,
    CritDamage = 3,
    SkillCooldownReduction = 4,
    FoodProduction = 5,
    StartFood = 6,
    BossDamage = 7,
    BurnDamage = 8,
    TotemEffect = 9,
}
