using Cysharp.Threading.Tasks;
using UnityEditor.EditorTools;
using UnityEngine;

public static class RM
{
    static IResourceProvider _provider = new ResourcesProvider();
    static PoolManager _poolManager = new PoolManager();

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

    #region Async Instantiate
    public static async UniTask<T> InstantiateAsync<T>(string address, Vector3 position, Quaternion rotation, Transform parent, bool isPool = false) where T : Object
    {
        T instance = Load<T>(address);

        if (instance != null)
            return instance;
        else
            instance = await LoadAsync<T>(address);

        if (instance == null) return null;

        if (isPool)
            return _poolManager.Get(instance, position, rotation, parent);
        else
            return Object.Instantiate(instance, position, rotation, parent);
    }

    public static async UniTask<T> InstantiateAsync<T>(string address, Vector3 position, Quaternion rotation, bool isPool = false) where T : Object
        => await InstantiateAsync<T>(address, position, rotation, null, isPool);

    public static async UniTask<T> InstantiateAsync<T>(string address, Vector3 position, bool isPool = false) where T : Object
        => await InstantiateAsync<T>(address, position, Quaternion.identity, null, isPool);

    public static async UniTask<T> InstantiateAsync<T>(string address, Vector3 position, Transform parent, bool isPool = false) where T : Object
        => await InstantiateAsync<T>(address, position, Quaternion.identity, parent, isPool);

    public static async UniTask<T> InstantiateAsync<T>(string address, Transform parent, bool isPool = false) where T : Object
        => await InstantiateAsync<T>(address, Vector3.zero, Quaternion.identity, parent, isPool);

    public static async UniTask<T> InstantiateAsync<T>(string address, bool isPool = false) where T : Object
        => await InstantiateAsync<T>(address, Vector3.zero, Quaternion.identity, null, isPool);

    #endregion

    #region Sync Instantiate
    // Prefab
    public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent, bool isPool = false) where T : Object
    {
        if (isPool)
            return _poolManager.Get(original, position, rotation, parent);
        else
            return Object.Instantiate(original, position, rotation, parent);
    }

    public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, bool isPool = false) where T : Object
        => Instantiate(original, position, rotation, null, isPool);

    public static T Instantiate<T>(T original, Vector3 position, bool isPool = false) where T : Object
        => Instantiate(original, position, Quaternion.identity, null, isPool);

    public static T Instantiate<T>(T original, Vector3 position, Transform parent, bool isPool = false) where T : Object
        => Instantiate(original, position, Quaternion.identity, parent, isPool);

    public static T Instantiate<T>(T original, Transform parent, bool isPool = false) where T : Object
        => Instantiate(original, Vector3.zero, Quaternion.identity, parent, isPool);

    public static T Instantiate<T>(T original, bool isPool = false) where T : Object
        => Instantiate(original, Vector3.zero, Quaternion.identity, null, isPool);

    // Address
    public static T Instantiate<T>(string address, Vector3 position, Quaternion rotation, Transform parent, bool isPool = false) where T : Object
    {
        T instance = Load<T>(address);
        if (instance == null)
        {
            Debug.LogWarning($"[AddressableSystem] {address} 주소의 에셋이 로드되지 않았습니다.");
            return null;
        }

        if (isPool)
            return _poolManager.Get(instance, position, rotation, parent);
        else
            return Object.Instantiate(instance, position, rotation, parent);
    }

    public static T Instantiate<T>(string address, Vector3 position, Quaternion rotation, bool isPool = false) where T : Object
        => Instantiate<T>(address, position, rotation, null, isPool);

    public static T Instantiate<T>(string address, Vector3 position, bool isPool = false) where T : Object
        => Instantiate<T>(address, position, Quaternion.identity, null, isPool);

    public static T Instantiate<T>(string address, Vector3 position, Transform parent, bool isPool = false) where T : Object
        => Instantiate<T>(address, position, Quaternion.identity, parent, isPool);

    public static T Instantiate<T>(string address, Transform parent, bool isPool = false) where T : Object
        => Instantiate<T>(address, Vector3.zero, Quaternion.identity, parent, isPool);
    #endregion

    #region Destroy
    public static async UniTask DestroyAsync(GameObject obj, float duration = 0)
    {
        if (obj == null) return;

        await _poolManager.Release(obj, duration);
    }

    public static void Destroy(GameObject obj, float duration = 0)
    {
        if (obj == null) return;

        _poolManager.Release(obj, duration).Forget();
    }

    public static async UniTask DestroyAsync(Component component, float duration = 0)
    {
        if (component == null) return;

        await _poolManager.Release(component, duration);
    }

    public static void Destroy(Component component, float duration = 0)
    {
        if (component == null) return;

        _poolManager.Release(component, duration).Forget();
    }
    #endregion
}
