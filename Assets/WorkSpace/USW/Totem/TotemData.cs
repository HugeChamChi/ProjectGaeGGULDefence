using UnityEngine;
using System.Collections.Generic;

public enum TotemRarity { Normal, Rare, Epic, Special }

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
    public TotemRarity rarity;
    public string      totemName;
    public Sprite      icon;
    public GameObject  prefab;
    [TextArea] public string description;

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

    [Header("범위 데이터 (TotemEditor로 설정)")]
    [Tooltip("토템 위치 기준 상대 오프셋 — TotemEditorWindow에서 편집")]
    public List<Vector2Int> effectRange         = new List<Vector2Int>();
    [Tooltip("공격 불가 범위 — 토템 위치 기준 상대 오프셋")]
    public List<Vector2Int> attackDisabledRange = new List<Vector2Int>();
}