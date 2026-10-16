using UnityEngine;

/// <summary>Authored initial balances; player-owned amounts are stored separately in PlayerData.</summary>
[CreateAssetMenu(menuName = "Game/Player Economy", fileName = "PlayerEconomyConfig")]
public sealed class PlayerEconomyConfig : ScriptableObject
{
    /// <summary>Existing new-account Gold default.</summary>
    [Min(0)] public int InitialGold;
    /// <summary>Existing new-account Diamond default; not a substitute for upgrade currencies A/B.</summary>
    [Min(0)] public int InitialDiamond;
}
