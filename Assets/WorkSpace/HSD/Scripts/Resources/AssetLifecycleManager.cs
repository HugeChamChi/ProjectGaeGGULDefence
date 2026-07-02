using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 에셋 로드/언로드 라이프사이클을 중앙에서 관리합니다.
/// VContainer에 등록하여 씬 단위 에셋 관리에 사용합니다.
/// </summary>
public class AssetLifecycleManager
{
    private readonly HashSet<ILoadableAsset> _loaded = new HashSet<ILoadableAsset>();

    public async UniTask LoadAsync(IEnumerable<ILoadableAsset> assets)
    {
        if (assets == null) return;
        var tasks = new List<UniTask>();
        foreach (var asset in assets)
        {
            if (asset != null && !asset.IsLoaded)
            {
                _loaded.Add(asset);
                tasks.Add(asset.LoadAssetsAsync());
            }
        }
        await UniTask.WhenAll(tasks);
    }

    public async UniTask LoadAsync(ILoadableAsset asset)
    {
        if (asset == null || asset.IsLoaded) return;
        _loaded.Add(asset);
        await asset.LoadAssetsAsync();
    }

    public void UnloadAll()
    {
        foreach (var asset in _loaded)
        {
            if (asset != null)
                asset.UnloadAssets();
        }
        _loaded.Clear();
    }

    public void Unload(ILoadableAsset asset)
    {
        if (asset == null) return;
        asset.UnloadAssets();
        _loaded.Remove(asset);
    }

    public int LoadedCount => _loaded.Count;
    public IReadOnlyCollection<ILoadableAsset> LoadedAssets => _loaded;
}
