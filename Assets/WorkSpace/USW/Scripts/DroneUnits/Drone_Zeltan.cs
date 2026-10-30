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
    [Inject] private UpgradeManager _upgradeManager;
    private SpineActorVisual _spineVisual;
    private UnitResourceComponent _foodResource;

    protected override void Awake()
    {
        base.Awake();
        _spineVisual = GetComponent<SpineActorVisual>();
    }

    public override bool IsFoodProductionBuffable => false;
    public override bool CanBasicAttack => false;
    /// <summary>식량 생산 전용 유닛으로 자동·수동 스킬을 사용하지 않는다.</summary>
    public override bool CanUseSkill => false;
    /// <summary>공격하지 않는 식량 생산자는 전투 보정과 관계없이 공격력을 0으로 표시한다.</summary>
    public override float GetDisplayAttackDamage() => 0f;
    /// <summary>공격하지 않는 식량 생산자는 공격 주기도 0으로 표시한다.</summary>
    public override float GetDisplayAttackInterval() => 0f;
    /// <inheritdoc />
    public override float GetDisplayAttackDamageBeforeUpgrade() => 0f;
    /// <inheritdoc />
    public override float GetDisplayAttackIntervalBeforeUpgrade() => 0f;
    /// <summary>군단에 등록된 현재 전투 드론 수.</summary>
    public int FleetDroneCount => _droneManager?.DroneCount ?? 0;
    /// <summary>실제 생산 가능한 드론들의 초당 식량. 기절·봉인·토템 보정을 반영한다.</summary>
    public float FleetFoodPerSecond => _droneManager?.CurrentFoodProductionPerSecond ?? 0f;
    /// <summary>강화분을 제외한 현재 자체 초당 생산량. 선택지와 생산 불가 상태는 유지한다.</summary>
    public float OwnFoodPerSecondBeforeUpgrade => IsStunned || IsCellSealed || unitData == null ? 0f
        : unitData.foodProduction.Get(currentTier) * (1f + (DroneEffects?.FoodProductionBonus ?? 0f));
    /// <inheritdoc />
    public override float CurrentFoodProductionPerSecond => IsStunned ? 0f : base.CurrentFoodProductionPerSecond;
    /// <inheritdoc />
    public override float FoodPayoutInterval => DroneEffects?.FoodPayoutInterval ?? 1f;

    protected override void OnUnitPlaced()
    {
        _foodResource = GetComponent<UnitResourceComponent>();
        if (_foodResource != null)
        {
            _foodResource.OnFoodProduced -= PlayFoodPayoutVisual;
            _foodResource.OnFoodProduced += PlayFoodPayoutVisual;
        }
        if (unitData != null)
            _droneManager?.RegisterFoodProducer(this);
    }

    protected override void OnUnitRemoved()
    {
        if (_foodResource != null) _foodResource.OnFoodProduced -= PlayFoodPayoutVisual;
        _droneManager?.UnregisterFoodProducer(this);
    }

    private void PlayFoodPayoutVisual()
    {
        if (DroneEffects?.HasFoodPayoutAnimation == true
            && _spineVisual != null && _spineVisual.isActiveAndEnabled)
            _spineVisual.PlaySkill();
    }

    /// <summary>배치된 젤탕이 제공하는 드론당 군단 식량. 정기 점검을 즉시 반영한다.</summary>
    public float FoodPerDronePerSecond => (_upgradeManager != null ? _upgradeManager.ZeltanFoodPerDrone : 0f)
        * (1f + (DroneEffects?.FleetFoodProductionBonus ?? 0f));

    public override float GetBaseFoodPerSecond()
    {
        return unitData != null ? (base.GetBaseFoodPerSecond() + (_upgradeManager?.ZeltanFoodBonus ?? 0f))
            * (1f + (DroneEffects?.FoodProductionBonus ?? 0f)) : 0f;
    }
}
