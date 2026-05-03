using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class ShopTableManager
{
    private Dictionary<int, ShopItemData> _allShopItems = new Dictionary<int, ShopItemData>();
    public IEnumerable<ShopItemData> AllItems => _allShopItems.Values;

    private const string SHOP_DAILY_CHART_ID = "238813"; // 실제 차트 ID

    public async UniTask InitializeAsync()
    {
        // 로컬 캐시 데이터 로드 시도
        Backend.Chart.GetLocalChartData(SHOP_DAILY_CHART_ID);

        var bro = Backend.Chart.GetChartContents(SHOP_DAILY_CHART_ID);
        if (bro.IsSuccess())
        {
            JsonData rows = bro.FlattenRows();
            _allShopItems.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                var item = ParseShopItem(rows[i]);
                if (item != null)
                {
                    _allShopItems[item.ShopID] = item;
                }
            }
            Debug.Log($"ShopTable: Loaded {_allShopItems.Count} items from chart.");
        }
        else
        {
            Debug.LogError($"Failed to load Shop Chart: {bro}");
        }
    }

    public ShopItemData GetItem(int shopID)
    {
        if (_allShopItems.TryGetValue(shopID, out var item))
            return item;
        return null;
    }

    private ShopItemData ParseShopItem(JsonData data)
    {
        ShopItemData item = new ShopItemData();
        
        item.ShopID = GetInt(data, "ShopID");
        item.ItemID = GetInt(data, "ItemID");
        item.Amount = GetInt(data, "Amount");
        item.Price = GetInt(data, "Price");

        string typeStr = GetString(data, "ItemType");
        if (Enum.TryParse(typeStr, true, out ItemType itemType))
        {
            item.Type = itemType;
        }
        else
        {
            Debug.LogWarning($"ShopTable: Failed to parse ItemType from '{typeStr}' for ShopID {item.ShopID}. Defaulting to Item.");
        }

        string currencyStr = GetString(data, "CurrencyType");
        if (Enum.TryParse(currencyStr, true, out CurrencyType currencyType))
        {
            item.CurrencyType = currencyType;
        }
        else
        {
             Debug.LogWarning($"ShopTable: Failed to parse CurrencyType from '{currencyStr}' for ShopID {item.ShopID}. Defaulting to Gold.");
        }

        Debug.Log($"ShopTable Parsed: ID:{item.ShopID}, Type:{item.Type}, ItemID:{item.ItemID}, Currency:{item.CurrencyType}");

        return item;
    }

    private int GetInt(JsonData data, string key)
    {
        if (data.ContainsKey(key)) return int.Parse(data[key].ToString());
        string lowerKey = key.ToLower();
        if (data.ContainsKey(lowerKey)) return int.Parse(data[lowerKey].ToString());
        return 0;
    }

    private string GetString(JsonData data, string key)
    {
        if (data.ContainsKey(key)) return data[key].ToString();
        string lowerKey = key.ToLower();
        if (data.ContainsKey(lowerKey)) return data[lowerKey].ToString();
        return string.Empty;
    }
}
