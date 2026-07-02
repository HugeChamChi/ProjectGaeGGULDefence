using System.Collections.Generic;
using UnityEngine;

public class DynamicShopTable
{
    private Dictionary<string, DynamicShopData> _shopDict = new Dictionary<string, DynamicShopData>();

    public async Cysharp.Threading.Tasks.UniTask InitializeAsync()
    {
        _shopDict.Clear();
        // Addressables 레이블 "DynamicShopData" 기반으로 에셋들을 비동기 로드합니다.
        var shopDatas = await RM.LoadAllAsync<DynamicShopData>("DynamicShopData");
        
        if (shopDatas == null || shopDatas.Length == 0)
        {
            Debug.LogWarning("[DynamicShopTable] No DynamicShopData found with Label 'DynamicShopData'");
            return;
        }

        foreach (var data in shopDatas)
        {
            if (data == null || string.IsNullOrEmpty(data.ShopID)) continue;
            _shopDict[data.ShopID] = data;
        }
        
        Debug.Log($"[DynamicShopTable] Initialized with {_shopDict.Count} shops.");
    }

    public DynamicShopData Get(string shopID)
    {
        if (_shopDict.TryGetValue(shopID, out var data))
            return data;
        return null;
    }

    public IEnumerable<DynamicShopData> AllShops => _shopDict.Values;
}
