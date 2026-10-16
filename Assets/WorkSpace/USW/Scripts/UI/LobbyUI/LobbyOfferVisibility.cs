using UnityEngine;

/// <summary>Offer icons/banners are visible only on the settled lobby battle tab. Never opens a popup.</summary>
public sealed class LobbyOfferVisibility : MonoBehaviour
{
    private const int BattleTabIndex = 2;
    private BottomPanelController _tabs;
    private CanvasGroup _group;

    /// <summary>Binds a promotion-only container; never attach to the whole battle page.</summary>
    public void Configure(BottomPanelController tabs)
    {
        _tabs = tabs;
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        Refresh();
    }
    /// <summary>Pure placement policy shared by dynamic packages, target offers and welcome packages.</summary>
    public static bool CanShow(string scene, int tab, bool transitioning, bool eligible)
        => scene == "LobbyScene" && tab == BattleTabIndex && !transitioning && eligible;
    private void LateUpdate() => Refresh();
    private void Refresh()
    {
        if (_group == null) return;
        bool show = _tabs != null && _tabs.isActiveAndEnabled && CanShow(gameObject.scene.name, _tabs.CurrentIndex, _tabs.IsTransitioning, true);
        _group.alpha = show ? 1 : 0; _group.interactable = _group.blocksRaycasts = show;
    }
}
