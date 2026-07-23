/// <summary>
/// [파티원] 용사 파티 — 마법사 — "용사 캐릭터 기획.pdf" 5p~7p 기반.
///
/// 컨셉: 화염 마법에 특화된 거만한 엘리트 마법사. 직업 분류는 [마법사](UnitTribe.Mage).
/// (주의: UnitTribe.Wizard는 기존 "고블린 마법사"(FrogWizard, 식량 감소 디버프) 전용
///  값이라 재사용하지 않고 UnitTribe.Mage를 신규로 추가해 사용합니다.)
///
/// 기본 공격 (투사체 — 매직 미사일):
///   표준 UnitBase → UnitCombatComponent 파이프라인을 그대로 사용합니다 (atk 비례 피해).
///
/// 액티브 스킬 (화염구, 쿨타임 unitData.skillCooldown):
///   기획서 문구: "공격력 비례 피해 방식이 아닌, 스킬 자체가 공격력을 가지고 있는 형태로
///   강화, 상승효과를 따로 받는다" — 즉 스킬 데미지는 unitData.atk에서 파생되는 값이 아니라
///   티어별 고정 수치(노말 500 / 레어 1500 / 에픽 2500 / 레전드 3500)입니다.
///
///   unitData.skillData(MultiShotSkillAction, useSkillAtk=true)로 데이터 정의되어 있습니다.
///   useSkillAtk는 caster.GetAttackDamage() 대신 caster.GetSkillDamage()(내부적으로 기존과
///   동일한 UnitStatsModifier.ComputeDamage(unitData.skillAtk) 경로)를 기준 데미지로 사용하는
///   MultiShotSkillAction의 옵션이라, 티어별 skillAtk(500/1500/2500/3500)만 UnitData 에셋에
///   데이터로 넣으면 기획 요구사항이 그대로 충족됩니다. 스킬 투사체 크기(150%~300%, 티어별)도
///   같은 SkillData의 sizeMultiplier로 표현되어 있어 별도 코드가 필요 없습니다(단, 화염구
///   전용 VFX 프리팹/이펙트 에셋은 아직 없어 ProjectileData_Mage에는 비워두었습니다).
/// </summary>
public class FrogMage : UnitBase
{
}
