using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ResourcesProvider : IResourceProvider
{
    Dictionary<string, Object> _cache = new Dictionary<string, Object>();

    public T Load<T>(string path) where T : Object
    {
        if (_cache.TryGetValue(path, out var cachedAsset))
        {
            return cachedAsset as T;
        }

        var asset = Resources.Load<T>(path);
        if (asset != null)
        {
            _cache[path] = asset;
        }

        return asset;
    }
    public async UniTask<T> LoadAsync<T>(string path) where T : Object
    {
        if (_cache.TryGetValue(path, out var cachedAsset))
        {
            return cachedAsset as T;
        }

        var request = Resources.LoadAsync<T>(path);
        await request;

        return request.asset as T;
    }

    public void Unload(Object obj)
    {
        if (obj == null) return;

        Resources.UnloadAsset(obj);
    }
    public UniTask UnloadAsync(Object obj)
    {
        if (obj == null) return

        UniTask.CompletedTask;
        Resources.UnloadAsset(obj);
        return UniTask.CompletedTask;
    }

    public T[] LoadAll<T>(string path) where T : Object
    {
        return Resources.LoadAll<T>(path);
    }
    public async UniTask<T[]> LoadAllAsync<T>(string path) where T : Object
    {
        var request = Resources.LoadAll<T>(path);
        return request;
    }
}
