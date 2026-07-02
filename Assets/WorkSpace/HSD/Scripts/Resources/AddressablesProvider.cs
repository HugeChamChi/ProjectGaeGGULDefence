using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressablesProvider : IResourceProvider
{
    private class RefCountHandle
    {
        public AsyncOperationHandle Handle;
        public int RefCount;
    }

    private Dictionary<string, RefCountHandle> _handles = new Dictionary<string, RefCountHandle>();
    private Dictionary<Object, string> _objectToAddress = new Dictionary<Object, string>();

    public T Load<T>(string path) where T : Object
    {
        if (_handles.TryGetValue(path, out var refHandle))
        {
            if (refHandle.Handle.IsValid())
            {
                refHandle.RefCount++;
                if (refHandle.Handle.IsDone) return refHandle.Handle.Result as T;
            }
            else
            {
                _handles.Remove(path);
            }
        }
        
        var newHandle = Addressables.LoadAssetAsync<T>(path);
        T result = newHandle.WaitForCompletion();
        
        if (result != null)
        {
            if (!_handles.TryGetValue(path, out refHandle))
            {
                _handles[path] = new RefCountHandle { Handle = newHandle, RefCount = 1 };
                _objectToAddress[result] = path;
            }
            else
            {
                refHandle.RefCount++;
                Addressables.Release(newHandle); // Duplicate loaded synchronously
                result = refHandle.Handle.Result as T;
            }
        }
        else
        {
            Addressables.Release(newHandle);
        }
        
        return result;
    }

    public async UniTask<T> LoadAsync<T>(string path) where T : Object
    {
        if (_handles.TryGetValue(path, out var refHandle))
        {
            if (refHandle.Handle.IsValid())
            {
                refHandle.RefCount++;
                try
                {
                    await refHandle.Handle.ToUniTask();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AddressablesProvider] Failed to await loaded handle {path}: {e.Message}");
                }
                return refHandle.Handle.Result as T;
            }
            else
            {
                _handles.Remove(path);
            }
        }

        var newHandle = Addressables.LoadAssetAsync<T>(path);
        var newRefHandle = new RefCountHandle { Handle = newHandle, RefCount = 1 };
        _handles[path] = newRefHandle;
        
        T result = null;
        try
        {
            result = await newHandle.ToUniTask();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AddressablesProvider] Failed to load {path}: {e.Message}");
        }

        if (result != null)
        {
            _objectToAddress[result] = path;
        }
        else
        {
            newRefHandle.RefCount--;
            if (newRefHandle.RefCount <= 0)
            {
                _handles.Remove(path);
                Addressables.Release(newHandle);
            }
        }
        
        return result;
    }

    public void Unload(Object obj)
    {
        if (obj == null) return;
        
        if (_objectToAddress.TryGetValue(obj, out var path))
        {
            if (_handles.TryGetValue(path, out var refHandle))
            {
                refHandle.RefCount--;
                if (refHandle.RefCount <= 0)
                {
                    if (refHandle.Handle.IsValid())
                    {
                        Addressables.Release(refHandle.Handle);
                    }
                    _handles.Remove(path);
                    _objectToAddress.Remove(obj);
                }
            }
        }
        else
        {
            // 인스턴스화된 GameObject의 경우 바로 ReleaseInstance 처리
            if (obj is GameObject go)
            {
                Addressables.ReleaseInstance(go);
            }
            else
            {
                Addressables.Release(obj);
            }
        }
    }

    public UniTask UnloadAsync(Object obj)
    {
        Unload(obj);
        return UniTask.CompletedTask;
    }

    public T[] LoadAll<T>(string path) where T : Object
    {
        var handle = Addressables.LoadAssetsAsync<T>(path, null);
        IList<T> resultList = handle.WaitForCompletion();
        
        List<T> list = new List<T>(resultList);
        return list.ToArray();
    }

    public async UniTask<T[]> LoadAllAsync<T>(string path) where T : Object
    {
        var handle = Addressables.LoadAssetsAsync<T>(path, null);
        IList<T> resultList = null;
        try
        {
            resultList = await handle.ToUniTask();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AddressablesProvider] Failed to load all {path}: {e.Message}");
            return new T[0];
        }
        
        if (resultList == null) return new T[0];
        List<T> list = new List<T>(resultList);
        return list.ToArray();
    }

    public void ReleaseAll()
    {
        foreach (var kvp in _handles)
        {
            if (kvp.Value.Handle.IsValid())
            {
                Addressables.Release(kvp.Value.Handle);
            }
        }
        _handles.Clear();
        _objectToAddress.Clear();
    }
}
