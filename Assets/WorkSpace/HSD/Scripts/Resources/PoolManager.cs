using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class PoolManager : IDisposable
{
    private Dictionary<string, IObjectPool<GameObject>> _poolDic = new();
    private Dictionary<string, Transform> _parentDic = new();
    private Dictionary<string, float> _lastUseTimeDic = new();

    private Transform _parent;

    private const float _poolCleanupTime = 60;
    private const float _poolCleanupDelay = 30;

    private CancellationTokenSource _cts;

    public PoolManager()
    {
        Initialize();
    }

    public void Start()
    {
        // Already initialized in constructor, but kept for compatibility
        if (_cts == null) Initialize();
    }

    private void Initialize()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        ResetPool();
        PoolCleanupRoutine(_cts.Token).Forget();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void ResetPool()
    {
        if (_poolDic != null)
        {
            foreach (var pool in _poolDic.Values)
            {
                pool?.Clear();
            }
        }

        _poolDic = new();
        _parentDic = new();
        _lastUseTimeDic = new();

        _parent = new GameObject("Pools").transform;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetPool();
    }

    private async UniTask PoolCleanupRoutine(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool canceled = await UniTask.WaitForSeconds(_poolCleanupDelay, cancellationToken: token).SuppressCancellationThrow();
            if (canceled) break;

            float now = Time.time;
            List<string> removePoolKeys = new List<string>();

            foreach (var kvp in _poolDic)
            {
                if (_lastUseTimeDic.TryGetValue(kvp.Key, out float lastTime))
                {
                    if (now - lastTime > _poolCleanupTime)
                    {
                        removePoolKeys.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in removePoolKeys)
            {
                if (_poolDic.TryGetValue(key, out var pool))
                {
                    pool.Clear();
                    _poolDic.Remove(key);
                }

                if (_parentDic.TryGetValue(key, out var parent) && parent != null)
                {
                    Object.Destroy(parent.gameObject);
                    _parentDic.Remove(key);
                }

                _lastUseTimeDic.Remove(key);
            }
        }
    }

    private IObjectPool<GameObject> GetOrCreatePool(string name, GameObject prefab)
    {
        if (_poolDic.TryGetValue(name, out var pool))
            return pool;

        if (_parent == null)
            _parent = new GameObject("Pools").transform;

        GameObject rootGo = new GameObject($"{name} Pool");
        Transform root = rootGo.transform;
        root.parent = _parent;
        _parentDic[name] = root;

        ObjectPool<GameObject> newPool = new ObjectPool<GameObject>
        (
            createFunc: () =>
            {
                GameObject obj = Object.Instantiate(prefab);
                obj.SetActive(false);
                obj.name = name;
                if (root != null)
                    obj.transform.SetParent(root, false);
                
                _lastUseTimeDic[name] = Time.time;
                return obj;
            },
            actionOnGet: (GameObject go) =>
            {
                if (go != null)
                {
                    go.transform.SetParent(null, false);
                    _lastUseTimeDic[name] = Time.time;
                }
            },
            actionOnRelease: (GameObject go) =>
            {
                if (go != null)
                {
                    if (root != null)
                        go.transform.SetParent(root, false);
                    go.SetActive(false);
                }
            },
            actionOnDestroy: (GameObject go) =>
            {
                if (go != null)
                    Object.Destroy(go);
            },
            maxSize: 10
        );

        _poolDic[name] = newPool;
        return newPool;
    }

    #region Get
    public T Get<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object
    {
        if (original == null) return null;

        GameObject prefab = original as GameObject;
        if (prefab == null && original is Component comp)
            prefab = comp.gameObject;

        if (prefab == null) return null;

        string name = prefab.name;
        var pool = GetOrCreatePool(name, prefab);

        GameObject go = pool.Get();
        if (go == null) return null;

        if (parent != null)
            go.transform.SetParent(parent, false);

        go.transform.position = position;
        go.transform.rotation = rotation;
        go.SetActive(true);

        return go as T ?? go.GetComponent<T>();
    }

    public T Get<T>(T original, Vector3 position, Quaternion rotation) where T : Object
    {
        return Get<T>(original, position, rotation, null);
    }

    public T Get<T>(T original, Vector3 position) where T : Object
    {
        return Get<T>(original, position, Quaternion.identity);
    }

    public T Get<T>(T original, Vector3 position, Transform parent) where T : Object
    {
        return Get<T>(original, position, Quaternion.identity, parent);
    }
    #endregion

    #region Release
    public void Release<T>(T original) where T : Object
    {
        if (original == null) return;

        GameObject obj = original as GameObject;
        if (obj == null && original is Component comp)
            obj = comp.gameObject;

        if (obj == null) return;

        string name = obj.name;

        if (_poolDic.TryGetValue(name, out var pool))
        {
            if (obj.activeSelf)
                pool.Release(obj);
        }
        else
        {
            Object.Destroy(obj);
        }
    }

    public async UniTask Release(GameObject obj, float duration)
    {
        if (obj == null) return;

        if (duration > 0)
        {
            bool canceled = await UniTask.WaitForSeconds(duration).SuppressCancellationThrow();
            if (canceled || obj == null) return;
        }

        if (_poolDic.TryGetValue(obj.name, out var pool))
        {
            if (obj.activeSelf)
                pool.Release(obj);
        }
        else
        {
            Object.Destroy(obj);
        }
    }

    public async UniTask Release(Component component, float duration)
    {
        if (component == null) return;
        await Release(component.gameObject, duration);
    }
    #endregion

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
