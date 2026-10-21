using System;
using System.Collections.Generic;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using Random = UnityEngine.Random;

namespace GaeGGUL.Shop
{
    public class DailyShopManager
    {
        private const string PLAYER_SHOP_DATA_TABLE = "PlayerShopData";
        private const string COLUMN_DAILY_ITEM_IDS = "DailyItemIDs";
        private const string COLUMN_PURCHASED_ITEM_IDS = "PurchasedItemIDs";

        private List<ShopItemData> _dailyItems = new List<ShopItemData>();
        private HashSet<int> _purchasedItemIDs = new HashSet<int>();
        private string _inDate = string.Empty;
        private string _owner;
        private int _generation;
        private bool _ready;
        private bool _buying;

        /// <summary>Current session identity; override only for isolated store verification.</summary>
        protected virtual string CurrentOwner => Backend.UserInDate;
        /// <summary>Server-time daily reset decision.</summary>
        protected virtual bool IsNewDay => Player.Daily.IsNewDay;
        /// <summary>Current account balances.</summary>
        protected virtual PlayerDataController Balances => Player.PlayerData;

        public List<ShopItemData> DailyItems => _dailyItems;
        public HashSet<int> PurchasedItemIDs => _purchasedItemIDs;

        public void Clear()
        {
            _generation++; _owner = null; _ready = false; _buying = false;
            _dailyItems.Clear();
            _purchasedItemIDs.Clear();
            _inDate = string.Empty;
        }

        public async UniTask InitializeAsync()
        {
            Clear(); _owner = CurrentOwner;
            if (string.IsNullOrEmpty(_owner)) throw new InvalidOperationException("Daily shop requires login.");
            int generation = _generation;
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
            try
            {
                var row = await ReadRowAsync(timeout.Token);
                RequireCurrent(generation);
                // Retain the existing row before rollover. A failed GET must never be interpreted as no row.
                if (row != null) _inDate = row["inDate"].ToString();
                if (IsNewDay || row == null)
                {
                    GenerateDailyItems();
                    await SaveAsync();
                    RequireCurrent(generation);
                }
                else
                {
                    _dailyItems.Clear();
                    JsonData dailyItemIds = row[COLUMN_DAILY_ITEM_IDS];
                    for (int i = 0; i < dailyItemIds.Count; i++)
                    {
                        int id = int.Parse(dailyItemIds[i].ToString());
                        var item = FindItem(id);
                        if (item != null) _dailyItems.Add(item);
                    }

                    _purchasedItemIDs.Clear();
                    JsonData purchasedIds = row[COLUMN_PURCHASED_ITEM_IDS];
                    for (int i = 0; i < purchasedIds.Count; i++)
                    {
                        _purchasedItemIDs.Add(int.Parse(purchasedIds[i].ToString()));
                    }
                }
                _ready = true;
            }
            catch (Exception error)
            {
                RequireCurrent(generation);
                // A legacy duplicate or unavailable optional shop must not block the rest of the lobby.
                // Keep purchases disabled, leave every server row intact, and retry on the next initialization.
                _ready = false;
                UnityEngine.Debug.LogWarning("Daily shop unavailable: " + error.Message);
            }
        }

        /// <summary>Reads one existing row; failed or ambiguous reads stop initialization without writing.</summary>
        protected virtual async UniTask<JsonData> ReadRowAsync(CancellationToken token)
        {
            var completion = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.GameData.GetMyData(PLAYER_SHOP_DATA_TABLE, new Where(), result => completion.TrySetResult(result));
            var response = await completion.Task.AttachExternalCancellation(token);
            if (!response.IsSuccess()) throw new InvalidOperationException("Daily shop read failed: " + response.GetStatusCode());
            var rows = response.FlattenRows();
            if (rows.Count > 1) throw new InvalidOperationException("Daily shop duplicate rows require repair.");
            return rows.Count == 0 ? null : rows[0];
        }

        /// <summary>Looks up an authored daily item.</summary>
        protected virtual ShopItemData FindItem(int id) => Table.Shop.Daily.GetItem(id);

        /// <summary>Creates the authored daily selection after a confirmed read.</summary>
        protected virtual void GenerateDailyItems()
        {
            _dailyItems.Clear();
            _purchasedItemIDs.Clear();

            var mandatoryFreeItem = Table.Shop.Daily.GetItem(1001);
            if (mandatoryFreeItem != null)
            {
                _dailyItems.Add(mandatoryFreeItem);
            }

            List<ShopItemData> pool = new List<ShopItemData>();
            foreach (var item in Table.Shop.Daily.AllItems)
            {
                if (item.ShopID == 1001) continue;
                pool.Add(item);
            }

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
            int generation = _generation;
            RequireCurrent(generation);
            Param param = new Param();
            
            List<int> dailyIds = new List<int>();
            foreach (var item in _dailyItems) dailyIds.Add(item.ShopID);
            param.Add(COLUMN_DAILY_ITEM_IDS, dailyIds);

            List<int> purchasedIds = new List<int>(_purchasedItemIDs);
            param.Add(COLUMN_PURCHASED_ITEM_IDS, purchasedIds);

            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
            string inDate = await WriteRowAsync(_inDate, _owner, param, timeout.Token);
            RequireCurrent(generation);
            _inDate = inDate;
        }

        /// <summary>Awaits an acknowledged row write; never silently accepts a failed save.</summary>
        protected virtual async UniTask<string> WriteRowAsync(string inDate, string owner, Param param, CancellationToken token)
        {
            var completion = new UniTaskCompletionSource<BackendReturnObject>();
            if (string.IsNullOrEmpty(inDate)) Backend.GameData.Insert(PLAYER_SHOP_DATA_TABLE, param, result => completion.TrySetResult(result));
            else Backend.GameData.UpdateV2(PLAYER_SHOP_DATA_TABLE, inDate, owner, param, result => completion.TrySetResult(result));
            var response = await completion.Task.AttachExternalCancellation(token);
            if (!response.IsSuccess()) throw new InvalidOperationException("Daily shop save failed: " + response.GetStatusCode());
            return string.IsNullOrEmpty(inDate) ? response.GetInDate() : inDate;
        }

        private void RequireCurrent(int generation)
        { if (generation != _generation || string.IsNullOrEmpty(_owner) || _owner != CurrentOwner) throw new OperationCanceledException("Daily shop account changed."); }

        /// <summary>Grants the selected authored reward after confirmed payment.</summary>
        protected virtual void GrantReward(ShopItemData item) => item.GetReward()?.GetReward();

        public async UniTask<bool> BuyItem(int shopID)
        {
            if (!_ready || _buying || _owner != CurrentOwner || !Balances.IsReady) return false;
            var item = FindItem(shopID);
            if (item == null || !_dailyItems.Exists(value => value.ShopID == shopID)) return false;
            if (_purchasedItemIDs.Contains(shopID)) return false;
            _buying = true;
            int generation = _generation;
            var balances = Balances;
            var snapshot = balances.Data;
            try
            {
                bool success = false;
                switch (item.CurrencyType)
                {
                    case CurrencyType.Free:
                        success = true;
                        break;
                    case CurrencyType.Diamond:
                        success = balances.SpendDiamond(item.Price);
                        break;
                    case CurrencyType.Gold:
                        success = balances.SpendGold(item.Price);
                        break;
                }

                if (success)
                {
                    // 재화 차감을 먼저 서버에 확정한 뒤 보상 지급/구매완료를 저장한다.
                    // (역순이면 재화 차감 전에 구매완료가 먼저 서버에 남아, 크래시 시 무료 획득이 발생할 수 있음)
                    bool paid = await balances.TrySaveAsync();
                    RequireCurrent(generation);
                    if (!ReferenceEquals(snapshot, balances.Data)) return false;
                    if (!paid)
                    {
                        // No reward was issued. Compensate the locally reserved price, even if acknowledgement was lost.
                        if (item.CurrencyType == CurrencyType.Diamond) balances.AddDiamond(item.Price);
                        else if (item.CurrencyType == CurrencyType.Gold) balances.AddGold(item.Price);
                        return false;
                    }
                    GrantReward(item);
                    _purchasedItemIDs.Add(shopID);
                    bool rewardSaved = await balances.TrySaveAsync();
                    RequireCurrent(generation);
                    if (!ReferenceEquals(snapshot, balances.Data)) return false;
                    if (!rewardSaved) { _ready = false; return false; }
                    await SaveAsync();
                    return true;
                }

                return false;
            }
            catch (Exception error)
            {
                if (generation == _generation) _ready = false;
                UnityEngine.Debug.LogWarning("Daily shop purchase incomplete: " + error.GetType().Name);
                return false;
            }
            finally { if (generation == _generation) _buying = false; }
        }

        public bool IsSoldOut(int shopID)
        {
            return _purchasedItemIDs.Contains(shopID);
        }
    }
}
