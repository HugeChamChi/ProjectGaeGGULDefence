using UnityEngine;

/// <summary>Connects the scene-authored header and utility menu. Anchors/layout groups own placement.</summary>
public sealed class LobbyHeaderLayout : MonoBehaviour
{
    [SerializeField] private LobbyUtilityMenu _menu;
    [SerializeField] private BottomPanelController _lobbyTabs;
    private bool _lastAvailable;
    private bool _availabilitySet;

    private void Start()
    {
        if (_menu == null) return;
        _menu.Initialize();
        RefreshAvailability();
    }

    private void LateUpdate() => RefreshAvailability();

    private void RefreshAvailability()
    {
        if (_menu == null) return;
        bool available = _lobbyTabs != null && _lobbyTabs.CurrentIndex == 2 && !_lobbyTabs.IsTransitioning;
        if (_availabilitySet && available == _lastAvailable) return;
        _availabilitySet = true; _lastAvailable = available;
        _menu.SetAvailable(available);
    }
}
