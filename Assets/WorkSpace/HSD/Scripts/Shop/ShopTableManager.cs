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

    public async UniTask InitializeAsync()
    {
        var bro = Backend.Chart.GetChartContents(Chart.SHOP_DAILY_ID);
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
        
        item.ShopID = data.GetInt("ShopID");
        item.ItemID = data.GetInt("ItemID");
        item.Amount = data.GetInt("Amount");
        item.Price = data.GetInt("Price");
        string typeStr = data.GetString("ItemType");
        string currencyStr = data.GetString("CurrencyType");

        if (Enum.TryParse(typeStr, true, out ItemType itemType))
            item.Type = itemType;
        else
            Debug.LogWarning($"ShopTable: Failed to parse ItemType from '{typeStr}' for ShopID {item.ShopID}. Defaulting to Item.");

        if (Enum.TryParse(currencyStr, true, out CurrencyType currencyType))
            item.CurrencyType = currencyType;
        else
             Debug.LogWarning($"ShopTable: Failed to parse CurrencyType from '{currencyStr}' for ShopID {item.ShopID}. Defaulting to Gold.");

        Debug.Log($"ShopTable Parsed: ID:{item.ShopID}, Type:{item.Type}, ItemID:{item.ItemID}, Currency:{item.CurrencyType}");

        return item;
    }
}
