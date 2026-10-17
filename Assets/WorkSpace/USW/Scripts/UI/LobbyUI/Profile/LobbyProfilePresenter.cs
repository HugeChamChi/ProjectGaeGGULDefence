using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Account profile, record and cosmetic catalog presentation for the lobby popup.</summary>
public sealed class LobbyProfilePresenter : IDisposable
{
    private readonly LobbyProfilePopup _view;
    private readonly ProfilePopupConfig _config;
    private readonly PlayerDataController _data;
    private readonly RoundRecordManager _records;
    /// <summary>Subscribes to the existing account and record entry points.</summary>
    public LobbyProfilePresenter(LobbyProfilePopup view, ProfilePopupConfig config)
    {
        _view = view; _config = config; _data = Player.PlayerData; _records = Player.RoundRecords;
        _data.OnUpdateUI += OnData; _records.OnChanged += Refresh;
        Player.Profile.OnChanged += Refresh;
    }
    /// <summary>Publishes real account values, keeping unqueried records unknown.</summary>
    public void Refresh()
    {
        var data = _data.IsReady ? _data.Data : null;
        _view.Render(data, _records.GetBest("NORMAL"));
    }
    private void OnData(PlayerData data) => Refresh();
    /// <summary>Stable catalog ordering; the view adds empty, unselectable slots after these items.</summary>
    public IReadOnlyList<IProfileItem> Catalog(bool frames) =>
        Table.Profile.GetItemByType(frames ? ProfileItemType.Frame : ProfileItemType.Icon).OrderBy(x => x.Id).ToArray();
    /// <summary>Uses account ownership or the authored default grant; never changes a shared SO.</summary>
    public bool IsOwned(IProfileItem item)
    {
        if (item == null) return false;
        var map = item.Type == ProfileItemType.Icon ? Player.Profile.Data.iconStatusMap : Player.Profile.Data.frameStatusMap;
        return item.IsUnlocked || (map.TryGetValue(item.Id.ToString(), out bool owned) && owned);
    }
    /// <summary>Only approved catalog artwork is used; current placeholder policy leaves it blank.</summary>
    public UnityEngine.Sprite Artwork(IProfileItem item) =>
        item?.Type == ProfileItemType.Icon && item.Id == 0 && _config.DefaultPortrait != null ? _config.DefaultPortrait :
        _config.ShowCatalogArtwork ? item?.Sprite : null;
    /// <summary>Unsubscribes when the scene-owned popup is destroyed.</summary>
    public void Dispose() { _data.OnUpdateUI -= OnData; _records.OnChanged -= Refresh; Player.Profile.OnChanged -= Refresh; }
}
