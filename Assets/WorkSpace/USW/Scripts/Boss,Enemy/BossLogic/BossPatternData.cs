using UnityEngine;

/// <summary>
/// 보스 패턴 종류
/// </summary>
public enum BossPatternType
{
    DamageReduction,   // 범위 내 기물 데미지 감소
    SpeedReduction,    // 범위 내 기물 공격속도 감소
    AttackDisable,     // 범위 내 기물 공격 불가
    DestroyUnit,       // 범위 내 기물 파괴
    SealCell,          // 범위 내 셀 봉인 (배치 불가)
}

/// <summary>
/// 패턴 발동 트리거 방식
/// </summary>
public enum PatternTriggerType
{
    Timer,   // 주기적 자동 발동
    Phase,   // HP 구간 도달 시 발동
    Both,    // 타이머 + 페이즈 혼합
}

/// <summary>
/// 패턴 범위 지정 방식
/// </summary>
public enum PatternRangeType
{
    Fixed,   // 보스마다 고정된 셀 좌표
    Random,  // 매 발동마다 랜덤 선택
}

/// <summary>
/// 보스 1개 패턴의 데이터 정의 (ScriptableObject)
/// BossData.patterns[] 배열에 등록하여 사용
/// </summary>
[CreateAssetMenu(fileName = "BossPatternData", menuName = "Game/BossPatternData")]
public class BossPatternData : ScriptableObject
{
    [Header("패턴 기본 정보")]
    public BossPatternType  patternType;
    public string           patternName;
    [TextArea] public string description;

    [Header("발동 트리거")]
    public PatternTriggerType triggerType;

    [Tooltip("Timer/Both 일 때 — 발동 주기 (초)")]
    public float interval     = 5f;

    [Tooltip("Phase/Both 일 때 — HP 비율 (0.7 = 70% 이하일 때 발동)")]
    [Range(0f, 1f)]
    public float hpThreshold  = 0.5f;

    [Header("범위")]
    public PatternRangeType rangeType;

    [Tooltip("Fixed 일 때 — 영향받는 셀 좌표 목록 (x, y)")]
    public Vector2Int[] fixedCells;

    [Tooltip("Random 일 때 — 랜덤으로 선택할 셀 수")]
    public int randomCellCount = 3;

    [Header("효과 수치")]
    [Tooltip("데미지 감소율 (0.3 = 30% 감소)")]
    [Range(0f, 1f)]
    public float damageReductionRate = 0f;

    [Tooltip("속도 감소율 (0.3 = 30% 느려짐)")]
    [Range(0f, 1f)]
    public float speedReductionRate  = 0f;

    [Tooltip("지속 시간 (초) — 0이면 영구 또는 웨이브 종료까지")]
    public float duration            = 3f;
}
