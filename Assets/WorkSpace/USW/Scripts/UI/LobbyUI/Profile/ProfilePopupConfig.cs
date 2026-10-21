using UnityEngine;

/// <summary>Authored profile presentation and nickname rules.</summary>
[CreateAssetMenu(menuName = "GaeGGUL/UI/Profile popup")]
public sealed class ProfilePopupConfig : ScriptableObject
{
    [Min(1)] public int MinimumNicknameLength = 2;
    [Min(1)] public int MaximumNicknameLength = 10;
    [Min(1)] public int MinimumCatalogSlots = 12;
    [Tooltip("Enable after the profile catalog has game-specific artwork.")]
    public bool ShowCatalogArtwork;
    [Tooltip("Existing game portrait for the default profile; unassigned art remains blank.")]
    public Sprite DefaultPortrait;
}
