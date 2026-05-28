using UnityEngine;

/// <summary>
/// 드론 생산자 등급별 설정.
/// Normal/Rare/Epic/Legend 각각 별도 에셋으로 생성한다.
/// </summary>
[CreateAssetMenu(fileName = "DroneProducerData", menuName = "Game/DroneUnits/DroneProducerData")]
public class DroneProducerData : ScriptableObject
{
    [Header("소환 구성")]
    [Tooltip("스킬마다 1마리씩 소환, 이 수치가 최대 보유량")]
    public int maxDroneCount;
    [Tooltip("스킬마다 즉발 소환하는 자폭 드론 수 (Legend 전용)")]
    public int selfDestructCount;

    [Header("드론 스탯")]
    public float droneAtk;
    public float droneAttackInterval;

    [Header("자폭 드론")]
    public float selfDestructDamage;
    // 프리팹은 DronePool Inspector에서 설정 — 여기서 관리하지 않음
}
