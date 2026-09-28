using System;

/// <summary>Single owner of run identity, round completion and deferred penalties. Null mode preserves finite play.</summary>
public sealed class EndlessRunService : IRunStatModifiers, IDisposable
{
    private readonly RunPenaltyState _penalties;
    private readonly IEndlessRandom _random;
    private EndlessBossSchedule _bossSchedule;
    private EndlessModeData _mode;
    private bool _roundCompleted;

    /// <summary>Constructs scene-owned state using an injectable random stream.</summary>
    public EndlessRunService(IEndlessRandom random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _penalties = new RunPenaltyState(_random);
    }

    /// <summary>Monotonic identity protecting new runs from old callbacks.</summary>
    public long RunId { get; private set; }
    /// <summary>One-based round. WaveManager performs the legacy index conversion.</summary>
    public int CurrentRound { get; private set; }
    /// <summary>Whether progression callbacks can be accepted.</summary>
    public bool IsActive { get; private set; }
    /// <summary>Whether an explicitly validated endless mode owns this run.</summary>
    public bool IsEndless => _mode != null;
    /// <summary>Calculated values for the current endless encounter.</summary>
    public RuntimeBossStats BossStats { get; private set; }
    /// <summary>Last configuration/numeric failure; never converted into a victory.</summary>
    public string LastError { get; private set; } = "";
    /// <inheritdoc />
    public double AttackMultiplier => _penalties.Attack;
    /// <inheritdoc />
    public double AttackFrequencyMultiplier => _penalties.Frequency;
    /// <inheritdoc />
    public event Action OnModifiersChanged;
    /// <summary>Draw notification after completion; activates only at next round entry.</summary>
    public event Action<RunPenaltyResult> OnPenaltyReserved;
    /// <summary>Diagnostic failure; integration must stop the run without inventing a gameplay result.</summary>
    public event Action<string> OnFailed;

    /// <summary>Starts a fresh run. Invalid linked settings fail explicitly without finite fallback.</summary>
    public bool TryStart(EndlessModeData mode, out string error)
    {
        error = "A run is already active.";
        if (IsActive) return false;
        RuntimeBossStats stats = default;
        if (mode != null && !EndlessConfigurationValidator.TryValidateForRun(mode, out error))
        { LastError = error; return false; }
        var schedule = mode != null ? new EndlessBossSchedule(mode, _random) : null;
        if (schedule != null && (!schedule.TrySelect(1, out var firstBoss, out error)
            || !RuntimeBossStatsCalculator.TryCalculate(firstBoss, mode.Growth, 1, out stats, out error)))
        { LastError = error; return false; }
        if (RunId == long.MaxValue) { error = LastError = "Run identity exhausted."; return false; }
        _penalties.Reset(mode != null ? mode.PenaltyPool : null);
        _mode = mode;
        _bossSchedule = schedule;
        RunId++;
        long startedRunId = RunId;
        CurrentRound = 1;
        BossStats = stats;
        _roundCompleted = false;
        IsActive = true;
        error = LastError = "";
        OnModifiersChanged?.Invoke();
        return IsActive && RunId == startedRunId;
    }

    /// <summary>Consumes one completion per run/round; duplicate and stale calls have no side effects.</summary>
    public bool TryCompleteRound(long runId, int round)
    {
        if (!Matches(runId, round) || _roundCompleted) return false;
        if (IsEndless && round == int.MaxValue) { Fail("Round number exceeds Int32 range."); return false; }
        RunPenaltyResult result = default;
        bool draw = IsEndless && round >= _mode.PenaltyFirstApplyRound - 1
            && (round - (_mode.PenaltyFirstApplyRound - 1)) % _mode.PenaltyInterval == 0;
        if (draw && !_penalties.TryReserve(runId, round + 1, out result, out string error))
        { Fail(error); return false; }
        _roundCompleted = true;
        if (draw) OnPenaltyReserved?.Invoke(result);
        return Matches(runId, round);
    }

    /// <summary>Consumes reward completion and activates the next round exactly once.</summary>
    public bool TryAdvanceRound(long runId, int completedRound)
    {
        if (!Matches(runId, completedRound) || !_roundCompleted) return false;
        if (completedRound == int.MaxValue) { Fail("Round number exceeds Int32 range."); return false; }
        RuntimeBossStats stats = default;
        if (IsEndless)
        {
            if (!_bossSchedule.TrySelect(completedRound + 1, out var boss, out string statsError)
                || !RuntimeBossStatsCalculator.TryCalculate(boss, _mode.Growth, completedRound + 1, out stats, out statsError))
            { Fail(statsError); return false; }
        }
        if (!_penalties.TryActivate(out bool changed, out string error)) { Fail(error); return false; }
        CurrentRound++;
        BossStats = stats;
        _roundCompleted = false;
        if (changed) OnModifiersChanged?.Invoke();
        return IsActive && RunId == runId;
    }

    /// <summary>Already active stack count for one fixed penalty key.</summary>
    public int GetStackCount(string key) => _penalties.GetStackCount(key);

    /// <summary>Checks callback ownership, including stopped runs.</summary>
    public bool Matches(long runId, int round) => IsActive && RunId == runId && CurrentRound == round;

    /// <summary>Invalidates callbacks and pending selections; active values remain readable for results UI.</summary>
    public void Stop()
    {
        IsActive = false;
        _roundCompleted = false;
        _penalties.DiscardPending();
        _bossSchedule = null;
    }

    /// <summary>Stops on a detected fault without inventing a balance cap, loss or victory.</summary>
    public void Fail(string error)
    {
        if (!IsActive) return;
        LastError = error;
        Stop();
        OnFailed?.Invoke(error);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        OnModifiersChanged = null;
        OnPenaltyReserved = null;
        OnFailed = null;
    }
}
