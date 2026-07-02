using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ChiefTable
{
    private Dictionary<int, ChiefData> _chiefDict = new Dictionary<int, ChiefData>();
    public IEnumerable<ChiefData> Chiefs => _chiefDict.Values;

    const string LABEL = "ChiefData";

    public async UniTask InitializeAsync()
    {
        // RM.LoadAllAsync를 사용하여 Addressables Label 'ChiefData'의 모든 ChiefData 로드
        var dataList = await RM.LoadAllAsync<ChiefData>(LABEL);

        foreach (var data in dataList)
        {
            if (!_chiefDict.TryAdd(data.Id, data))
            {
                Debug.LogWarning($"Duplicate Chief ID: {data.Id} in {LABEL}");
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
