using System;
using System.Collections.Generic;

/// <summary>Per-run draw/activation state advanced only by EndlessRunService.</summary>
internal sealed class RunPenaltyState
{
    private sealed class Candidate
    {
        internal string Key;
        internal RunPenaltyTarget Target;
        internal double Factor;
        internal double CumulativeWeight;
        internal int Count;
    }

    private readonly IEndlessRandom _random;
    private readonly List<Candidate> _candidates = new List<Candidate>();
    private Candidate _pending;
    private double _totalWeight;

    internal double Attack { get; private set; } = 1d;
    internal double Frequency { get; private set; } = 1d;

    internal RunPenaltyState(IEndlessRandom random) => _random = random;

    internal void Reset(RunPenaltyPoolData pool)
    {
        _pending = null;
        _candidates.Clear();
        _totalWeight = 0d;
        Attack = Frequency = 1d;
        if (pool == null) return;
        foreach (var entry in pool.Entries)
        {
            if (!entry.Enabled) continue;
            _totalWeight += entry.Weight;
            _candidates.Add(new Candidate
            {
                Key = entry.Penalty.Key, Target = entry.Penalty.Target,
                Factor = 1d + entry.Penalty.Delta / 100d, CumulativeWeight = _totalWeight
            });
        }
    }

    internal int GetStackCount(string key)
    {
        foreach (var candidate in _candidates)
            if (candidate.Key == key) return candidate.Count;
        return 0;
    }

    internal bool TryReserve(long runId, int nextRound, out RunPenaltyResult result, out string error)
    {
        result = default;
        error = "Invalid random draw or exhausted penalty stack count.";
        double sample = _random.NextUnit();
        if (sample < 0d || sample >= 1d || double.IsNaN(sample)) return false;
        double target = sample * _totalWeight;
        // Scaling can round up to totalWeight. The last eligible interval owns that endpoint.
        Candidate selected = _candidates[_candidates.Count - 1];
        foreach (var candidate in _candidates)
            if (target < candidate.CumulativeWeight) { selected = candidate; break; }
        if (selected.Count == int.MaxValue) return false;
        _pending = selected;
        result = new RunPenaltyResult(runId, selected.Key, selected.Count + 1, nextRound);
        error = "";
        return true;
    }

    internal bool TryActivate(out bool changed, out string error)
    {
        changed = false;
        error = "";
        if (_pending == null) return true;
        double attack = 1d;
        double frequency = 1d;
        foreach (var candidate in _candidates)
        {
            double factor = Math.Pow(candidate.Factor, candidate.Count + (candidate == _pending ? 1 : 0));
            if (candidate.Target == RunPenaltyTarget.UnitAttack) attack *= factor;
            else frequency *= factor;
        }
        if (!(attack > 0d) || !(frequency > 0d) || double.IsInfinity(attack) || double.IsInfinity(frequency))
        { error = "Run penalty multiplier underflow/overflow; long-run numeric policy required."; return false; }
        _pending.Count++;
        _pending = null;
        Attack = attack;
        Frequency = frequency;
        changed = true;
        return true;
    }

    internal void DiscardPending() => _pending = null;
}
