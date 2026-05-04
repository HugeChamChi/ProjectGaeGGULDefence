using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using GaeGGUL.Core;

public class DynamicShopDataManager
{
    private const string PLAYER_DYNAMIC_SHOP_TABLE = "PlayerDynamicShopData";
    private const string COLUMN_SHOPS = "Shops";

    private Dictionary<string, long> _activeShops = new Dictionary<string, long>(); // ShopID -> StartTime (Ticks/Unix)
    private HashSet<string> _completedShops = new HashSet<string>();

    public event Action<string, bool> OnShopActivated; // shopID, isFirstTime
    public event Action<string> OnShopCompleted;      // shopID

    public bool IsCompleted(string shopID) => _completedShops.Contains(shopID);
    public bool IsActive(string shopID) => _activeShops.ContainsKey(shopID);
    public DateTime GetStartTime(string shopID) => _activeShops.TryGetValue(shopID, out long time) ? new DateTime(time) : DateTime.MinValue;

    public async UniTask InitializeAsync()
    {
        var bro = Backend.GameData.GetMyData(PLAYER_DYNAMIC_SHOP_TABLE, new Where());
        if (bro.IsSuccess() && bro.FlattenRows().Count > 0)
        {
            JsonData shops = bro.FlattenRows()[0][COLUMN_SHOPS];
            for (int i = 0; i < shops.Count; i++)
            {
                string id = shops[i]["ShopID"].ToString();
                bool completed = (bool)shops[i]["IsCompleted"];
                
                if (completed)
                {
                    _completedShops.Add(id);
                }
                else if (shops[i].Keys.Contains("StartTime"))
                {
                    _activeShops[id] = long.Parse(shops[i]["StartTime"].ToString());
                }
            }
        }
    }

    public async UniTask TriggerShopAsync(string shopID)
    {
        if (IsCompleted(shopID) || IsActive(shopID)) return;

        _activeShops[shopID] = Server.GetServerTime().Ticks;
        await SaveAsync();
        
        OnShopActivated?.Invoke(shopID, true);
    }

    public async UniTask CompleteShopAsync(string shopID)
    {
        if (_activeShops.ContainsKey(shopID))
        {
            _activeShops.Remove(shopID);
        }
        _completedShops.Add(shopID);
        await SaveAsync();

        OnShopCompleted?.Invoke(shopID);
    }

    private async UniTask SaveAsync()
    {
        Param param = new Param();
        JsonData shopList = new JsonData();
        
        foreach (var id in _completedShops)
        {
            JsonData shop = new JsonData();
            shop["ShopID"] = id;
            shop["IsCompleted"] = true;
            shopList.Add(shop);
        }

        foreach (var kvp in _activeShops)
        {
            JsonData shop = new JsonData();
            shop["ShopID"] = kvp.Key;
            shop["IsCompleted"] = false;
            shop["StartTime"] = kvp.Value.ToString();
            shopList.Add(shop);
        }

        param.Add(COLUMN_SHOPS, shopList);

        var bro = Backend.GameData.GetMyData(PLAYER_DYNAMIC_SHOP_TABLE, new Where());
        if (bro.IsSuccess() && bro.FlattenRows().Count > 0)
        {
            string inDate = bro.FlattenRows()[0]["inDate"].ToString();
            Backend.GameData.UpdateV2(PLAYER_DYNAMIC_SHOP_TABLE, inDate, Backend.UserInDate, param);
        }
        else
        {
            Backend.GameData.Insert(PLAYER_DYNAMIC_SHOP_TABLE, param);
        }
    }

    // 초기 접속 시 활성화된 상점들을 UI에 알리기 위한 메서드
    public void CheckActiveShops()
    {
        foreach (var kvp in _activeShops)
        {
            OnShopActivated?.Invoke(kvp.Key, false);
        }
    }
}
