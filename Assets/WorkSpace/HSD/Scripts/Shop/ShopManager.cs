using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShopManager
{
    private static ShopManager _instance;
    public static ShopManager Instance => _instance ??= new ShopManager();

    private Dictionary<int, ShopItemData> _allShopItems = new Dictionary<int, ShopItemData>();
    private List<ShopItemData> _dailyItems = new List<ShopItemData>();
    private HashSet<int> _purchasedItemIDs = new HashSet<int>();

    public List<ShopItemData> DailyItems => _dailyItems;
    public HashSet<int> PurchasedItemIDs => _purchasedItemIDs;

    private const string SHOP_DAILY_CHART_ID = "238813"; // 실제 차트 이름으로 변경 필요
    private const string Player_SHOP_DATA_TABLE = "PlayerShopData";

    public async UniTask InitializeAsync()
    {
        await LoadShopChartAsync();
        await LoadUserShopDataAsync();
    }

    private async UniTask LoadShopChartAsync()
    {
        Backend.Chart.GetLocalChartData(SHOP_DAILY_CHART_ID);

        // 실제로는 ServerDataProvider 등을 사용하거나 직접 Backend API 호출
        var bro = Backend.Chart.GetChartContents(SHOP_DAILY_CHART_ID);
        if (bro.IsSuccess())
        {
            JsonData rows = bro.FlattenRows();
            _allShopItems.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                var item = ParseShopItem(rows[i]);
                _allShopItems.Add(item.ShopID, item);
            }
        }
        else
        {
            Debug.LogError($"Failed to load Shop Chart: {bro}");
            // Test Data for development
            LoadTestData();
        }
    }

    private void LoadTestData()
    {
        _allShopItems.Clear();
        // Free Items
        _allShopItems.Add(1, new ShopItemData(1, ItemType.Currency, (int)CurrencyType.Gold, 1000, CurrencyType.Free, 0));
        _allShopItems.Add(2, new ShopItemData(2, ItemType.Currency, (int)CurrencyType.Diamond, 10, CurrencyType.Free, 0));
        
        // Paid Items
        for (int i = 3; i <= 20; i++)
        {
            _allShopItems.Add(i, new ShopItemData(i, ItemType.Item, i * 100, 1, CurrencyType.Gold, i * 500));
        }
    }

    private ShopItemData ParseShopItem(JsonData data)
    {
        ShopItemData item = new ShopItemData();
        
        // 뒤끝 차트의 컬럼명은 대소문자를 구분합니다. 
        // 서버 설정에 따라 소문자로 시작할 수도 있으므로 유연하게 처리합니다.
        item.ShopID = GetInt(data, "ShopID");
        item.ItemID = GetInt(data, "ItemID");
        item.Amount = GetInt(data, "Amount");
        item.Price = GetInt(data, "Price");

        string typeStr = GetString(data, "Type");
        if (Enum.TryParse(typeStr, out ItemType itemType))
            item.Type = itemType;

        string currencyStr = GetString(data, "CurrencyType");
        if (Enum.TryParse(currencyStr, out CurrencyType currencyType))
            item.CurrencyType = currencyType;

        return item;
    }

    private int GetInt(JsonData data, string key)
    {
        if (data.ContainsKey(key)) return int.Parse(data[key].ToString());
        string lowerKey = key.ToLower();
        if (data.ContainsKey(lowerKey)) return int.Parse(data[lowerKey].ToString());
        
        Debug.LogWarning($"ShopManager: Key '{key}' not found in Chart data.");
        return 0;
    }

    private string GetString(JsonData data, string key)
    {
        if (data.ContainsKey(key)) return data[key].ToString();
        string lowerKey = key.ToLower();
        if (data.ContainsKey(lowerKey)) return data[lowerKey].ToString();
        
        return string.Empty;
    }

    private async UniTask LoadUserShopDataAsync()
    {
        var bro = Backend.GameData.GetMyData(Player_SHOP_DATA_TABLE, new Where());
        if (bro.IsSuccess() && bro.FlattenRows().Count > 0)
        {
            JsonData row = bro.FlattenRows()[0];
            string lastUpdateDate = row["LastUpdateDate"].ToString();
            
            if (lastUpdateDate != DateTime.Now.ToString("yyyy-MM-dd"))
            {
                // New day, refresh daily items
                GenerateDailyItems();
                await SaveUserShopDataAsync();
            }
            else
            {
                // Load existing daily items
                _dailyItems.Clear();
                JsonData dailyItemIds = row["DailyItemIDs"];
                for (int i = 0; i < dailyItemIds.Count; i++)
                {
                    int id = int.Parse(dailyItemIds[i].ToString());
                    if (_allShopItems.TryGetValue(id, out var item))
                    {
                        _dailyItems.Add(item);
                    }
                }

                _purchasedItemIDs.Clear();
                JsonData purchasedIds = row["PurchasedItemIDs"];
                for (int i = 0; i < purchasedIds.Count; i++)
                {
                    _purchasedItemIDs.Add(int.Parse(purchasedIds[i].ToString()));
                }
            }
        }
        else
        {
            // No data, generate first time
            GenerateDailyItems();
            await SaveUserShopDataAsync();
        }
    }

    private void GenerateDailyItems()
    {
        _dailyItems.Clear();
        _purchasedItemIDs.Clear();

        List<ShopItemData> freePool = new List<ShopItemData>();
        List<ShopItemData> normalPool = new List<ShopItemData>();

        foreach (var item in _allShopItems.Values)
        {
            if (item.CurrencyType == CurrencyType.Free)
                freePool.Add(item);
            else
                normalPool.Add(item);
        }

        // Pick 1 free item (flexible count later)
        if (freePool.Count > 0)
        {
            _dailyItems.Add(freePool[Random.Range(0, freePool.Count)]);
        }

        // Pick 5 normal items
        int itemsToPick = 5;
        for (int i = 0; i < itemsToPick && normalPool.Count > 0; i++)
        {
            int index = Random.Range(0, normalPool.Count);
            _dailyItems.Add(normalPool[index]);
            normalPool.RemoveAt(index);
        }
    }

    private async UniTask SaveUserShopDataAsync()
    {
        Param param = new Param();
        param.Add("LastUpdateDate", DateTime.Now.ToString("yyyy-MM-dd"));
        
        List<int> dailyIds = new List<int>();
        foreach (var item in _dailyItems) dailyIds.Add(item.ShopID);
        param.Add("DailyItemIDs", dailyIds);

        List<int> purchasedIds = new List<int>(_purchasedItemIDs);
        param.Add("PurchasedItemIDs", purchasedIds);

        var bro = Backend.GameData.GetMyData(Player_SHOP_DATA_TABLE, new Where());
        if (bro.IsSuccess() && bro.FlattenRows().Count > 0)
        {
            string inDate = bro.FlattenRows()[0]["inDate"].ToString();
            Backend.GameData.UpdateV2(Player_SHOP_DATA_TABLE, inDate, Backend.UserInDate, param);
        }
        else
        {
            Backend.GameData.Insert(Player_SHOP_DATA_TABLE, param);
        }
    }

    public async UniTask<bool> BuyItem(int shopID)
    {
        if (!_allShopItems.TryGetValue(shopID, out var item)) return false;
        if (_purchasedItemIDs.Contains(shopID)) return false;

        bool success = false;
        switch (item.CurrencyType)
        {
            case CurrencyType.Free:
                success = true;
                break;
            case CurrencyType.Diamond:
                success = Player.PlayerData.SpendDiamond(item.Price);
                break;
            case CurrencyType.Gold:
                success = Player.PlayerData.SpendGold(item.Price);
                break;
        }

        if (success)
        {
            item.GetReward()?.GetReward();
            _purchasedItemIDs.Add(shopID);
            await SaveUserShopDataAsync();
            return true;
        }

        return false;
    }

    public bool IsSoldOut(int shopID)
    {
        return _purchasedItemIDs.Contains(shopID);
    }
}
