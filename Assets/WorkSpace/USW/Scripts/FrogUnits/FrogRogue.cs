/// <summary>
/// [파티원] 용사 파티 — 도적 — "용사 캐릭터 기획.pdf" 11p~13p 기반.
///
/// 컨셉: 단검·쿠나이를 든 후드 차림의 도적. 직업 분류는 [도적](UnitTribe.Rogue).
///
/// 기본 공격 (투사체 — 쿠나이 투척):
///   unitData.basicAttackData(SD_쿠나이투척, MultiShotSkillAction+AtkCoefficientDamage)로 데이터
///   정의되어 있습니다 (atk 비례 피해, coefficient=1).
///
/// 액티브 스킬 (그림자 표창): unitData.skillData(MultiShotSkillAction+AtkCoefficientDamage)로
///   데이터 정의되어 있으며(AtkCoefficientDamage.coefficient=80%, shotCount=2,
///   sizeMultiplier=1.5(투사체 크기 50% 증가)는 Normal 티어 기준. 티어별 UnitData 에셋마다
///   각각의 SkillData를 연결), 별도 코드가 필요 없습니다.
/// </summary>
public class FrogRogue : UnitBase
{
}
