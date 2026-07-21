using UnityEngine;

/// <summary>
/// [파티원] 용사 파티 — 도적 — "용사 캐릭터 기획.pdf" 11p~13p 기반.
///
/// 컨셉: 단검·쿠나이를 든 후드 차림의 도적. 직업 분류는 [도적](UnitTribe.Rogue).
/// (주의: UnitTribe.Ninja는 기존 "레어 닌굴이" 계열 유닛(Frog_Ninja, characterId 1008~1011,
///  skillName "대형 수리검"/"아브다케다브라")이 이미 사용 중인 값이라, 무기 모티브(쿠나이/표창)가
///  비슷하더라도 재사용하지 않고 UnitTribe.Rogue를 신규로 추가해 사용합니다. FrogUnits 폴더의
///  다른 빈 스텁(FrogFarmer/FrogSpearMan)은 이름·컨셉이 도적과 무관해 재사용 대상이 아니므로
///  이 파일을 새로 만듭니다.)
///
/// 기본 공격 (투사체 — 쿠나이 투척):
///   표준 UnitBase → UnitCombatComponent 파이프라인을 그대로 사용합니다 (atk 비례 피해).
///
/// 액티브 스킬 (그림자 표창, 쿨타임 unitData.skillCooldown):
///   기획서 문구: 티어별로 "공격력의 {80/90/90/100}%"에 해당하는 표창을 "{2/3/4/6}개 동시에
///   투척"합니다. 즉 데미지 비율과 발사 개수가 둘 다 티어별로 다릅니다.
///     - GetSkillDamage()를 오버라이드해 GetAttackDamage()(atk 100%, 훈련의 성과 패시브 등
///       기존 배율 포함)에 티어별 계수(0.8/0.9/0.9/1.0)를 곱한 값을 반환합니다. skillAtk
///       고정값(마법사 방식) 대신 atk 비례로 처리해야 UpgradeManager의 공격력 강화가 스킬
///       데미지에도 그대로 반영됩니다(궁수와 동일한 이유).
///     - GetSkillShotCount()를 오버라이드해 티어별 동시 투척 개수(2/3/4/6)를 반환합니다.
///   기존 두 훅(GetSkillDamage, GetSkillShotCount)만으로 표현 가능해 새 훅 추가는 필요
///   없었습니다.
///
/// 확인 필요 (최종 보고 참고):
///   1. 스킬 표창의 투사체 "크기" 증가(50%~200%, 티어별)는 현재 ProjectilePool/Projectile이
///      유닛·스킬별 개별 크기 파라미터를 지원하지 않고 TotemBuffManager.
///      ProjectileSizeMultiplier 단일 전역 배율만 적용하는 구조라(FrogMage/FrogArcher와
///      동일한 사유), 코드 변경 없이 스킬 전용 VFX 프리팹 자체의 스케일로 구현되어야 합니다.
///   2. PDF에 스킬 쿨타임(초) 수치가 명시되어 있지 않아 unitData.skillCooldown 값은
///      데이터(시트/Inspector) 쪽에서 별도로 정해야 합니다.
///   3. PDF 도적 섹션에는 리더/궁수와 달리 별도의 "패시브" 항목이 없어 패시브 관련 코드는
///      추가하지 않았습니다.
/// </summary>
public class FrogRogue : UnitBase
{
    // 티어(Normal=0, Rare=1, Epic=2, Legend=3)별 '그림자 표창' 공격력 계수(공격력 대비 %).
    private static readonly float[] SkillAtkPercentByTier = { 0.8f, 0.9f, 0.9f, 1.0f };

    // 티어별 '그림자 표창' 동시 투척 개수.
    private static readonly int[] SkillShotCountByTier = { 2, 3, 4, 6 };

    public override int GetSkillDamage()
    {
        int index = Mathf.Clamp((int)unitData.unitTier, 0, SkillAtkPercentByTier.Length - 1);
        return Mathf.RoundToInt(GetAttackDamage() * SkillAtkPercentByTier[index]);
    }

    public override int GetSkillShotCount()
    {
        int index = Mathf.Clamp((int)unitData.unitTier, 0, SkillShotCountByTier.Length - 1);
        return SkillShotCountByTier[index];
    }
}
