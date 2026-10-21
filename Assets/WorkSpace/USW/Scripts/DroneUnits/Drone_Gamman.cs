using UnityEngine;

/// <summary>기본 스킬 피해를 유지하며 공유 해킹 스택을 등급별 한도까지 소비하는 기폭 유닛.</summary>
public class Drone_Gamman : HackingDroneSpawner
{
    private float _pendingResidue;
    /// <summary>현재 드론 수/선택지를 반영한 스택 추가 피해 배율.</summary>
    public float StackDamageMultiplier
    {
        get
        {
            var effects = DroneEffects;
            return 1f + (effects != null ? Mathf.Min(effects.MaximumStackSkillReduction, (_droneManager?.DroneCount ?? 0) * effects.StackSkillReduction) : 0f);
        }
    }
    /// <summary>SkillData 액션에서 현재 잔고를 확보하고 순간이동 기폭을 시작한다.</summary>
    public void CastHacking()
    {
        if (!TryGetHackingTarget(out var runtime, out var target)) return;
        var data = unitData.Hacking;
        var reservation = runtime.Reserve(target, data.MaxStacksConsumed.Get(currentTier));
        if (reservation == null) return;
        float coefficient = data.BaseCoefficient.Get(currentTier)
            + reservation.Amount * data.CoefficientPerStack.Get(currentTier) * StackDamageMultiplier;
        int damage = ComputeAttackDamageFrom(GetUpgradedAtk() * coefficient, 1f, out bool critical);
        // 9109 넹...: 치명·편차가 정해진 기폭 피해에서 스택 추가분 비율만큼을 여운 총량으로 잡는다.
        var effects = DroneEffects;
        float stackCoefficient = reservation.Amount * data.CoefficientPerStack.Get(currentTier) * StackDamageMultiplier;
        _pendingResidue = effects?.HasGammanResidue == true && reservation.Amount > 0 && coefficient > 0f
            ? damage * (stackCoefficient / coefficient) * effects.GammanResidueRatio : 0f;
        FlashOwnedDrones();
        StartHacking(runtime, target, 0, reservation, damage, critical);
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
