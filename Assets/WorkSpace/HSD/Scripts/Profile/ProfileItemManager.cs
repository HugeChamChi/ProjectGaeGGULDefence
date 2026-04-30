using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

public class ProfileItemManager
{
    private Dictionary<int, IconItemDataSO> iconItems;
    private Dictionary<int, FrameItemDataSO> frameItems;

    public IconItemDataSO GetIcon(int id) => iconItems.GetValueOrDefault(id);
    public FrameItemDataSO GetFrame(int id) => frameItems.GetValueOrDefault(id);

    public IEnumerable<IconItemDataSO> GetAllIcons() => iconItems.Values;
    public IEnumerable<FrameItemDataSO> GetAllFrames() => frameItems.Values;

    const string iconKey   = "Data/IconItem";
    const string frameKey  = "Data/FrameItem";

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
