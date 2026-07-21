// 토템/버프/패시브가 공통으로 사용하는 스탯 종류.
// 값은 에셋에 kind: N으로 저장되어 있으니 기존 값은 바꾸지 말 것 — 새 항목은 끝에 추가.
public enum StatKind
{
    AttackPercent = 0,
    Speed = 1,
    FoodSpeed = 2,
    FoodAmount = 3,
    CritChance = 4,
    CritDamage = 5,
    ProjectileSize = 6,
    GaugeSpeed = 7, // 스킬 쿨타임
    AttackFlat = 8, // 깡 공격력(고정 수치)
}
