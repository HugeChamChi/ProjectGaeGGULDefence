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

    private string _inDate = string.Empty;
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
            JsonData row = bro.FlattenRows()[0];
            _inDate = row["inDate"].ToString();

            if (row.ContainsKey(COLUMN_SHOPS))
            {
                JsonData shops = row[COLUMN_SHOPS];
                if (shops != null && shops.IsArray)
                {
                    for (int i = 0; i < shops.Count; i++)
                    {
                        JsonData shopJson = shops[i];
                        if (shopJson.ContainsKey("ShopID") && shopJson.ContainsKey("IsCompleted"))
                        {
                            string id = shopJson["ShopID"].ToString();
                            bool completed = shopJson["IsCompleted"].ToString().ToLower() == "true";

                            if (completed)
                            {
                                _completedShops.Add(id);
                            }
                            else if (shopJson.ContainsKey("StartTime"))
                            {
                                if (long.TryParse(shopJson["StartTime"].ToString(), out long time))
                                {
                                    _activeShops[id] = time;
                                }
                            }
                        }
                    }
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
        List<Param> shopList = new List<Param>();
        
        foreach (var id in _completedShops)
        {
            Param shop = new Param();
            shop.Add("ShopID", id);
            shop.Add("IsCompleted", true);
            shopList.Add(shop);
        }

        foreach (var kvp in _activeShops)
        {
            Param shop = new Param();
            shop.Add("ShopID", kvp.Key);
            shop.Add("IsCompleted", false);
            shop.Add("StartTime", kvp.Value.ToString());
            shopList.Add(shop);
        }

        param.Add(COLUMN_SHOPS, shopList);

        if (!string.IsNullOrEmpty(_inDate))
        {
            Backend.GameData.UpdateV2(PLAYER_DYNAMIC_SHOP_TABLE, _inDate, Backend.UserInDate, param);
        }
        else
        {
            // 데이터가 없는 경우 새로 삽입
            var bro = Backend.GameData.Insert(PLAYER_DYNAMIC_SHOP_TABLE, param);
            if (bro.IsSuccess())
            {
                _inDate = bro.GetInDate();
            }
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
