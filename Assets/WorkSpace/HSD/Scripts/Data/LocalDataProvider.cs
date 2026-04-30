using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
/// <summary>
/// Resources 폴더에서 ScriptableObject를 로드하는 프로바이더
/// </summary>
public class LocalDataProvider<T> : IDataProvider<T> where T : ScriptableObject
{
    private readonly string _path;

    public LocalDataProvider(string path)
    {
        _path = path;
    }

    public async UniTask<Dictionary<int, T>> LoadAsync()
    {
        var dictionary = new Dictionary<int, T>();
        var assets = Resources.LoadAll<T>(_path);

        foreach (var asset in assets)
        {
            // UnitData의 경우 unitType을 키로 사용. 
            // 다른 타입의 경우 인터페이스나 리플렉션을 통해 키를 가져와야 함.
            if (asset is UnitData unit)
            {
                dictionary.TryAdd(unit.unitType, asset);
            }
            // ItemData 등 추가 처리 필요
        }

        await UniTask.Yield();
        return dictionary;
    }
}
