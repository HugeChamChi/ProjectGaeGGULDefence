using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Orders drone shots and hacking boundaries by combat time, independent of rendered frames.</summary>
public sealed class DroneCombatClock : MonoBehaviour
{
    /// <summary>Floating-point boundary tolerance in seconds, not a simulation tick or rate cap.</summary>
    public const double BoundaryTolerance = 0.0000001;
    private sealed class Entry
    {
        internal IDroneCombatParticipant Participant;
        internal int RegisteredFrame;
        internal bool Removed;
        internal bool Suppressed;
    }
    private readonly List<Entry> _entries = new();
    private GameManager _game;
    private bool _advancing;
    /// <summary>Whether a call originates inside the logical timeline rather than an external Unity callback.</summary>
    public bool IsAdvancing => _advancing;
    /// <summary>Logical time at the currently dispatched event, or at the last completed frame.</summary>
    public double ElapsedCombatTime { get; private set; }
    /// <summary>Connect this scene-owned clock to its existing game lifetime.</summary>
    public void Initialize(GameManager game) => _game = game;
    /// <summary>Register once; a newly placed object never receives time preceding its placement frame.</summary>
    public void Register(IDroneCombatParticipant participant)
    {
        foreach (var entry in _entries)
            if (!entry.Removed && ReferenceEquals(entry.Participant, participant)) return;
        _entries.Add(new Entry { Participant = participant, RegisteredFrame = _advancing ? Time.frameCount - 1 : Time.frameCount });
    }
    /// <summary>Removal immediately prevents all later events, including during the same catch-up frame.</summary>
    public void Unregister(IDroneCombatParticipant participant)
    {
        foreach (var entry in _entries)
            if (ReferenceEquals(entry.Participant, participant)) entry.Removed = true;
    }
    private bool Active(Entry entry) => !entry.Removed && !entry.Suppressed
        && entry.RegisteredFrame < Time.frameCount
        && (!(entry.Participant is UnityEngine.Object obj) || obj != null);
    private void LateUpdate() => Advance(Time.deltaTime);
    /// <summary>Consume Unity's accepted scaled delta; no wall-clock catch-up and no event-count truncation.</summary>
    public void Advance(double seconds)
    {
        if (_advancing || seconds < 0 || double.IsInfinity(seconds) || double.IsNaN(seconds)) return;
        if (_game != null && _game.CurrentState != GameManager.GameState.Playing) return;
        _advancing = true;
        foreach (var entry in _entries) entry.Suppressed = false;
        double end = ElapsedCombatTime + seconds;
        try
        {
            while (_game?.IsFinished != true)
            {
                Entry next = null;
                double wait = double.PositiveInfinity;
                // Registration order is the stable tie-breaker inside each event priority.
                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    if (!Active(entry)) continue;
                    double due = entry.Participant.NextCombatEventIn;
                    if (double.IsInfinity(due) || double.IsNaN(due)) continue;
                    due = Math.Max(0, due);
                    if (next == null || due < wait - BoundaryTolerance ||
                        (Math.Abs(due - wait) <= BoundaryTolerance && entry.Participant.CombatEventPriority < next.Participant.CombatEventPriority))
                    { next = entry; wait = due; }
                }
                double remaining = Math.Max(0, end - ElapsedCombatTime);
                double step = Math.Min(wait, remaining);
                if (step > 0)
                {
                    for (int i = 0; i < _entries.Count; i++)
                        if (Active(_entries[i])) _entries[i].Participant.AdvanceCombatTime(step);
                    ElapsedCombatTime += step;
                }
                if (next == null || wait > remaining + BoundaryTolerance) break;
                if (Active(next))
                {
                    try { if (!next.Participant.ProcessCombatEvent()) next.Suppressed = true; }
                    catch (Exception error) { next.Suppressed = true; Debug.LogException(error); }
                }
                // Re-evaluate after EVERY event. A sixth shot can start a skill before a
                // seventh shot at the same timestamp, even when both fell inside one frame.
            }
            if (_game?.IsFinished != true) ElapsedCombatTime = end;
            for (int i = 0; i < _entries.Count; i++)
                if (Active(_entries[i])) _entries[i].Participant.RenderCombatState();
        }
        finally
        {
            _advancing = false;
            _entries.RemoveAll(entry => entry.Removed || entry.Participant is UnityEngine.Object obj && obj == null);
        }
    }
}
