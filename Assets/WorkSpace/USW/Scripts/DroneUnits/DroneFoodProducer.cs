using UnityEngine;
using VContainer;

/// <summary>
/// 드론 식량 생산자 — 패시브 전용 유닛.
/// 배치 시 DroneManager의 드론당 식량 기여를 높은 수치로 교체하고,
/// 자체 고정 생산량을 UnitBase의 식량 틱 루프를 통해 매초 공급한다.
/// </summary>
public class DroneFoodProducer : UnitBase
{
    [Inject] private DroneManager _droneManager;

    [SerializeField] private DroneFoodProducerData[] _dataByTier; // 0=Normal 1=Rare 2=Epic 3=Legend

    private DroneFoodProducerData Data =>
        unitData != null && _dataByTier != null && (int)unitData.unitTier < _dataByTier.Length
            ? _dataByTier[(int)unitData.unitTier] : null;

    protected override void OnUnitPlaced()
    {
        if (Data != null)
            _droneManager?.SetPerDroneFood(Data.foodPerDronePerSec);
    }

    protected override void OnUnitRemoved()
    {
        _droneManager?.ResetPerDroneFood();
    }

    protected override float GetBaseFoodPerSecond()
    {
        return Data != null ? Data.fixedFoodPerSec : 0f;
    }
}
