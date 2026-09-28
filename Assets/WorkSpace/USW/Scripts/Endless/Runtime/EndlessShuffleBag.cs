using System;
using System.Collections.Generic;

/// <summary>
/// Run-owned draw-without-replacement pool. Each boss category owns a separate instance.
/// Round/cycle boundaries do not reset it; only exhaustion triggers another shuffle.
/// </summary>
public sealed class EndlessShuffleBag<T> where T : class
{
    private readonly T[] _candidates;
    private readonly IEndlessRandom _random;
    private readonly IEqualityComparer<T> _comparer;
    private T[] _order;
    private int _cursor;

    /// <summary>
    /// Copies distinct candidates. previousSelection seeds the same category's last fixed-segment boss,
    /// without consuming that candidate from the new pool. Equality is explicitly configurable by the caller.
    /// </summary>
    public EndlessShuffleBag(IReadOnlyList<T> candidates, IEndlessRandom random,
        T previousSelection = null, IEqualityComparer<T> comparer = null)
    {
        if (candidates == null || candidates.Count == 0)
            throw new ArgumentException("A shuffle pool requires at least one candidate.", nameof(candidates));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _candidates = new T[candidates.Count];
        var distinct = new HashSet<T>(_comparer);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == null || !distinct.Add(candidates[i]))
                throw new ArgumentException("Shuffle candidates must be non-null and distinct.", nameof(candidates));
            _candidates[i] = candidates[i];
        }
        LastSelected = previousSelection;
    }

    /// <summary>Total distinct candidates in this pool.</summary>
    public int CandidateCount => _candidates.Length;
    /// <summary>Remaining candidates in the current pool; an untouched/reset pool is full.</summary>
    public int RemainingCount => _order == null ? CandidateCount : CandidateCount - _cursor;
    /// <summary>Last selected boss in this category, including an optional fixed-segment seed.</summary>
    public T LastSelected { get; private set; }

    /// <summary>
    /// Consumes one candidate, reshuffling only when needed. A failed random sample consumes no candidate.
    /// With two or more candidates, the first selection after each shuffle differs from LastSelected.
    /// </summary>
    public bool TryTake(out T candidate, out string error)
    {
        candidate = null;
        error = "";
        if (_order == null || _cursor == CandidateCount)
        {
            if (!TryShuffle(out T[] shuffled, out error)) return false;
            _order = shuffled;
            _cursor = 0;
        }
        candidate = _order[_cursor++];
        LastSelected = candidate;
        return true;
    }

    /// <summary>Clears run history for a new run; never call at a ten-round boundary.</summary>
    public void Reset(T previousSelection = null)
    {
        _order = null;
        _cursor = 0;
        LastSelected = previousSelection;
    }

    private bool TryShuffle(out T[] shuffled, out string error)
    {
        shuffled = (T[])_candidates.Clone();
        error = "";
        // Fisher-Yates: a full permutation, without repeated draws or retry loops.
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            if (!TryIndex(i + 1, out int index, out error)) return false;
            Swap(shuffled, i, index);
        }
        if (shuffled.Length > 1 && LastSelected != null && _comparer.Equals(shuffled[0], LastSelected))
        {
            // Uniformly choose another first element, keeping the displaced boss in this pool.
            if (!TryIndex(shuffled.Length - 1, out int index, out error)) return false;
            Swap(shuffled, 0, index + 1);
        }
        return true;
    }

    private bool TryIndex(int count, out int index, out string error)
    {
        index = 0;
        error = "";
        double sample = _random.NextUnit();
        if (double.IsNaN(sample) || sample < 0d || sample >= 1d)
        { error = "Shuffle random sample must be finite and in [0, 1)."; return false; }
        index = Math.Min((int)(sample * count), count - 1);
        return true;
    }

    private static void Swap(T[] values, int a, int b)
    {
        T value = values[a];
        values[a] = values[b];
        values[b] = value;
    }
}
