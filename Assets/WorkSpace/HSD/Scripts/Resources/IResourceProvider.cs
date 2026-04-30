using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IResourceProvider
{
    T Load<T>(string path) where T : Object;
    UniTask<T> LoadAsync<T>(string path) where T : Object;

    void Unload(Object obj);
    UniTask UnloadAsync(Object obj);

    T[] LoadAll<T>(string path) where T : Object;
    UniTask<T[]> LoadAllAsync<T>(string path) where T : Object;
}
