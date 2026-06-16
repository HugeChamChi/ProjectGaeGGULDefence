using UnityEngine;
using System.Collections.Generic;

// 토템 종류
public enum TotemType
{
    AttackBuff,  // 좌우 1칸 — 공격력 +10%,             장판색: 빨강
    SpeedBuff,   // X자 대각 4칸 — 속도 +30%,           장판색: 주황
    FoodBuff,    // 우 2칸 — 식량 생산 간격 -20%,        장판색: 초록
    AttackTop,   // 최상단 전체 — 공격력 +30%,            장판색: 분홍
    Berserk,     // 상 1칸 — 공격력+10% 속도+10%,        장판색: 빨강+주황
    United,      // 상1+하1칸 — 앞 배치 시 공격력+속도+20%, 장판색: 노랑
    OverWelm,    // 상3칸 공격불가 + 하1칸 공격력+70%,   장판색: 청록+금색
}

[CreateAssetMenu(fileName = "TotemData", menuName = "Game/TotemData")]
public class TotemData : ScriptableObject
{
    [Header("기본 정보")]
    public TotemType   totemType;
    public Tier        tier;
    public string      totemName;
    public Sprite      icon;
    [Tooltip("특수 동작 스크립트가 필요한 토템만 연결. 비워두면 TotemSpawner의 genericPrefab 사용.")]
    public GameObject  prefab;

    [Header("회전 스프라이트 (0°/90°/180°/270°)")]
    [Tooltip("각 90° 회전 상태별 스프라이트. null이면 icon 사용.")]
    public Sprite[] rotationSprites = new Sprite[4];
    [TextArea] public string description;

    public Sprite DisplaySprite
        => rotationSprites != null && rotationSprites.Length > 0 && rotationSprites[0] != null
            ? rotationSprites[0]
            : icon;

    [Header("버프 수치")]
    // AttackBuff/AttackTop/Berserk/United: 공격력 버프 비율 (0.1 = 10%)
    // OverWelm: 하1칸 공격력 증폭 비율 (0.7 = +70% → modifier 1.7)
    public float attackBuffAmount    = 0f;
    // SpeedBuff/Berserk/United: 공격속도 버프 비율 (0.3 = 30%)
    public float speedBuffAmount     = 0f;
    // FoodBuff: 식량 생산 간격 감소 비율 (0.2 = 20%)
    public float foodSpeedBuffAmount  = 0f;
    // 식량 생산량 증가 비율 (0.3 = 30%)
    public float foodAmountBuffAmount = 0f;
    // 치명타 데미지 증가 비율 (0.1 = 10%)
    public float critDamageBuffAmount = 0f;
    // 치명타 확률 증가 (0.1 = 10%)
    public float critChanceBuffAmount = 0f;

    [Header("식량 생성 (끝없는 수확 전용)")]
    [Tooltip("토템이 스스로 식량을 생성하는 주기(초). 생성량은 foodAmountBuffAmount 사용. 0 이하면 미사용.")]
    public float foodGenInterval = 10f;

    [Header("범위 데이터 (TotemEditor로 설정)")]
    [Tooltip("토템 위치 기준 상대 오프셋 — TotemEditorWindow에서 편집")]
    public List<Vector2Int> effectRange         = new List<Vector2Int>();
    [Tooltip("공격 불가 범위 — 토템 위치 기준 상대 오프셋")]
    public List<Vector2Int> attackDisabledRange = new List<Vector2Int>();

    [Header("시트 연동")]
    [Tooltip("구글 시트 totem_id 컬럼 값. 런타임 시트 데이터 매핑 키.")]
    public int  totemId     = 0;
    [Tooltip("회전 가능 여부. 시트 is_rotatable 컬럼이 있으면 런타임에 덮어쓰기됨.")]
    public bool isRotatable = true;

    /// <summary>
    /// 구글 시트에서 가져온 토템 데이터를 SO 인스턴스에 적용합니다.
    /// </summary>
    public void ApplySheetData(GameDataManager.TotemSheetRow row)
    {
        if (row == null) return;

        // 1. 기본 정보 덮어쓰기
        if (!string.IsNullOrEmpty(row.TotemName)) totemName = row.TotemName;
        tier = row.Grade;
        isRotatable = row.IsRotatable;

        // 2. 버프 수치 덮어쓰기
        attackBuffAmount = row.AtkIncreaseRate;
        speedBuffAmount = row.AttackSpeedIncreaseRate;
        foodSpeedBuffAmount = row.FoodProductionRate;
        foodAmountBuffAmount = row.FoodAmount;
        critChanceBuffAmount = row.CriticalChanceRate;
        critDamageBuffAmount = row.CriticalDamageRate;

        // 3. 범위 데이터 덮어쓰기 (시트에 데이터가 존재할 경우에만)
        if (row.EffectRange != null && row.EffectRange.Count > 0)
        {
            effectRange = new List<Vector2Int>(row.EffectRange);
        }
        
        if (row.AttackDisabledRange != null && row.AttackDisabledRange.Count > 0)
        {
            attackDisabledRange = new List<Vector2Int>(row.AttackDisabledRange);
        }
    }
}
