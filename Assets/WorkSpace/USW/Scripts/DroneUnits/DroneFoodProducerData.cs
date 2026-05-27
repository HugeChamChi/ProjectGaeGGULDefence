using UnityEngine;

/// <summary>
/// 드론 식량 생산자 등급별 설정.
/// 배치 시 DroneManager.BaseFoodPerDrone 을 foodPerDronePerSec 로 교체한다.
/// </summary>
[CreateAssetMenu(fileName = "DroneFoodProducerData", menuName = "Game/DroneUnits/DroneFoodProducerData")]
public class DroneFoodProducerData : ScriptableObject
{
    [Tooltip("유닛 자체 고정 식량 생산 (드론 무관)")]
    public float fixedFoodPerSec;

    [Tooltip("드론 1마리당 초당 식량 기여 (기본 0.15 대체)")]
    public float foodPerDronePerSec;
}
