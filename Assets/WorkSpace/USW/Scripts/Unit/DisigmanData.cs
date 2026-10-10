using UnityEngine;

/// <summary>Authored charge and countdown recovery, independent of drone hacking.</summary>
[CreateAssetMenu(fileName = "Disigman", menuName = "Game/Disigman")]
public sealed class DisigmanData : ScriptableObject
{
    /// <summary>Own basic attacks required for one skill; runtime minimum is one.</summary>
    public PerTierInt AttacksToCharge = new PerTierInt();
    /// <summary>Seconds immediately restored by one accepted skill.</summary>
    public PerTierFloat SecondsRecovered = new PerTierFloat();
}
