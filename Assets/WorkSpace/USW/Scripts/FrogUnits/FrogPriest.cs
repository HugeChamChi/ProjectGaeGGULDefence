using UnityEngine;

/// <summary>
/// [파티원] 용사 파티 — 사제(지원가) — "용사 캐릭터 기획.pdf" 14p~16p 기반.
///
/// 컨셉: 후드 형태의 백색 의복, 적십자 포인트 컬러, 십자가 목걸이/지팡이, 마법서를 든
/// 신비주의 사제. 직업 분류는 [지원가](UnitTribe.Support 신규 추가).
/// (주의: FrogUnits 폴더의 기존 빈 스텁(FrogFarmer/FrogSpearMan, "농부"/"창병" 컨셉)과
///  FrogAlchemist(연금술사 — 스킬 시 식량 배율, 보스 공격 없음)는 이름·컨셉이 사제와
///  무관해 재사용 대상이 아니므로 이 파일을 새로 만듭니다. UnitTribe 8종
///  (UnEmployed/Gunner/Ninja/Wizard/Warrior/Mage/Archer/Rogue) 중에도 사제/지원가에
///  해당하는 값이 없어 UnitTribe.Support를 신규로 추가했습니다.)
///
/// 기본 공격 (투사체 — 홀리 에로우):
///   표준 UnitBase → UnitCombatComponent 파이프라인을 그대로 사용합니다 (atk 비례 피해,
///   보스에게 직접 피해를 줍니다).
///
/// 액티브 스킬 (염원, 쿨타임 unitData.skillCooldown):
///   기획서 문구: "캐릭터 주변 8칸 이내에 존재하는 무작위 유닛에게 10초간 증폭 버프를
///   부여합니다"(공격력·공격속도 %는 티어별로 다름). 보스에게 피해를 주는 스킬이 아니라
///   아군 대상 버프이므로, 기존 GetSkillDamage()/GetSkillShotCount() 훅만으로는 표현할 수
///   없습니다(둘 다 "보스에게 쏘는 투사체 데미지" 전제). 그래서 최소한의 새 코드를
///   추가했습니다:
///     - GetSkillShotCount()를 0으로 오버라이드해 스킬 발동 시 보스 투사체 발사를
///       억제합니다. (UnitCombatComponent.ExecuteSkill()의 `Mathf.Max(1, ...)` 강제를
///       `Mathf.Max(0, ...)`로 완화하는 1줄 수정이 필요했습니다 — 기존 유닛은 모두 기본값
///       1 이상을 반환하므로 동작 변화가 없습니다.)
///     - OnSkillFull()을 오버라이드해 자신의 currentCell 기준 8칸(체비셰프 거리, 가로/세로
///       중 큰 값) 이내에 배치된 다른 유닛 중 무작위 1체를 골라 신규 UnitBase.
///       ApplySupportBuff()(UnitBase/UnitStatsModifier에 최소 추가)를 호출합니다. 이 버프는
///       DroneBuffer(드론 버프)·리더의 burstAtk(레벨업 특수효과)와 동일한 "Time.time 기반
///       타이머 배율" 패턴을 재사용해 UnitStatsModifier.ComputeDamage()/
///       GetCurrentAttackInterval()에 곱연산으로 반영됩니다.
///
/// 확인 필요 (최종 보고 참고):
///   1. 버프의 "투사체 크기 {50~100}% 증가" 항목은 현재 프로젝트에 유닛별/타겟별 투사체
///      크기 파라미터가 없고(TotemBuffManager.ProjectileSizeMultiplier 전역 배율만 존재),
///      다른 4종 유닛(마법사/궁수/도적)의 스킬 투사체 크기와 동일한 이유로 코드
///      미구현 — VFX 프리팹 스케일로 대체되어야 합니다.
///   2. PDF에 스킬 쿨타임(초) 수치가 없어 unitData.skillCooldown 값은 데이터(시트/Inspector)
///      쪽에서 별도로 정해야 합니다.
///   3. "무작위 유닛"에 사제 자기 자신이 포함되는지 PDF에 명시가 없어, 자기 자신은
///      제외하고 다른 유닛 중에서만 선택하도록 구현했습니다 — 기획 의도와 다르면 조정이
///      필요합니다.
/// </summary>
public class FrogPriest : UnitBase
{
    [Tooltip("'염원' 버프 대상을 찾는 범위 (그리드 셀, 체비셰프 거리)")]
    [SerializeField] private int _buffRangeCells = 8;

    [Tooltip("'염원' 버프 지속시간 (초)")]
    [SerializeField] private float _buffDuration = 10f;

    // 티어(Normal=0, Rare=1, Epic=2, Legend=3)별 '염원' 공격력/공격속도 증가율.
    private static readonly float[] AtkSpeedBonusByTier = { 0.05f, 0.10f, 0.20f, 0.30f };

    public override int GetSkillShotCount() => 0;

    protected override void OnSkillFull()
    {
        base.OnSkillFull();

        var target = GetRandomAllyInRange();
        if (target == null) return;

        int index = Mathf.Clamp((int)unitData.unitTier, 0, AtkSpeedBonusByTier.Length - 1);
        float bonus = AtkSpeedBonusByTier[index];
        target.ApplySupportBuff(bonus, bonus, _buffDuration);
    }

    private UnitBase GetRandomAllyInRange()
    {
        var grid = _gridManager;
        var selfCell = currentCell;
        if (grid == null || selfCell == null) return null;

        var candidates = new System.Collections.Generic.List<UnitBase>();
        foreach (var cell in grid.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit == null || unit == this) continue;

            int dx = Mathf.Abs(cell.GridPosition.x - selfCell.GridPosition.x);
            int dy = Mathf.Abs(cell.GridPosition.y - selfCell.GridPosition.y);
            if (Mathf.Max(dx, dy) <= _buffRangeCells) candidates.Add(unit);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }
}
