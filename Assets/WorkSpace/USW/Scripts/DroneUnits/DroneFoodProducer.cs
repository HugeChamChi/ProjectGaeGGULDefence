using UnityEngine;
using VContainer;

/// <summary>
/// 드론 식량 생산자 — 패시브 전용 유닛.
/// 배치 시 DroneManager의 드론당 식량 기여를 높은 수치로 교체하고,
/// 자체 생산량을 UnitBase의 식량 틱 루프를 통해 매초 공급한다.
/// </summary>
public class DroneFoodProducer : UnitBase
{
    [Inject] private DroneManager _droneManager;

    [Header("Food Production Settings")]
    [SerializeField] private float foodPerDronePerSec = 1f;

    protected override bool IsFoodProductionBuffable => false;
    protected override bool CanBasicAttack => false;

    protected override void OnUnitPlaced()
    {
        if (unitData != null)
            _droneManager?.SetPerDroneFood(foodPerDronePerSec);
    }

    protected override void OnUnitRemoved()
    {
        _droneManager?.ResetPerDroneFood();
    }

    protected override float GetBaseFoodPerSecond()
    {
        return unitData != null ? unitData.foodProduction : 0f;
    }
}
