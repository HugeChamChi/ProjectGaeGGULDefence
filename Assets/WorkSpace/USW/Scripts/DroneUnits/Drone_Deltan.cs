using UnityEngine;

/// <summary>소유 드론의 일반공격으로 마나를 충전하고 완충 시 해킹 스택을 생산한다.</summary>
public class Drone_Deltan : HackingDroneSpawner
{
    private int _attackCount;
    private float _productionRemainder;
    /// <summary>마나 카드까지 반영한 필요 공격 횟수.</summary>
    public int RequiredAttacks
    {
        get
        {
            int baseline = Mathf.Max(1, unitData?.Hacking != null ? unitData.Hacking.AttacksToCharge.Get(currentTier) : 1);
            var card = DroneSelections?.Get(DroneSelectionKind.DeltanCooldown);
            return card == null ? baseline : Mathf.Max(1, baseline - Mathf.Max(1, Mathf.RoundToInt(baseline * card.Value)));
        }
    }
    /// <summary>현재 모은 유효 일반공격 수.</summary>
    public int ChargedAttacks => Mathf.Min(_attackCount, RequiredAttacks);
    /// <summary>선택지와 이월 소수량을 포함한 다음 시전의 생산량. 조회는 상태를 바꾸지 않는다.</summary>
    public int NextProduction => Mathf.FloorToInt(ExactProduction);
    private float ExactProduction => ((unitData?.Hacking != null ? unitData.Hacking.StacksProduced.Get(currentTier) : 0)
        + (DroneSelections?.Get(DroneSelectionKind.DeltanDefenseReduction)?.Count ?? 0))
        * (1f + (DroneSelections?.Get(DroneSelectionKind.DeltanDamageTaken)?.Value ?? 0f)) + _productionRemainder;
    /// <inheritdoc />
    public override bool UsesTimedSkillCharge => false;
    /// <inheritdoc />
    public override bool HasSkillCooldownGauge => CanUseSkill && unitData?.Hacking != null;
    /// <inheritdoc />
    public override float SkillGaugeProgress => ChargedAttacks / (float)RequiredAttacks;
    /// <inheritdoc />
    public override bool IsSkillChargeReady(float elapsed, float interval) => ChargedAttacks >= RequiredAttacks;
    /// <inheritdoc />
    protected override bool ApplySkillDebuff() => false;
    /// <inheritdoc />
    protected override void OnUnitPlaced()
    {
        if (IsFirstPlacement) { _attackCount = 0; _productionRemainder = 0; }
        if (Combat != null) { Combat.OnBasicAttackStarted -= Charge; Combat.OnBasicAttackStarted += Charge; }
        base.OnUnitPlaced();
    }
    private void Charge(BasicAttackReplay attack)
    {
        if (isActiveAndEnabled && currentCell != null && unitData?.Hacking != null)
        {
            var zombie=DroneSelections?.Get(DroneSelectionKind.DeltanZombiePc);
            int extra=zombie!=null && Random.value<zombie.Value ? zombie.Count : 0;
            AddMana(1+extra);
        }
    }
    private void AddMana(int amount) => _attackCount=Mathf.Min(RequiredAttacks,_attackCount+Mathf.Max(0,amount));
    /// <inheritdoc />
    protected override void OnHackingProduced(int requested,int accepted)
    {
        var backdoor=DroneSelections?.Get(DroneSelectionKind.DeltanBackdoor);
        if(backdoor!=null) AddMana((requested-accepted)*backdoor.Count);
    }
    /// <inheritdoc />
    protected override void OnUnitRemoved()
    {
        if (Combat != null) Combat.OnBasicAttackStarted -= Charge;
        base.OnUnitRemoved();
    }
    /// <summary>SkillData 액션에서 호출한다. 수동 호출도 완충을 우회하지 않는다.</summary>
    public void CastHacking()
    {
        if (ChargedAttacks < RequiredAttacks || !TryGetHackingTarget(out var runtime, out var target)) return;
        _attackCount = 0;
        float exact = ExactProduction;
        int produced = Mathf.FloorToInt(exact);
        _productionRemainder = exact - produced;
        _audioManager?.PlaySFX("05.Drone_Debuff");
        FlashOwnedDrones();
        StartHacking(runtime, target, produced, null, 0, false);
    }
}
