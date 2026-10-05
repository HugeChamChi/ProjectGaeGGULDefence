using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Deck selection, card pool and independent active skill. Never a grid unit.</summary>
[CreateAssetMenu(fileName = "ChieftainData", menuName = "Game/ChieftainData")]
public sealed class ChieftainData : ScriptableObject
{
    /// <summary>Existing backend chief ID when authored; zero means no saved-ID mapping.</summary>
    [FormerlySerializedAs("chieftainId")] public int ChieftainId;
    /// <summary>Selection display name.</summary>
    [FormerlySerializedAs("chieftainName")] public string DisplayName;
    /// <summary>Selection portrait and fallback skill icon.</summary>
    public Sprite Icon;
    /// <summary>The complete card pool for this selection.</summary>
    public LevelUpPoolData LevelUpPool;
    /// <summary>Independent Alphan skill configuration, if this selection provides one.</summary>
    public AlphanSkillData AlphanSkill;
}
