using UnityEngine;
using VContainer;

/// <summary>
/// 드론 식량 생산자 — 패시브 전용 유닛.
/// 생산량 계산과 지급 완료 연출을 담당한다. 누적·지급은 UnitResourceComponent가 소유한다.
/// 전장 전투 드론 1기당 계수를 DroneManager에 더하며, 여러 젤탕의 기당 계수는 중첩된다.
/// </summary>
public class Drone_Zeltan : UnitBase
{
    [Inject] private DroneManager _droneManager;
    [Inject] private UpgradeManager _upgradeManager;
    private SpineActorVisual _spineVisual;
    private UnitResourceComponent _foodResource;
    private UnitDependencies _dependencies;
    private int _payoutCount;
    public override float NextFoodPayoutMultiplier()
    {
        _payoutCount++;
        return _payoutCount % 5 == 0 ? Mathf.Max(1f, LevelStat(UnitBonusStat.FoodFifthMultiplier)) : 1f;
    }
    private void OnBossKilled()
    {
        if (currentCell == null || IsStunned || IsCellSealed || _dependencies?.GameManager?.IsFinished == true) return;
        float amount=GetBaseFoodPerSecond() * (1f+LevelStat(UnitBonusStat.FoodSpeedPercent)) * LevelStat(UnitBonusStat.BossFoodSeconds);
        if (amount <= 0f) return;
        _dependencies?.CurrencyManager?.AddCurrency(amount);
        _dependencies?.CurrencyFloaterManager?.ReportFoodProduction(amount);
        PlayFoodPayoutVisual();
    }

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
    /// <summary>이 젤탕의 기당 계수로 실제 생산 가능한 드론들이 버는 초당 식량. 드론 기절·봉인·토템 보정을 반영한다.</summary>
    public float FleetFoodPerSecond => currentCell != null && !IsCellSealed ? _droneManager?.GetFleetFoodPerSecond(FoodPerDronePerSecond) ?? 0f : 0f;
    /// <summary>강화분을 제외한 기당 계수로 계산한 초당 군단 식량. 정보창의 기본값에 쓴다.</summary>
    public float FleetFoodPerSecondBeforeUpgrade => currentCell != null && !IsCellSealed ? _droneManager?.GetFleetFoodPerSecond(FoodPerDronePerSecondBeforeUpgrade) ?? 0f : 0f;
    /// <inheritdoc />
    public override float CurrentFoodProductionPerSecond => IsStunned ? 0f : base.CurrentFoodProductionPerSecond;
    /// <inheritdoc />
    public override float FoodPayoutInterval => DroneEffects?.FoodPayoutInterval ?? 1f;
    /// <inheritdoc />
    public override bool UsesExternalFoodPayout => DroneEffects?.HasBatchedFoodPayout == true;

    protected override void OnInitialized(UnitDependencies dependencies)
    {
        base.OnInitialized(dependencies);
        _dependencies = dependencies;
        _payoutCount = 0;
        _foodResource = GetComponent<UnitResourceComponent>();
    }

    /// <summary>자체·군단 생산량을 공통 정산 컴포넌트에 전달한다. 이 유닛은 지급 상태를 소유하지 않는다.</summary>
    public void TickBatchedFoodPayout(float deltaTime)
    {
        _foodResource?.TickBatchedFoodProduction(deltaTime, CurrentFoodProductionPerSecond + FleetFoodPerSecond);
    }

    protected override void OnUnitPlaced()
    {
        BossBase.OnAnyBossDied -= OnBossKilled;
        BossBase.OnAnyBossDied += OnBossKilled;
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
        BossBase.OnAnyBossDied -= OnBossKilled;
        if (_foodResource != null) _foodResource.OnFoodProduced -= PlayFoodPayoutVisual;
        _droneManager?.UnregisterFoodProducer(this);
    }

    private void OnDisable() => BossBase.OnAnyBossDied -= OnBossKilled;

    private void PlayFoodPayoutVisual()
    {
        if (DroneEffects?.HasBatchedFoodPayout == true
            && _spineVisual != null && _spineVisual.isActiveAndEnabled)
            _spineVisual.PlaySkill();
    }

    /// <summary>저작된 등급별 전투 드론 1기당 초당 식량. 강화·선택지 보정 전 값이다.</summary>
    public float AuthoredFoodPerDrone => unitData != null ? unitData.fleetFoodPerDrone.Get(currentTier) : 0f;

    /// <summary>이 젤탕이 더하는 최종 드론 1기당 초당 식량. 강화와 생산 선택지 배율을 즉시 반영한다.</summary>
    public float FoodPerDronePerSecond => (AuthoredFoodPerDrone + (_upgradeManager?.ZeltanFoodPerDroneBonus ?? 0f))
        * (1f + (DroneEffects?.FoodProductionBonus ?? 0f));

    /// <summary>강화분을 제외한 드론 1기당 초당 식량. 선택지 보정은 유지한다.</summary>
    public float FoodPerDronePerSecondBeforeUpgrade => AuthoredFoodPerDrone
        * (1f + (DroneEffects?.FoodProductionBonus ?? 0f));

    /// <summary>저작된 등급별 자체 초당 생산에 선택지 배율을 적용한다. 인게임 강화는 기당 계수만 올린다.</summary>
    public override float GetBaseFoodPerSecond()
    {
        return unitData != null ? base.GetBaseFoodPerSecond()
            * (1f + (DroneEffects?.FoodProductionBonus ?? 0f)) : 0f;
    }
}
