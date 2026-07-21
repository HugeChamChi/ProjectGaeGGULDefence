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
///   표준 파이프라인의 UnitStatsModifier.GetSkillDamage()는 이미
///   ComputeDamage(unitData.skillAtk)를 사용하며, skillAtk는 atk와 무관한 별도 필드이자
///   UpgradeManager의 강화 곡선(atk 전용)에도 영향받지 않으므로 — 티어별 UnitData 에셋의
///   skillAtk 값을 500/1500/2500/3500으로 데이터 설정하는 것만으로 기획 요구사항이
///   그대로 충족됩니다. "훈련의 성과"(투사체 크기→데미지, LevelUpManager.
///   GetProjectileSizeAtkBonus) 패시브 역시 기본 배율(1배)로 표준 파이프라인에서 자동
///   적용되며, 기획서에도 마법사 스킬에 대한 별도 배율(리더의 2배 같은)이 명시되어 있지
///   않으므로 GetSkillDamage() 오버라이드가 필요하지 않습니다.
///
/// 확인 필요 (최종 보고 참고): 스킬 화염구의 투사체 "크기"(150%~300%, 티어별)는 현재
/// ProjectilePool/Projectile이 유닛·스킬별 개별 크기 파라미터를 지원하지 않고
/// TotemBuffManager.ProjectileSizeMultiplier 단일 전역 배율만 적용하는 구조라, 코드
/// 변경 없이 스킬 전용 VFX 프리팹 자체의 스케일로 구현되어야 합니다.
/// </summary>
public class FrogMage : UnitBase
{
}
