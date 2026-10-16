using UnityEngine;

/// <summary>Connects the scene-authored header and utility menu. Anchors/layout groups own placement.</summary>
public sealed class LobbyHeaderLayout : MonoBehaviour
{
    [SerializeField] private LobbyUtilityMenu _menu;

    private void Start()
    {
        if (_menu == null) return;
        _menu.Initialize();
    }

}
