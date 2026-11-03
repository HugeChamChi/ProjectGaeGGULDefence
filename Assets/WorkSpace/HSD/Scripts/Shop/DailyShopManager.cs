using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly bool _weekly;
        private string ItemIdsColumn => _weekly ? "WeeklyItemIDs" : "DailyItemIDs";
        private string PurchasedColumn => _weekly ? "WeeklyPurchasedItemIDs" : "PurchasedItemIDs";
        private string PeriodColumn => _weekly ? "WeeklyPeriodKey" : "DailyPeriodKey";
        private string ItemsColumn => _weekly ? "WeeklyItemsJson" : "DailyItemsJson";
        private string _periodKey;

        public DailyShopManager(bool weekly = false) { _weekly = weekly; }

        public static string GetPeriodKey(DateTime utc, bool weekly)
        {
            DateTime date = utc.AddHours(9 - 5).Date;
            if (weekly) date = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        protected virtual string CurrentPeriodKey
        {
            get
            {
                if (!ServerClock.IsSynced) throw new InvalidOperationException("Shop requires server time.");
                return GetPeriodKey(ServerClock.UtcNow, _weekly);
            }
        }

        protected virtual IEnumerable<ShopItemData> ItemPool => Table.Shop.Daily.AllItems;

        private List<ShopItemData> _dailyItems = new List<ShopItemData>();
        private HashSet<int> _purchasedItemIDs = new HashSet<int>();
        private string _inDate = string.Empty;
        private string _owner;
        private int _generation;
        private bool _ready;
        private bool _buying;
        private bool _initializing;

        /// <summary>Current session identity; override only for isolated store verification.</summary>
        protected virtual string CurrentOwner => Backend.UserInDate;
        /// <summary>Server-time daily reset decision.</summary>
        protected virtual bool IsNewDay => Player.Daily.IsNewDay;
        /// <summary>Current account balances.</summary>
        protected virtual PlayerDataController Balances => Player.PlayerData;

        public List<ShopItemData> DailyItems => _dailyItems;
        public HashSet<int> PurchasedItemIDs => _purchasedItemIDs;
        public bool IsReady => _ready;

        public void Clear()
        {
            _generation++; _owner = null; _ready = false; _buying = false;
            _dailyItems.Clear();
            _purchasedItemIDs.Clear();
            _inDate = string.Empty;
            _periodKey = null;
        }

        public async UniTask InitializeAsync()
        {
            if (_initializing || _buying) return;
            _initializing = true;
            Clear(); _owner = CurrentOwner;
            int generation = _generation;
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
            try
            {
                if (string.IsNullOrEmpty(_owner)) throw new InvalidOperationException("Daily shop requires login.");
                var row = await ReadRowAsync(timeout.Token);
                RequireCurrent(generation);
                // Retain the existing row before rollover. A failed GET must never be interpreted as no row.
                if (row != null) _inDate = row["inDate"].ToString();
                string period = CurrentPeriodKey;
                bool hasPeriod = row != null && row.ContainsKey(PeriodColumn);
                bool hasItems = row != null && row.ContainsKey(ItemIdsColumn);
                if (!hasItems || (hasPeriod ? row[PeriodColumn].ToString() != period : IsNewDay))
                {
                    GenerateDailyItems();
                    _periodKey = period;
                    await SaveAsync();
                    RequireCurrent(generation);
                }
                else
                {
                    _dailyItems.Clear();
                    JsonData dailyItemIds = row[ItemIdsColumn];
                    if (row.ContainsKey(ItemsColumn))
                    {
                        var savedItems = JsonMapper.ToObject<List<ShopItemData>>(row[ItemsColumn].ToString());
                        if (savedItems == null || savedItems.Count != dailyItemIds.Count)
                            throw new InvalidOperationException("Invalid saved shop items.");
                        for (int i = 0; i < savedItems.Count; i++)
                            if (savedItems[i].ShopID != int.Parse(dailyItemIds[i].ToString()))
                                throw new InvalidOperationException("Saved shop item IDs do not match.");
                        _dailyItems = savedItems;
                    }
                    else
                    {
                        for (int i = 0; i < dailyItemIds.Count; i++)
                        {
                            int id = int.Parse(dailyItemIds[i].ToString());
                            var item = id == 1001
                                ? new ShopItemData(1001, ItemType.Currency, (int)CurrencyType.Diamond, 10, CurrencyType.Free, 0)
                                : FindItem(id);
                            if (item == null) throw new InvalidOperationException("Saved shop item is missing from chart.");
                            _dailyItems.Add(item);
                        }
                    }

                    _purchasedItemIDs.Clear();
                    JsonData purchasedIds = row[PurchasedColumn];
                    for (int i = 0; i < purchasedIds.Count; i++)
                    {
                        _purchasedItemIDs.Add(int.Parse(purchasedIds[i].ToString()));
                    }
                    _periodKey = period;
                    // Preserve legacy purchases while adding an independent period stamp and item snapshot.
                    if (!hasPeriod || !row.ContainsKey(ItemsColumn)) await SaveAsync();
                }
                RequireCurrent(generation);
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
            finally { _initializing = false; }
        }

        public async UniTask<bool> RefreshIfNeededAsync()
        {
            if (_initializing || _buying || string.IsNullOrEmpty(CurrentOwner) || !ServerClock.IsSynced) return false;
            if (_ready && _periodKey == CurrentPeriodKey) return false;
            await InitializeAsync();
            return _ready;
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

            _dailyItems.Add(new ShopItemData(1001, ItemType.Currency, (int)CurrencyType.Diamond, 10, CurrencyType.Free, 0));

            List<ShopItemData> pool = new List<ShopItemData>();
            foreach (var item in ItemPool)
            {
                if (item.ShopID == 1001) continue;
                pool.Add(item);
            }

            int itemsToPick = 5;
            if (pool.Count < itemsToPick) throw new InvalidOperationException("Shop needs at least five random products.");
            for (int i = 0; i < itemsToPick && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                var item = pool[index];
                _dailyItems.Add(new ShopItemData(item.ShopID, item.Type, item.ItemID, item.Amount, item.CurrencyType, item.Price));
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
            param.Add(ItemIdsColumn, dailyIds);
            param.Add(PeriodColumn, _periodKey);
            param.Add(ItemsColumn, JsonMapper.ToJson(_dailyItems));

            List<int> purchasedIds = new List<int>(_purchasedItemIDs);
            param.Add(PurchasedColumn, purchasedIds);

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
            // An open shop must not sell the previous period's stock after the reset boundary.
            if (!ServerClock.IsSynced) return false;
            if (_periodKey != CurrentPeriodKey) { await InitializeAsync(); return false; }
            var item = _dailyItems.Find(value => value.ShopID == shopID);
            if (item == null) return false;
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
