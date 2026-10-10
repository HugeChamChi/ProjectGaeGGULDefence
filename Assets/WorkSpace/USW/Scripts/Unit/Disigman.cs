using UnityEngine;

/// <summary>A direct-fire unit that restores time after charging with its own basic attacks.</summary>
public sealed class Disigman : UnitBase
{
    private UnitDependencies _dependencies;
    private int _chargedAttacks;

    /// <summary>Current tier's authored attack requirement, clamped to at least one.</summary>
    public int RequiredAttacks => Mathf.Max(1, unitData?.Disigman != null
        ? unitData.Disigman.AttacksToCharge.Get(currentTier) : 1);
    /// <summary>Current charge; movement preserves it and a new instance starts empty.</summary>
    public int ChargedAttacks => Mathf.Min(_chargedAttacks, RequiredAttacks);
    /// <summary>Seconds restored immediately on the next accepted cast.</summary>
    public float SecondsRecovered => unitData?.Disigman != null
        ? Mathf.Max(0f, unitData.Disigman.SecondsRecovered.Get(currentTier)) : 0f;
    /// <inheritdoc />
    public override bool UsesTimedSkillCharge => false;
    /// <inheritdoc />
    public override bool HasSkillCooldownGauge => CanUseSkill && unitData?.Disigman != null;
    /// <inheritdoc />
    public override float SkillGaugeProgress => ChargedAttacks / (float)RequiredAttacks;
    /// <inheritdoc />
    public override bool IsSkillChargeReady(float elapsed, float interval) =>
        unitData?.Disigman != null && ChargedAttacks >= RequiredAttacks;

    /// <inheritdoc />
    protected override void OnInitialized(UnitDependencies dependencies)
    {
        _dependencies = dependencies;
        _chargedAttacks = 0;
        Subscribe();
    }

    private void Subscribe()
    {
        if (Combat == null) return;
        Combat.OnBasicAttackStarted -= Charge;
        Combat.OnBasicAttackStarted += Charge;
    }

    // The publisher is our own component and has exactly the same lifetime.
    // Keep the subscription across moves so their immediate first attack is counted too.
    private bool CanAct => isActiveAndEnabled && currentCell != null && !IsStunned && !IsCellSealed
        && !currentCell.Model.IsAttackDisabled && !currentCell.Model.TotemAttackDisabled
        && Combat != null && !Combat.AttacksHeld;

    private void Charge(BasicAttackReplay attack)
    {
        // Drone, shadow and bonus attacks must never charge this unit.
        if (!CanAct || unitData?.Disigman == null || attack == null
            || !ReferenceEquals(attack, Combat.CurrentBasicAttack)) return;
        _chargedAttacks = Mathf.Min(RequiredAttacks, _chargedAttacks + 1);
    }

    /// <summary>Accept one fully charged cast and restore time before its visual/projectile starts.</summary>
    public bool TryCast()
    {
        var target = _dependencies?.BossManager?.CurrentBoss ?? Boss;
        if (!CanUseSkill || !CanAct || !IsSkillChargeReady(0f, 0f)
            || target == null || target.IsDead || _dependencies?.Timer == null) return false;
        _chargedAttacks = 0;
        // Tagged as a hack so the timer shows the glitch presentation (signal leaves from this unit).
        _dependencies.Timer.AddTime(SecondsRecovered, TimeAddSource.Hack, transform.position);
        return true;
    }
}
