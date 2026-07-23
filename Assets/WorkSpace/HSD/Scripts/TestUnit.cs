/// <summary>
/// SkillTestScene 전용 테스트 유닛. 특정 히어로에 종속되지 않는 빈 UnitBase 구현체로,
/// unitData.skillData에 원하는 SkillData를 자유롭게 연결해 테스트할 수 있다.
/// 스킬만 순수하게 테스트할 수 있도록 자동 공격/자동 스킬 발동은 꺼두고, 스킬은 스킬실행
/// 버튼(UnitCombatComponent.TriggerSkillManually)으로만 나가도록 한다.
/// </summary>
public class TestUnit : UnitBase
{
    public override bool CanBasicAttack => false;
    public override bool CanAutoSkill => false;
}
