using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using Random = UnityEngine.Random;

public class ShopDataManager
{
    private List<ShopItemData> _dailyItems = new List<ShopItemData>();
    private HashSet<int> _purchasedItemIDs = new HashSet<int>();

    public List<ShopItemData> DailyItems => _dailyItems;
    public HashSet<int> PurchasedItemIDs => _purchasedItemIDs;

    private const string PLAYER_SHOP_DATA_TABLE = "PlayerShopData";

    public async UniTask InitializeAsync()
    {
        var bro = Backend.GameData.GetMyData(PLAYER_SHOP_DATA_TABLE, new Where());
        bool hasData = bro.IsSuccess() && bro.FlattenRows().Count > 0;

        if (Player.Daily.IsNewDay || !hasData)
        {
            GenerateDailyItems();
            await SaveAsync();
        }
        else
        {
            JsonData row = bro.FlattenRows()[0];
            _dailyItems.Clear();
            JsonData dailyItemIds = row["DailyItemIDs"];
            for (int i = 0; i < dailyItemIds.Count; i++)
            {
                int id = int.Parse(dailyItemIds[i].ToString());
                var item = Table.Shop.GetItem(id);
                if (item != null) _dailyItems.Add(item);
            }

            _purchasedItemIDs.Clear();
            JsonData purchasedIds = row["PurchasedItemIDs"];
            for (int i = 0; i < purchasedIds.Count; i++)
            {
                _purchasedItemIDs.Add(int.Parse(purchasedIds[i].ToString()));
            }
        }
    }

    private void GenerateDailyItems()
    {
        _dailyItems.Clear();
        _purchasedItemIDs.Clear();

        // 1. 1001번 무료 아이템 무조건 포함
        var mandatoryFreeItem = Table.Shop.GetItem(1001);
        if (mandatoryFreeItem != null)
        {
            _dailyItems.Add(mandatoryFreeItem);
        }
        else
        {
            UnityEngine.Debug.LogWarning("ShopDataManager: ShopID 1001번 아이템을 찾을 수 없습니다.");
        }

        // 2. 나머지 아이템 풀 구성 (1001번 제외)
        List<ShopItemData> pool = new List<ShopItemData>();
        foreach (var item in Table.Shop.AllItems)
        {
            if (item.ShopID == 1001) continue;
            pool.Add(item);
        }

        // 3. 풀에서 랜덤하게 5개 선정 (총 6개)
        int itemsToPick = 5;
        for (int i = 0; i < itemsToPick && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            _dailyItems.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }

    public async UniTask SaveAsync()
    {
        Param param = new Param();
        
        List<int> dailyIds = new List<int>();
        foreach (var item in _dailyItems) dailyIds.Add(item.ShopID);
        param.Add("DailyItemIDs", dailyIds);

        List<int> purchasedIds = new List<int>(_purchasedItemIDs);
        param.Add("PurchasedItemIDs", purchasedIds);

        var bro = Backend.GameData.GetMyData(PLAYER_SHOP_DATA_TABLE, new Where());
        if (bro.IsSuccess() && bro.FlattenRows().Count > 0)
        {
            string inDate = bro.FlattenRows()[0]["inDate"].ToString();
            Backend.GameData.UpdateV2(PLAYER_SHOP_DATA_TABLE, inDate, Backend.UserInDate, param);
        }
        else
        {
            Backend.GameData.Insert(PLAYER_SHOP_DATA_TABLE, param);
        }
    }

    public async UniTask<bool> BuyItem(int shopID)
    {
        var item = Table.Shop.GetItem(shopID);
        if (item == null) return false;
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
            await SaveAsync();
            return true;
        }

        return false;
    }

    public bool IsSoldOut(int shopID)
    {
        return _purchasedItemIDs.Contains(shopID);
    }
}
