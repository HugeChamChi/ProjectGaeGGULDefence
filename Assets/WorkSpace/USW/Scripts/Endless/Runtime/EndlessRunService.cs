using System;
using System.Collections.Generic;

/// <summary>Single owner of run identity, round completion and deferred penalties. Null mode preserves finite play.</summary>
public sealed class EndlessRunService : IRunStatModifiers, IDisposable
{
    private readonly RunPenaltyState _penalties;
    private readonly IEndlessRandom _random;
    private EndlessBossSchedule _bossSchedule;
    private EndlessModeData _mode;
    private EndlessGrowthSettings _growth;
    private EndlessBossProgressionSnapshot? _bossProgression;
    private EndlessBalanceSnapshot _remoteBalance;
    private Dictionary<BossData, string> _bossKeys;
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
    /// <summary>Frozen role settings for diagnostics; null only for legacy profiles.</summary>
    public EndlessBossProgressionSnapshot? BossProgression => _bossProgression;
    /// <summary>Last configuration/numeric failure; never converted into a victory.</summary>
    public string LastError { get; private set; } = "";
    /// <inheritdoc />
    public double AttackMultiplier => _penalties.Attack;
    /// <summary>획득 경험치 배율.</summary>
    public double ExperienceMultiplier => _penalties.Experience;
    /// <summary>기본 후보 수에서 활성 규칙형 감소량을 뺀다.</summary>
    public int GetChoiceCount(int baseline) => Math.Max(1, baseline - _penalties.ChoiceReduction);
    /// <summary>영구 봉인 규칙이 적용되었는지 여부.</summary>
    public bool HasPermanentSeal => _penalties.PermanentSeal;
    /// <summary>현재 제시된 서로 다른 두 후보.</summary>
    public System.Collections.Generic.IReadOnlyList<RunPenaltyData> PenaltyChoices => _penalties.Offered;
    /// <summary>유효한 선택이 오기 전 다음 라운드 진행을 막는다.</summary>
    public bool AwaitingPenaltyChoice => _penalties.AwaitingChoice;
    /// <summary>현재 런/라운드의 제시된 후보만 한 번 선택한다.</summary>
    public bool TryChoosePenalty(long runId, int round, string key)
    {
        if (!Matches(runId, round) || !_roundCompleted || !_penalties.TryChoose(runId, round+1, key, out var result)) return false;
        OnPenaltyReserved?.Invoke(result); return true;
    }
    /// <inheritdoc />
    public double AttackFrequencyMultiplier => _penalties.Frequency;
    /// <inheritdoc />
    public event Action OnModifiersChanged;
    /// <summary>Draw notification after completion; activates only at next round entry.</summary>
    public event Action<RunPenaltyResult> OnPenaltyReserved;
    /// <summary>Diagnostic failure; integration must stop the run without inventing a gameplay result.</summary>
    public event Action<string> OnFailed;

    /// <summary>Starts a fresh run. Invalid linked settings fail explicitly without finite fallback.</summary>
    public bool TryStart(EndlessModeData mode, out string error, EndlessBalanceSnapshot balance = null)
    {
        error = "A run is already active.";
        if (IsActive) return false;
        bool remoteRequired = mode != null && mode.RequiresRemoteBalance;
        if (remoteRequired && balance == null)
        { error = LastError = "NORMAL requires validated backend battle settings."; return false; }
        RuntimeBossStats stats = default;
        if (mode != null && !EndlessConfigurationValidator.TryValidateForRun(mode, out error, !remoteRequired))
        { LastError = error; return false; }
        if (mode != null && mode.Key == "NORMAL" && mode.BossSelection != EndlessBossSelection.ShuffleFromStart)
        { error = LastError = "NORMAL requires first-round role pools."; return false; }
        var growth = remoteRequired ? EndlessGrowthSettings.FromRemote(balance)
            : mode != null ? mode.Growth.WithHpPercent(mode.Growth.HpPercentPerStep) : null;
        EndlessBossProgressionSnapshot? progression = null;
        if (remoteRequired) progression = balance.BossProgression;
        else if (mode != null && mode.BossProgression != null)
        {
            if (!mode.BossProgression.TryCapture(out var captured, out error)) { LastError = error; return false; }
            progression = captured;
        }
        var keys = new Dictionary<BossData, string>();
        if (remoteRequired)
        {
            var registry = new Dictionary<string, BossData>(StringComparer.Ordinal);
            foreach (var pool in new[] { mode.BreatherBossPool, mode.HardBossPool })
                foreach (var entry in pool.Entries)
                    if (!registry.TryAdd(entry.BossKey, entry.Boss))
                    { error = LastError = "Duplicate installed boss key: " + entry.BossKey; return false; }
            var assignedPrefabs = new HashSet<UnityEngine.GameObject>();
            foreach (var role in new[] { EndlessBossRole.Breather, EndlessBossRole.Hard })
                foreach (string key in balance.BossKeys(role))
                {
                    if (!registry.TryGetValue(key, out var boss) || boss == null || boss.Prefab == null
                        || !assignedPrefabs.Add(boss.Prefab)
                        || !balance.TryGetCombatExpPerHp(key, 1, out var rate)
                        || !RuntimeBossStatsCalculator.TryCalculate(boss, growth, 1, role,
                            progression.Value, out _, out error, rate))
                    { error = LastError = "Missing/invalid remote boss balance/content: " + key + ". " + error; return false; }
                    keys.Add(boss, key);
                }
        }
        if (remoteRequired && mode.BossSelection == EndlessBossSelection.FixedCycle)
            foreach (var slot in mode.Slots)
                if (!keys.TryGetValue(slot.Boss, out var key)
                    || !new HashSet<string>(balance.BossKeys(slot.Role), StringComparer.Ordinal).Contains(key))
                { error = LastError = "Test boss is missing from the NORMAL remote role roster: " + slot.BossKey; return false; }
        _remoteBalance = remoteRequired ? balance : null;
        _bossKeys = keys;
        var schedule = mode != null ? new EndlessBossSchedule(mode, _random, remoteRequired ? balance : null) : null;
        if (schedule != null && (!schedule.TrySelect(1, out var firstBoss, out error)
            || !TryCalculateStats(firstBoss, growth, progression, schedule.CurrentRole, 1, out stats, out error)))
        { LastError = error; return false; }
        if (RunId == long.MaxValue) { error = LastError = "Run identity exhausted."; return false; }
        _penalties.Reset(mode != null ? mode.PenaltyPool : null);
        _mode = mode;
        _growth = growth;
        _bossProgression = progression;
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
        bool draw = IsEndless && round >= _mode.PenaltyFirstApplyRound - 1
            && (round - (_mode.PenaltyFirstApplyRound - 1)) % _mode.PenaltyInterval == 0;
        if (draw && !_penalties.TryOffer(out string error))
        { Fail(error); return false; }
        _roundCompleted = true;
        return Matches(runId, round);
    }

    /// <summary>Consumes reward completion and activates the next round exactly once.</summary>
    public bool TryAdvanceRound(long runId, int completedRound)
    {
        if (!Matches(runId, completedRound) || !_roundCompleted || AwaitingPenaltyChoice) return false;
        if (completedRound == int.MaxValue) { Fail("Round number exceeds Int32 range."); return false; }
        RuntimeBossStats stats = default;
        if (IsEndless)
        {
            if (!_bossSchedule.TrySelect(completedRound + 1, out var boss, out string statsError)
                || !TryCalculateStats(boss, _growth, _bossProgression, _bossSchedule.CurrentRole, completedRound + 1, out stats, out statsError))
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

    private bool TryCalculateStats(BossData source, EndlessGrowthSettings growth,
        EndlessBossProgressionSnapshot? progression, EndlessBossRole role, int round, out RuntimeBossStats stats, out string error)
    {
        if (_remoteBalance != null)
        {
            if (!_bossKeys.TryGetValue(source, out string key) || !_remoteBalance.TryGetCombatExpPerHp(key, round, out float rate))
            { stats = default; error = "Missing/invalid remote combat EXP rate."; return false; }
            return RuntimeBossStatsCalculator.TryCalculate(source, growth, round, role, progression.Value, out stats, out error, rate);
        }
        if (!progression.HasValue) return RuntimeBossStatsCalculator.TryCalculate(source, growth, round, out stats, out error);
        return RuntimeBossStatsCalculator.TryCalculate(source, growth, round, role, progression.Value, out stats, out error);
    }

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
