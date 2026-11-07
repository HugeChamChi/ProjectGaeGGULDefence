using UnityEngine;

/// <summary>기본 스킬 피해를 유지하며 공유 해킹 스택을 등급별 한도까지 소비하는 기폭 유닛.</summary>
public class Drone_Gamman : HackingDroneSpawner
{
    private float _pendingResidue;
    private int _deliveryLifetime;
    private SelfDestructDrone _delivery;
    private bool HasActiveDelivery => _delivery != null && _delivery.IsDeliveryFrom(this, _deliveryLifetime);
    /// <inheritdoc />
    public override bool CanAutoSkill => base.CanAutoSkill && !HasActiveDelivery;
    private void CancelDelivery()
    {
        if (HasActiveDelivery) RM.Destroy(_delivery.gameObject);
        _delivery = null;
    }
    /// <summary>이동/제거/재활성화 이전의 운반탄이 새 배치에서 적중하지 않게 한다.</summary>
    public int DeliveryLifetime => _deliveryLifetime;
    /// <inheritdoc />
    protected override void OnUnitRemoved() { CancelDelivery(); _deliveryLifetime++; base.OnUnitRemoved(); }
    /// <inheritdoc />
    protected override void OnDisable() { CancelDelivery(); _deliveryLifetime++; base.OnDisable(); }
    /// <summary>레벨과 선택지 배율을 반영한 실제 1회 최대 소비량.</summary>
    public int MaximumStacksConsumed => Mathf.Max(1, Mathf.FloorToInt(
        ((unitData?.Hacking != null ? unitData.Hacking.MaxStacksConsumed.Get(currentTier) : 0) + LevelConsumptionBonus)
        * (DroneEffects?.HackingConsumptionMultiplier ?? 1f)));

    public int LevelConsumptionBonus => Mathf.RoundToInt(LevelStat(currentTier switch { Tier.Normal => UnitBonusStat.HackingConsumedNormal, Tier.Rare => UnitBonusStat.HackingConsumedRare, Tier.Epic => UnitBonusStat.HackingConsumedEpic, _ => UnitBonusStat.HackingConsumedLegend }));
    /// <summary>현재 드론 수/선택지를 반영한 스택 추가 피해 배율.</summary>
    public float StackDamageMultiplier
    {
        get
        {
            var effects = DroneEffects;
            float levelMultiplier = (1f + LevelStat(UnitBonusStat.HackingStackDamagePercent)) * (effects?.HackingStackDamageMultiplier ?? 1f);
            if (effects == null) return levelMultiplier;
            float bonus = (_droneManager?.DroneCount ?? 0) * effects.StackDamageBonusPerDrone;
            return levelMultiplier * (1f + (effects.MaximumStackDamageBonus > 0f ? Mathf.Min(effects.MaximumStackDamageBonus, bonus) : bonus));
        }
    }
    /// <summary>SkillData 액션에서 현재 잔고를 확보하고 순간이동 기폭을 시작한다.</summary>
    public void CastHacking()
    {
        if (HasActiveDelivery) return;
        if (!TryGetHackingTarget(out var runtime, out var target)) return;
        var data = unitData.Hacking;
        var reservation = runtime.Reserve(target, MaximumStacksConsumed);
        if (reservation == null) return;
        float coefficient = data.BaseCoefficient.Get(currentTier)
            + reservation.DamageAmount * data.CoefficientPerStack.Get(currentTier) * StackDamageMultiplier;
        int damage = ComputeAttackDamageFrom(GetUpgradedAtk() * coefficient, 1f, out bool critical);
        // Historical definition compatibility only; the authored 9109 now supplies Deltan compression.
        var effects = DroneEffects;
        float stackCoefficient = reservation.DamageAmount * data.CoefficientPerStack.Get(currentTier) * StackDamageMultiplier;
        _pendingResidue = effects?.HasGammanResidue == true && reservation.Amount > 0 && coefficient > 0f
            ? damage * (stackCoefficient / coefficient) * effects.GammanResidueRatio : 0f;
        FlashOwnedDrones();
        if (effects?.HasGammanBomb == true && effects.GammanBombPrefab != null)
        {
            var obj = RM.Instantiate(effects.GammanBombPrefab.gameObject, transform.position, Quaternion.identity, null, true);
            var bomb = obj != null ? obj.GetComponent<SelfDestructDrone>() : null;
            if (bomb == null) { runtime.Refund(target, reservation); if (obj != null) RM.Destroy(obj); return; }
            _delivery = bomb;
            float residue = _pendingResidue; _pendingResidue = 0f;
            var source = unitData;
            bomb.InitializeDelivery(damage, critical, target, source, runtime, reservation, () =>
            {
                if (residue > 0f) runtime.AddResidue(target, residue, effects.GammanResidueDuration, effects.GammanResidueTicks, source);
            }, this);
        }
        else StartHacking(runtime, target, 0, reservation, damage, critical);
    }
    /// <inheritdoc />
    protected override void OnHackingCommitted(DroneHackingRuntime runtime, BossBase target)
    {
        float residue = _pendingResidue;
        _pendingResidue = 0f;
        var effects = DroneEffects;
        if (residue > 0f && effects != null)
            runtime.AddResidue(target, residue, effects.GammanResidueDuration, effects.GammanResidueTicks, unitData);
    }
    /// <summary>긴급 교신의 추가 기폭. 정상 충전 타이머는 초기화하지 않는다.</summary>
    public void ApplyDroneBuff() => CastHacking();
}
