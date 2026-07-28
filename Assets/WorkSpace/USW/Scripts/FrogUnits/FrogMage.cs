/// <summary>
/// [파티원] 용사 파티 — 마법사 — "용사 캐릭터 기획.pdf" 5p~7p 기반.
///
/// 컨셉: 화염 마법에 특화된 거만한 엘리트 마법사. 직업 분류는 [마법사](UnitTribe.Mage).
///
/// 기본 공격 (투사체 — 매직 미사일):
///   unitData.basicAttackData(SD_매직미사일, MultiShotSkillAction+AtkCoefficientDamage)로 데이터
///   정의되어 있습니다 (atk 비례 피해, coefficient=1).
///
/// 액티브 스킬 (화염구, 쿨타임 unitData.skillCooldown):
///   기획서 문구: "공격력 비례 피해 방식이 아닌, 스킬 자체가 공격력을 가지고 있는 형태로
///   강화, 상승효과를 따로 받는다" — 즉 스킬 데미지는 unitData.atk에서 파생되는 값이 아니라
///   티어별 고정 수치(노말 500 / 레어 1500 / 에픽 2500 / 레전드 3500)입니다.
///
///   unitData.skillData(MultiShotSkillAction + FixedDamage)로 데이터 정의되어 있습니다.
///   FixedDamage.power(500/1500/2500/3500, 티어별)를 UnitStatsModifier.ComputeDamage와
///   동일한 보정 파이프라인에 통과시켜 데미지로 사용합니다. 스킬 투사체 크기(150%~300%, 티어별)도 같은 SkillData의 sizeMultiplier로
///   표현되어 있어 별도 코드가 필요 없습니다(단, 화염구 전용 VFX 프리팹/이펙트 에셋은 아직
///   없어 ProjectileData_Mage에는 비워두었습니다).
/// </summary>
public class FrogMage : UnitBase
{
}
