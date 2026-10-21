using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

public class ProfileItemTable
{
    private Dictionary<int, IconItemDataSO> iconItems = new();
    private Dictionary<int, FrameItemDataSO> frameItems = new();
    /// <summary>Catalog has completed loading, including the direct LobbyScene Editor path.</summary>
    public bool IsReady { get; private set; }

    public IconItemDataSO GetIcon(int id) => iconItems.TryGetValue(id, out var item) ? item : null;
    public FrameItemDataSO GetFrame(int id) => frameItems.TryGetValue(id, out var item) ? item : null;

    public IEnumerable<IconItemDataSO> GetAllIcons() => iconItems.Values;
    public IEnumerable<FrameItemDataSO> GetAllFrames() => frameItems.Values;
    public IEnumerable<IProfileItem> GetItemByType(ProfileItemType type)
        => type switch
        {
            ProfileItemType.Icon => GetAllIcons(),
            ProfileItemType.Frame => GetAllFrames(),
            _ => Enumerable.Empty<IProfileItem>()
        };

    const string iconLabel  = "PlayerIcon";
    const string frameLabel = "PlayerFrame";

    public async UniTask InitializeAsync()
    {
        if (IsReady) return;
        await LoadAsync();
        IsReady = true;
    }

    private async UniTask LoadAsync()
    {
        var (icons, frames) = await UniTask.WhenAll(
            RM.LoadAllAsync<IconItemDataSO>(iconLabel),
            RM.LoadAllAsync<FrameItemDataSO>(frameLabel)
        );

        iconItems = icons.ToDictionary(x => x.Id);
        frameItems = frames.ToDictionary(x => x.Id);
    }
}
