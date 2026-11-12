using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Chronological hacking charge, contact and return; SkillData remains the cast entry point.</summary>
public abstract class HackingDroneSpawner : DroneSpawnerBase, IDroneCombatParticipant
{
    private sealed class CastState
    {
        internal DroneHackingRuntime Runtime;
        internal BossBase Target;
        internal HackingStackLedger.Reservation Reservation;
        internal int Generation, Produced, Damage, StartFrame;
        internal bool Critical, Contacted, Committed;
        internal double Elapsed, Contact, Recovery;
    }
    private CastState _cast;
    private HackingCastVisual _castVisual;
    private DroneCombatClock _clock;
    private bool _automatic;
    /// <summary>Current cast owns its reservation until contact, refund or cancellation.</summary>
    public bool IsHacking => _cast != null;
    /// <summary>The shared ledger's actual authored and selected capacity.</summary>
    public int SharedStackCapacity => _droneManager != null ? _droneManager.Hacking.Ledger.Capacity
        : (unitData?.Hacking?.CurrentCapacity ?? 0) + (DroneEffects?.HackingCapacityBonus ?? 0);
    /// <inheritdoc />
    public override bool CanUseSkill => unitData?.Hacking != null && unitData.skillData != null;
    /// <inheritdoc />
    public override bool CanAutoSkill => base.CanAutoSkill && !IsHacking;
    /// <inheritdoc />
    public override bool UsesExternalSkillClock => true;
    private void RegisterClock()
    {
        _clock = _droneManager?.CombatClock;
        _clock?.Register(this);
    }
    /// <inheritdoc />
    protected override void OnUnitPlaced()
    {
        base.OnUnitPlaced();
        _automatic = true;
        RegisterClock();
    }
    /// <inheritdoc />
    protected override void OnEnable()
    {
        base.OnEnable();
        if (currentCell != null) { _automatic = true; RegisterClock(); }
    }
    /// <summary>Accept a cast only for the original live target and an actionable unit.</summary>
    protected bool TryGetHackingTarget(out DroneHackingRuntime runtime, out BossBase target)
    {
        runtime = null; target = null;
        if (IsHacking || unitData?.Hacking == null || _droneManager == null || !CanContinue() || IsHeld()) return false;
        runtime = _droneManager.Hacking;
        runtime.Configure(unitData.Hacking);
        target = runtime.Target;
        return target != null && target.IsTargetable;
    }
    private bool CanContinue() => isActiveAndEnabled && currentCell != null;
    private bool IsHeld() => Combat == null || Combat.AttacksHeld || IsStunned || currentCell == null
        || currentCell.Model.IsSealed || currentCell.Model.IsAttackDisabled || currentCell.Model.TotemAttackDisabled;
    private bool CastTargetChanged(CastState cast) => !CanContinue() || cast.Target == null || cast.Runtime == null
        || cast.Runtime.Target != cast.Target || cast.Runtime.Ledger.Generation != cast.Generation;
    /// <summary>Schedule one cast; contact and return share the exact same combat timeline as basic shots.</summary>
    protected void StartHacking(DroneHackingRuntime runtime, BossBase target, int produced,
        HackingStackLedger.Reservation reservation, int damage, bool critical)
    {
        CancelHacking();
        RegisterClock();
        if (_clock == null) { if (reservation != null) runtime.Refund(target, reservation); return; }
        var data = unitData.Hacking;
        _cast = new CastState
        {
            Runtime = runtime, Target = target, Reservation = reservation, Generation = runtime.Ledger.Generation,
            StartFrame = _clock.IsAdvancing ? Time.frameCount - 1 : Time.frameCount,
            Produced = produced, Damage = damage, Critical = critical,
            Contact = Math.Max(0, reservation != null ? data.ConsumerContactSeconds : data.HackFlightSeconds),
            Recovery = Math.Max(0, reservation != null ? data.ConsumerRecoverySeconds : data.RecoverySeconds)
        };
        if (_castVisual == null) _castVisual = gameObject.AddComponent<HackingCastVisual>();
        _castVisual.Begin(OwnedDroneCount > 0 ? OwnedDrones[0] : null, target, data,
            reservation != null, _audioManager, reservation?.Amount > 0);
    }
    /// <summary>Next unit-owned timed state boundary, independent of the hacking cast's hold policy.</summary>
    protected virtual double NextTimedStateEventIn => double.PositiveInfinity;
    /// <summary>Advance unit-owned timed state with the same elapsed combat slice as shots and hacking.</summary>
    protected virtual void AdvanceTimedState(double seconds) { }
    /// <summary>Complete a due timed state boundary before any simultaneous cast or shot.</summary>
    protected virtual bool ProcessTimedStateEvent() => false;
    double IDroneCombatParticipant.NextCombatEventIn => Math.Min(NextHackingEventIn, NextTimedStateEventIn);
    private double NextHackingEventIn
    {
        get
        {
            if (_cast != null)
            {
                if (CastTargetChanged(_cast)) return 0;
                if (IsHeld() || _cast.StartFrame >= Time.frameCount) return double.PositiveInfinity;
                double boundary = _cast.Contact + (_cast.Contacted ? _cast.Recovery : 0);
                return Math.Max(0, boundary - _cast.Elapsed);
            }
            if (!_automatic || Combat?.LoopsRunning != true || !CanAutoSkill ||
                !TryGetHackingTarget(out _, out _)) return double.PositiveInfinity;
            float interval = GetCurrentSkillInterval();
            return UsesTimedSkillCharge ? Math.Max(0, interval - Combat.ExactExternalSkillTimer)
                : IsSkillChargeReady(Combat.SkillTimer, interval) ? 0 : double.PositiveInfinity;
        }
    }
    int IDroneCombatParticipant.CombatEventPriority => NextTimedStateEventIn <= NextHackingEventIn + DroneCombatClock.BoundaryTolerance
        ? -1 : IsHacking ? 0 : 1;
    void IDroneCombatParticipant.AdvanceCombatTime(double seconds)
    {
        if (!CanContinue()) return;
        AdvanceTimedState(seconds);
        if (_cast != null && !IsHeld() && _cast.StartFrame < Time.frameCount) _cast.Elapsed += seconds;
        // Preserve UnitCombatComponent's existing timed-charge policy: attack disable
        // and stun can fill one charge; a pause or sealed cell freezes charge entirely.
        if (_automatic && CanUseSkill && Combat?.LoopsRunning == true && UsesTimedSkillCharge
            && !Combat.AttacksHeld && !currentCell.Model.IsSealed)
            Combat.AdvanceExternalSkillTimer(seconds, GetCurrentSkillInterval());
    }
    bool IDroneCombatParticipant.ProcessCombatEvent()
    {
        try { return NextTimedStateEventIn <= DroneCombatClock.BoundaryTolerance ? ProcessTimedStateEvent() : ProcessClockEvent(); }
        catch { CancelHacking(); throw; }
    }
    private bool ProcessClockEvent()
    {
        var cast = _cast;
        if (cast == null)
        {
            if (!_automatic || Combat?.LoopsRunning != true || !CanAutoSkill ||
                !TryGetHackingTarget(out _, out _)) return false;
            float interval = GetCurrentSkillInterval();
            if (UsesTimedSkillCharge)
            {
                if (Combat.ExactExternalSkillTimer + DroneCombatClock.BoundaryTolerance < interval) return false;
                Combat.ConsumeExternalSkillTimer(interval);
            }
            else
            {
                if (!IsSkillChargeReady(Combat.SkillTimer, interval)) return false;
                Combat.SkillTimer = 0;
            }
            SetState(UnitState.Skilling);
            Combat.TriggerSkillManually();
            if (animator != null)
            {
                animator.SetSpeed(1f);
                animator.PlaySkillAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
            // A timed Gamman skill may start an authored delivery bomb instead of a
            // hacking cast. Its consumed charge still makes progress on this clock.
            return IsHacking || UsesTimedSkillCharge;
        }
        if (CastTargetChanged(cast)) { CancelHacking(); return true; }
        if (IsHeld()) return false;
        double boundary = cast.Contact + (cast.Contacted ? cast.Recovery : 0);
        if (cast.Elapsed + DroneCombatClock.BoundaryTolerance < boundary) return false;
        if (!cast.Contacted)
        {
            if (!cast.Target.IsTargetable) { CancelHacking(); return true; }
            cast.Contacted = true;
            if (cast.Reservation == null)
            {
                int accepted = cast.Runtime.Produce(cast.Target, cast.Produced, this);
                OnHackingProduced(cast.Produced, accepted);
                OnHackingOverflow(cast.Target, Mathf.Max(0, cast.Produced - accepted));
            }
            else if (cast.Runtime.Commit(cast.Target, cast.Reservation))
            {
                cast.Committed = true;
                cast.Target.TakeDamage(cast.Damage, cast.Target.transform.position,
                    cast.Critical ? BossDamageKind.Critical : BossDamageKind.Normal, unitData);
                OnHackingCommitted(cast.Runtime, cast.Target);
            }
        }
        else
        {
            CancelHacking();
            SetState(UnitState.Idle);
            animator?.PlayIdle();
        }
        return true;
    }
    void IDroneCombatParticipant.RenderCombatState()
    {
        var cast = _cast;
        if (cast == null) return;
        if (cast.Contacted)
        {
            _castVisual?.Draw(1);
            _castVisual?.Recover(cast.Recovery > 0 ? (float)((cast.Elapsed - cast.Contact) / cast.Recovery) : 1);
        }
        else _castVisual?.Draw(cast.Contact > 0 ? (float)(cast.Elapsed / cast.Contact) : 1);
    }
    /// <summary>Consumer follow-up after committed damage.</summary>
    protected virtual void OnHackingCommitted(DroneHackingRuntime runtime, BossBase target) { }
    /// <summary>Producer follow-up after accepted production.</summary>
    protected virtual void OnHackingProduced(int requested, int accepted) { }
    /// <summary>Producer follow-up for actual overflow at its original target.</summary>
    protected virtual void OnHackingOverflow(BossBase target, int overflow) { }
    private void CancelHacking()
    {
        var cast = _cast; _cast = null;
        if (cast?.Reservation != null && !cast.Committed && cast.Runtime != null)
            cast.Runtime.Refund(cast.Target, cast.Reservation);
        _castVisual?.Clear();
    }
    private void StopClock()
    {
        _automatic = false;
        CancelHacking();
        _clock?.Unregister(this); _clock = null;
    }
    /// <inheritdoc />
    protected override void OnUnitRemoved() { StopClock(); base.OnUnitRemoved(); }
    /// <inheritdoc />
    protected override void OnDisable() { StopClock(); base.OnDisable(); }
}
