using Cysharp.Threading.Tasks;
using UnityEngine;

public static class ResoucesManager
{
    static IResourceProvider _provider = new ResourcesProvider();

    public static T Load<T>(string path) where T : Object
    {
        return _provider.Load<T>(path);
    }

    public static UniTask<T> LoadAsync<T>(string path) where T : Object
    {
        return _provider.LoadAsync<T>(path);
    }

    public static void Unload(Object obj)
    {
        _provider.Unload(obj);
    }

    public static async UniTask UnloadAsync(Object obj)
    {
        await _provider.UnloadAsync(obj);
    }

    public static T[] LoadAll<T>(string path) where T : Object
    {
        return _provider.LoadAll<T>(path);
    }

    public static async UniTask<T[]> LoadAllAsync<T>(string path) where T : Object
    {
        return await _provider.LoadAllAsync<T>(path);
    }
}
