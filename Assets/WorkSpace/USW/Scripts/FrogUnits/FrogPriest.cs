/// <summary>
/// [파티원] 용사 파티 — 사제(지원가) — "용사 캐릭터 기획.pdf" 14p~16p 기반.
///
/// 컨셉: 후드 형태의 백색 의복, 적십자 포인트 컬러, 십자가 목걸이/지팡이, 마법서를 든
/// 신비주의 사제. 직업 분류는 [지원가](UnitTribe.Support 신규 추가).
///
/// 기본 공격 (투사체 — 홀리 에로우):
///   표준 UnitBase → UnitCombatComponent 파이프라인을 그대로 사용합니다 (atk 비례 피해,
///   보스에게 직접 피해를 줍니다).
///
/// 액티브 스킬 (염원): unitData.skillData(BuffAllyInRangeSkillAction)로 데이터 정의되어
///   있습니다. "캐릭터 주변 8칸 이내에 존재하는 무작위 유닛에게 10초간 증폭 버프를
///   부여합니다"(공격력·공격속도·투사체 크기 증가율은 티어별 BuffData 에셋에 데이터로
///   들어있음) — 별도 코드가 필요 없습니다.
/// </summary>
public class FrogPriest : UnitBase
{
}
