/// <summary>An event source on the scene's chronological drone combat clock.</summary>
public interface IDroneCombatParticipant
{
    /// <summary>Seconds until the next event, or positive infinity while blocked.</summary>
    double NextCombatEventIn { get; }
    /// <summary>Equal-time order: cast contact/return (0), ready skill (1), basic shot (2).</summary>
    int CombatEventPriority { get; }
    /// <summary>Advance active timers by one shared slice without firing an event.</summary>
    void AdvanceCombatTime(double seconds);
    /// <summary>Execute one due event. False suppresses a non-progressing source for this frame.</summary>
    bool ProcessCombatEvent();
    /// <summary>Render the final state once after all logical events in a frame.</summary>
    void RenderCombatState();
}
