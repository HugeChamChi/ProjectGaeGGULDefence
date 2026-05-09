using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ChiefTable
{
    private Dictionary<int, ChiefData> _chiefDict = new Dictionary<int, ChiefData>();
    public IEnumerable<ChiefData> Chiefs => _chiefDict.Values;

    const string PATH = "Data/ChiefData";

    public async UniTask InitializeAsync()
    {
        // RM.LoadAllAsync를 사용하여 Resources/Data/ChiefData 폴더의 모든 ChiefData 로드
        var dataList = await RM.LoadAllAsync<ChiefData>(PATH);

        foreach (var data in dataList)
        {
            if (!_chiefDict.TryAdd(data.Id, data))
            {
                Debug.LogWarning($"Duplicate Chief ID: {data.Id} in {PATH}");
            }
        }
        
        Debug.Log($"<color=cyan>ChiefTable</color> initialized with {_chiefDict.Count} chiefs.");
    }

    public ChiefData GetChief(int id)
    {
        if (_chiefDict.TryGetValue(id, out var data))
            return data;
        
        return null;
    }
}
