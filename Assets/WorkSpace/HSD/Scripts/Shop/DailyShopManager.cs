using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using System.Linq;

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

        protected virtual IEnumerable<ShopItemData> ItemPool => Table.Shop.Catalog.IsReady
            ? Table.Shop.Catalog.Products(ShopCategory.Daily).Select(p => p.Item)
            : throw new InvalidOperationException("ShopCatalog chart is not available.");

        private List<ShopItemData> _dailyItems = new List<ShopItemData>();
        // Sold-out IDs (count reached the chart Limit) and per-item purchase counts for this period.
        // The saved PurchasedItemIDs column repeats an ID once per purchase (ad rewards allow 2 a day).
        private HashSet<int> _purchasedItemIDs = new HashSet<int>();
        private readonly Dictionary<int, int> _purchaseCounts = new Dictionary<int, int>();
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
        protected virtual bool HasTrustedTime => ServerClock.IsSynced;

        public List<ShopItemData> DailyItems => _dailyItems;
        public HashSet<int> PurchasedItemIDs => _purchasedItemIDs;
        /// <summary>Purchases of an item in the current period.</summary>
        public int PurchaseCount(int shopID) => _purchaseCounts.TryGetValue(shopID, out int n) ? n : 0;
        /// <summary>Purchases allowed per period from the chart (at least 1).</summary>
        public static int LimitOf(int shopID)
        {
            var product = Table.Shop.Catalog.IsReady ? Table.Shop.Catalog.Products(ShopCategory.Daily).FirstOrDefault(p => p.Item.ShopID == shopID) : null;
            return Math.Max(1, product?.Limit ?? 1);
        }
        private void SetCount(int shopID, int count)
        {
            if (count <= 0) { _purchaseCounts.Remove(shopID); _purchasedItemIDs.Remove(shopID); return; }
            _purchaseCounts[shopID] = count;
            if (count >= LimitOf(shopID)) _purchasedItemIDs.Add(shopID); else _purchasedItemIDs.Remove(shopID);
        }
        public bool IsReady => _ready;

        public void Clear()
        {
            _generation++; _owner = null; _ready = false; _buying = false;
            _dailyItems.Clear();
            _purchasedItemIDs.Clear();
            _purchaseCounts.Clear();
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
                bool hasItems = row != null && row.ContainsKey(ItemIdsColumn) && (hasPeriod || row[ItemIdsColumn].Count > 0);
                if (!hasItems || (hasPeriod ? row[PeriodColumn].ToString() != period : IsNewDay))
                {
                    await GenerateDailyItemsAsync(timeout.Token);
                    RequireCurrent(generation);
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
                            // An id the current chart no longer sells makes the list differ; it is regenerated below.
                            if (item != null) _dailyItems.Add(item);
                        }
                    }

                    _purchasedItemIDs.Clear(); _purchaseCounts.Clear();
                    JsonData purchasedIds = row[PurchasedColumn];
                    for (int i = 0; i < purchasedIds.Count; i++)
                    {
                        int id = int.Parse(purchasedIds[i].ToString());
                        SetCount(id, PurchaseCount(id) + 1);
                    }
                    _periodKey = period;
                    // Daily stock is the fixed chart list. A row saved from an older list (random draw era or a chart
                    // edit) is replaced now; purchases are kept per ShopID and recovered from the account below.
                    // Compare the full terms, not only IDs: a saved snapshot must never charge an old price or cost.
                    bool listChanged = !_dailyItems.Select(Terms).SequenceEqual(ChartItems().Select(Terms));
                    if (listChanged)
                    {
                        var kept = new Dictionary<int, int>(_purchaseCounts);
                        await GenerateDailyItemsAsync(timeout.Token);
                        RequireCurrent(generation);
                        foreach (var item in _dailyItems) if (kept.TryGetValue(item.ShopID, out int n)) SetCount(item.ShopID, n);
                    }
                    // Preserve legacy purchases while adding an independent period stamp and item snapshot.
                    if (listChanged || !hasPeriod || !row.ContainsKey(ItemsColumn)) await SaveAsync();
                }
                RequireCurrent(generation);
                // Recover a purchase whose account save succeeded before the stock row write.
                foreach (var item in _dailyItems)
                    SetCount(item.ShopID, Math.Max(PurchaseCount(item.ShopID), Balances.ShopPurchaseCount(_periodKey, item.ShopID, _weekly)));
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
            Backend.GameData.GetMyData(PLAYER_SHOP_DATA_TABLE, new Where(), 100, result => completion.TrySetResult(result));
            var response = await completion.Task.AttachExternalCancellation(token);
            if (!response.IsSuccess()) throw new InvalidOperationException("Daily shop read failed: " + response.GetStatusCode());
            var rows = response.FlattenRows();
            return SelectSavedRow(rows);
        }

        public static JsonData SelectSavedRow(JsonData rows)
        {
            // The old implementation inserted empty rows repeatedly. Preserve them, but do not
            // let them hide one real stock row. Multiple populated rows remain ambiguous.
            if (rows.Count >= 100) throw new InvalidOperationException("Shop row query reached its limit; repair required.");
            JsonData selected = null, legacy = null;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                // Legacy rows without a period stamp and without purchases carry nothing to protect, even when they
                // hold an old stock list (2026-10 duplicate inserts, TODO V06). Rows with purchases stay ambiguous.
                bool emptyLegacy = row.ContainsKey("DailyItemIDs") && row["DailyItemIDs"].IsArray &&
                    row.ContainsKey("PurchasedItemIDs") && row["PurchasedItemIDs"].IsArray && row["PurchasedItemIDs"].Count == 0 &&
                    !row.ContainsKey("DailyPeriodKey") && !row.ContainsKey("WeeklyPeriodKey") &&
                    !row.ContainsKey("DailyItemsJson") && !row.ContainsKey("WeeklyItemsJson") &&
                    !row.ContainsKey("WeeklyItemIDs") && !row.ContainsKey("WeeklyPurchasedItemIDs");
                if (emptyLegacy)
                {
                    if (legacy == null || string.CompareOrdinal(row["inDate"].ToString(), legacy["inDate"].ToString()) > 0) legacy = row;
                }
                else
                {
                    if (selected != null) throw new InvalidOperationException("Daily shop duplicate populated rows require repair.");
                    selected = row;
                }
            }
            return selected ?? legacy;
        }

        /// <summary>Looks up an authored daily item.</summary>
        protected virtual ShopItemData FindItem(int id) => ItemPool.FirstOrDefault(item => item.ShopID == id);

        /// <summary>Creates the authored daily selection after a confirmed read.</summary>
        protected virtual UniTask GenerateDailyItemsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _dailyItems.Clear();
            _purchasedItemIDs.Clear();
            _purchaseCounts.Clear();
            foreach (var item in ChartItems())
                _dailyItems.Add(new ShopItemData(item.ShopID, item.Type, item.ItemID, item.Amount, item.CurrencyType, item.Price));
            return UniTask.CompletedTask;
        }

        private static string Terms(ShopItemData item) => $"{item.ShopID}:{item.Type}:{item.ItemID}:{item.Amount}:{item.CurrencyType}:{item.Price}";

        /// <summary>The fixed authored list for this period kind (weekly stock is no longer sold and stays empty).</summary>
        protected virtual IEnumerable<ShopItemData> ChartItems() => _weekly ? Enumerable.Empty<ShopItemData>() : ItemPool;

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

            List<int> purchasedIds = _purchaseCounts.SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value)).ToList();
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

        public async UniTask<bool> BuyItem(int shopID)
        {
            if (!_ready || _buying || _owner != CurrentOwner || !Balances.IsReady) return false;
            // An open shop must not sell the previous period's stock after the reset boundary.
            if (!HasTrustedTime) return false;
            if (_periodKey != CurrentPeriodKey) { await InitializeAsync(); return false; }
            var item = _dailyItems.Find(value => value.ShopID == shopID);
            if (item == null) return false;
            int limit = LimitOf(shopID);
            if (PurchaseCount(shopID) >= limit) return false;
            _buying = true;
            int generation = _generation;
            var balances = Balances;
            try
            {
                // Payment, reward and deduplication receipt share the same account snapshot.
                if (!balances.TryPurchaseShopItem(item, _periodKey, _weekly, limit: limit)) return false;
                SetCount(shopID, PurchaseCount(shopID) + 1);
                if (!await balances.TrySaveAsync()) { _ready = false; return false; }
                RequireCurrent(generation);
                await SaveAsync();
                RequireCurrent(generation);
                return true;
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
