using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

public class ProfileItemTable
{
    private Dictionary<int, IconItemDataSO> iconItems;
    private Dictionary<int, FrameItemDataSO> frameItems;

    public IconItemDataSO GetIcon(int id) => iconItems[id];
    public FrameItemDataSO GetFrame(int id) => frameItems[id];

    public IEnumerable<IconItemDataSO> GetAllIcons() => iconItems.Values;
    public IEnumerable<FrameItemDataSO> GetAllFrames() => frameItems.Values;
    public IEnumerable<IProfileItem> GetItemByType(ProfileItemType type)
        => type switch
        {
            ProfileItemType.Icon => GetAllIcons(),
            ProfileItemType.Frame => GetAllFrames(),
            _ => Enumerable.Empty<IProfileItem>()
        };

    const string iconKey   = "Data/PlayerIcon";
    const string frameKey  = "Data/PlayerFrame";

    public async UniTask InitializeAsync()
    {
        await LoadAsync();
    }

    private async UniTask LoadAsync()
    {
        var (icons, frames) = await UniTask.WhenAll(
            RM.LoadAllAsync<IconItemDataSO>(iconKey),
            RM.LoadAllAsync<FrameItemDataSO>(frameKey)
        );

        iconItems = icons.ToDictionary(x => x.Id);
        frameItems = frames.ToDictionary(x => x.Id);
    }
}
