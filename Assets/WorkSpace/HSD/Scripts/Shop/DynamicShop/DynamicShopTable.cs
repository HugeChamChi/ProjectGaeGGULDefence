using System.Collections.Generic;
using UnityEngine;

public class DynamicShopTable
{
    private Dictionary<string, DynamicShopData> _shopDict = new Dictionary<string, DynamicShopData>();

    public void Initialize()
    {
        _shopDict.Clear();
        // RM.LoadAll을 사용하여 Resources/Data/DynamicShopData 폴더 내의 모든 SO를 로드합니다.
        var shopDatas = RM.LoadAll<DynamicShopData>("Data/DynamicShopData");
        
        if (shopDatas == null || shopDatas.Length == 0)
        {
            Debug.LogWarning("[DynamicShopTable] No DynamicShopData found in Resources/Data/DynamicShopData");
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
