using UnityEngine;

/// <summary>
/// [파티원] 용사 파티 — 궁수 — "용사 캐릭터 기획.pdf" 8p~10p 기반.
///
/// 컨셉: 가죽 방어구에 투박한 활을 든 레인저형 궁수. 직업 분류는 [궁수](UnitTribe.Archer).
/// (주의: UnitTribe.Gunner/Ninja는 각각 기존 유닛(Frog_Gunner, Frog_Ninja)이 이미 사용 중인
///  값이라 재사용하지 않고 UnitTribe.Archer를 신규로 추가해 사용합니다. FrogArcher.cs 파일
///  자체는 이전에 빈 스텁(`public class FrogArcher : UnitBase { }`)이었고 어떤 UnitData
///  에셋에서도 참조되지 않는 미사용 상태였으며, 이름/컨셉이 이 PDF의 궁수와 정확히 일치하므로
///  그대로 재사용합니다.)
///
/// 기본 공격 (투사체 — 사격):
///   표준 UnitBase → UnitCombatComponent 파이프라인을 그대로 사용합니다 (atk 비례 피해).
///
/// 액티브 스킬 (연발 사격, 쿨타임 unitData.skillCooldown):
///   기획서 문구: "공격력의 100%로 {N}발을 연속으로 발사"(N = 노말 2 / 레어 3 / 에픽 4 /
///   레전드 5). 즉 스킬 데미지원은 skillAtk가 아니라 atk이며, 화살 1발당 데미지가
///   "일반 공격 1회"와 동일합니다. 이를 위해:
///     - GetSkillDamage()를 오버라이드해 GetAttackDamage()(= atk 100%, 훈련의 성과 패시브
///       등 기존 배율 포함)를 반환하도록 합니다.
///     - GetSkillShotCount()를 오버라이드해 티어별 발사 횟수(2/3/4/5)를 반환합니다.
///       (UnitCombatComponent.ExecuteSkill()이 이 값만큼 LaunchProjectile을 반복 호출합니다.
///        기본값 1을 쓰는 기존 유닛들은 동작 변화가 없습니다.)
///
/// 확인 필요 (최종 보고 참고):
///   1. 스킬 화살의 투사체 "크기" 증가(50%~200%, 티어별)는 현재 ProjectilePool/Projectile이
///      유닛·스킬별 개별 크기 파라미터를 지원하지 않고 TotemBuffManager.
///      ProjectileSizeMultiplier 단일 전역 배율만 적용하는 구조라(FrogMage와 동일한 사유),
///      코드 변경 없이 스킬 전용 VFX 프리팹 자체의 스케일로 구현되어야 합니다.
///   2. PDF에 스킬 쿨타임(초) 수치가 명시되어 있지 않아 unitData.skillCooldown 값은
///      데이터(시트/Inspector) 쪽에서 별도로 정해야 합니다.
/// </summary>
public class FrogArcher : UnitBase
{
    // 티어(Normal=0, Rare=1, Epic=2, Legend=3)별 '연발 사격' 발사 횟수.
    private static readonly int[] SkillShotCountByTier = { 2, 3, 4, 5 };

    public override int GetSkillDamage() => GetAttackDamage();

    public override int GetSkillShotCount()
    {
        int index = Mathf.Clamp((int)unitData.unitTier, 0, SkillShotCountByTier.Length - 1);
        return SkillShotCountByTier[index];
    }
}
