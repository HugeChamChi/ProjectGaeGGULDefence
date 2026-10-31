/// <summary>
/// 상태 팝업으로 알리는 효과 종류. 문구·색·화살표는 StatusPopupCatalog가 정한다.
/// 직렬화된 카탈로그가 정수값을 쓰므로 값은 바꾸지 않는다.
/// </summary>
public enum StatusPopupKind
{
    AttackUp = 0,
    AttackDown = 1,
    SpeedUp = 2,
    SpeedDown = 3,
    AttackDisabled = 4,
    CritChanceUp = 5,
    CritDamageUp = 6,
    SkillChargeUp = 7,
    FoodUp = 8,
    Burn = 20,
    ArmorBreak = 21,
    DamageTakenUp = 22,
}
