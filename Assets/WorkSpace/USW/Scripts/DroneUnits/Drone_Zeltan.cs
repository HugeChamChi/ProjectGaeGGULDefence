using UnityEngine;
using VContainer;

/// <summary>
/// 드론 식량 생산자 — 패시브 전용 유닛.
/// 배치 시 DroneManager의 드론당 식량 기여를 높은 수치로 교체하고,
/// 자체 생산량을 UnitBase의 식량 틱 루프를 통해 매초 공급한다.
/// </summary>
public class Drone_Zeltan : UnitBase
{
    [Inject] private DroneManager _droneManager;

    [Header("Food Production Settings")]
    [SerializeField] private float foodPerDronePerSec = 1f;

    public override bool IsFoodProductionBuffable => false;
    public override bool CanBasicAttack => false;
    /// <summary>식량 생산 전용 유닛으로 자동·수동 스킬을 사용하지 않는다.</summary>
    public override bool CanUseSkill => false;
    /// <summary>공격하지 않는 식량 생산자는 전투 보정과 관계없이 공격력을 0으로 표시한다.</summary>
    public override float GetDisplayAttackDamage() => 0f;
    /// <summary>공격하지 않는 식량 생산자는 공격 주기도 0으로 표시한다.</summary>
    public override float GetDisplayAttackInterval() => 0f;
    /// <inheritdoc />
    public override float FoodPayoutInterval => DroneSelections?.Get(DroneSelectionKind.ZeltanAirFryer)?.Interval ?? 1f;

    protected override void OnUnitPlaced()
    {
        if (unitData != null)
            _droneManager?.RegisterFoodProducer(this);
    }

    protected override void OnUnitRemoved()
    {
        _droneManager?.UnregisterFoodProducer(this);
    }

    /// <summary>배치된 젤탕이 제공하는 드론당 군단 식량. 정기 점검을 즉시 반영한다.</summary>
    public float FoodPerDronePerSecond => foodPerDronePerSec
        * (1f + (DroneSelections?.Get(DroneSelectionKind.ZeltanMaintenance)?.Value ?? 0f));

    public override float GetBaseFoodPerSecond()
    {
        return unitData != null ? unitData.foodProduction.Get(currentTier)
            * (1f + (DroneSelections?.Get(DroneSelectionKind.ZeltanAirFryer)?.Value ?? 0f)
                + (DroneSelections?.Get(DroneSelectionKind.ZeltanColdStorage)?.Value ?? 0f)) : 0f;
    }
}
