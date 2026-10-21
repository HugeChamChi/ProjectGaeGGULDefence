using UnityEngine;

/// <summary>Shared HP gates. Each threshold selects one skill from this phase group once per boss spawn.</summary>
[CreateAssetMenu(menuName = "Game/Boss/Placement HP Phases")]
public sealed class PlacementBossPhaseData : ScriptableObject
{
    /// <summary>HP fractions in descending order; crossing several gates queues one cast for each.</summary>
    public float[] Thresholds = { .7f, .3f };
}
