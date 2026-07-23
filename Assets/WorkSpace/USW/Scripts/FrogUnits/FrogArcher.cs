/// <summary>
/// [파티원] 용사 파티 — 궁수 — "용사 캐릭터 기획.pdf" 8p~10p 기반.
///
/// 컨셉: 가죽 방어구에 투박한 활을 든 레인저형 궁수. 직업 분류는 [궁수](UnitTribe.Archer).
///
/// 기본 공격 (투사체 — 사격):
///   표준 UnitBase → UnitCombatComponent 파이프라인을 그대로 사용합니다 (atk 비례 피해).
///
/// 액티브 스킬 (연발 사격): unitData.skillData(MultiShotSkillAction)로 데이터 정의되어
///   있으며(attackPower=100%, shotCount·sizeMultiplier(투사체 크기 증가)는 티어별
///   SkillData 에셋에 각각 연결), 별도 코드가 필요 없습니다.
/// </summary>
public class FrogArcher : UnitBase
{
}
