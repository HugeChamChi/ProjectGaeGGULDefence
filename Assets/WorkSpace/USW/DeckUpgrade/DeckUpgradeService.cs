using System;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Owns preview progress and upgrade commands independently of the UI.</summary>
    public sealed class DeckUpgradeService : IDisposable
    {
        private readonly IDeckUpgradeStore _store;
        private DeckUpgradeProgress _progress;
        public bool IsBusy { get; private set; }
        private string _owner;

        /// <summary>Loads one progress instance for this scope.</summary>
        public DeckUpgradeService(IDeckUpgradeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _owner = BackEnd.Backend.UserInDate;
            _progress = UsesAccountData && !Player.Characters.IsReady
                ? new DeckUpgradeProgress { money = 0 }
                : store.Load() ?? new DeckUpgradeProgress();
            if (UsesAccountData)
            {
                Player.PlayerData.OnUpdateUI += HandleBalance;
                Player.OnAccountReady += HandleAccountReady;
                Player.OnAccountCleared += HandleAccountCleared;
            }
        }

        /// <summary>Raised after a successful upgrade has been saved.</summary>
        public event Action OnChanged;

        /// <summary>Account gold in the lobby, local balance in standalone previews.</summary>
        public int Gold => UsesAccountData ? Player.PlayerData.Data?.Gold ?? 0 : _progress.money;
        private void HandleBalance(PlayerData data) => OnChanged?.Invoke();
        private void HandleAccountReady()
        {
            _owner = BackEnd.Backend.UserInDate;
            _progress = _store.Load();
            OnChanged?.Invoke();
        }
        private void HandleAccountCleared()
        {
            _owner = null;
            _progress = new DeckUpgradeProgress { money = 0 };
            OnChanged?.Invoke();
        }
        public void Dispose()
        {
            if (!UsesAccountData) return;
            Player.PlayerData.OnUpdateUI -= HandleBalance;
            Player.OnAccountReady -= HandleAccountReady;
            Player.OnAccountCleared -= HandleAccountCleared;
        }
        public bool IsOwned(string id) => _store is not AccountDeckUpgradeStore account || account.IsOwned(id);
        public bool UsesAccountData => _store is AccountDeckUpgradeStore;

        public async Cysharp.Threading.Tasks.UniTask<bool> GrantTestTokensAsync()
        {
            if (IsBusy || (UsesAccountData && (_owner != BackEnd.Backend.UserInDate || !Player.PlayerData.IsReady))) return false;
            IsBusy = true; OnChanged?.Invoke();
            try
            {
                if (!UsesAccountData)
                {
                    _progress.money = checked(_progress.money + 10);
                    _store.Save(_progress); return true;
                }
                return await Player.PlayerData.GrantRewardAsync(UnityEngine.Resources.Load<RewardCatalog>("RewardCatalog"),
                    "test-upgrade-token:" + Guid.NewGuid().ToString("N"), new[] { new RewardAmount { RewardId = 10004, Amount = 10 } });
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("[TestUpgradeToken] " + error.Message); return false; }
            finally { IsBusy = false; OnChanged?.Invoke(); }
        }

        /// <summary>Test ownership changes preserve levels and commit both ownership records together.</summary>
        public async Cysharp.Threading.Tasks.UniTask<bool> SetTestCharacterOwnershipAsync(string[] ids, bool owned)
        {
            if (!UsesAccountData || IsBusy || _owner != BackEnd.Backend.UserInDate || !Player.PlayerData.IsReady || !Player.Characters.IsReady) return false;
            IsBusy = true; OnChanged?.Invoke();
            var document = Player.Characters;
            int generation = document.Generation;
            bool locked = false;
            try
            {
                using var timeout = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(20));
                var row = await document.ReadCleanRowAsync(timeout.Token);
                if (_owner != BackEnd.Backend.UserInDate || generation != document.Generation) return false;
                var next = Newtonsoft.Json.JsonConvert.DeserializeObject<AccountCharacterData>(Newtonsoft.Json.JsonConvert.SerializeObject(document.Data));
                if (owned)
                {
                    foreach (string id in ids)
                    {
                        var entry = next.Characters.Find(value => value.Id == id);
                        if (entry == null) { entry = new AccountCharacterData.Entry { Id = id }; next.Characters.Add(entry); }
                        entry.Count = 1;
                    }
                }
                else foreach (var entry in next.Characters) entry.Count = 0;
                next.LegacyOwnershipImported = true;
                next.SaveId = Guid.NewGuid().ToString("N");
                document.Lock(); locked = true;
                int[] unitIds = System.Array.ConvertAll(System.Array.FindAll(ids, id => id.StartsWith("unit:", StringComparison.Ordinal)), id => int.Parse(id.Substring(5), System.Globalization.CultureInfo.InvariantCulture));
                bool saved = await Player.PlayerData.TryCommitTestCharacterOwnershipAsync(row, Newtonsoft.Json.JsonConvert.SerializeObject(next), unitIds, owned);
                if (!saved || generation != document.Generation) return false;
                document.ApplyCommitted(next);
                return true;
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("[TestCharacterOwnership] " + error.Message); return false; }
            finally
            {
                if (locked && generation == document.Generation) document.Unlock();
                if (document.IsReady && _owner == BackEnd.Backend.UserInDate) _progress = _store.Load();
                IsBusy = false; OnChanged?.Invoke();
            }
        }

        /// <summary>Current preview-wide chief multiplier bonus.</summary>
        public float ChiefBonus => _progress.ChiefBonus;
        public float GetChiefBonus(string id) => (_progress.Level(id) - 1) * DeckUpgradeProgress.ChiefBonusPerLevel;

        /// <summary>Gets the preview level of a stable existing save ID.</summary>
        public int GetLevel(string id) => _progress.Level(id);

        /// <summary>Gets the preview cost of the next level.</summary>
        public int GetCost(string id) => UsesAccountData ? UnitBalanceRuntime.UpgradeCost(id, GetLevel(id) + 1).Amount : _progress.Cost(id);
        public string GetCurrency(string id) => UsesAccountData ? UnitBalanceRuntime.UpgradeCost(id, GetLevel(id) + 1).ItemId : "Gold";
        public int GetBalance(string id) => UsesAccountData ? DeckUpgradeCurrency.Balance(Player.PlayerData.Data, GetCurrency(id)) : Gold;

        /// <summary>Gets the preview's level multiplier.</summary>
        public float GetMultiplier(string id) => _progress.Multiplier(id);

        /// <summary>Changes and saves progress only when the upgrade succeeds.</summary>
        public async Cysharp.Threading.Tasks.UniTask<bool> TryUpgradeAsync(string id)
        {
            if (!UsesAccountData) return TryUpgrade(id);
            if (IsBusy || _owner != BackEnd.Backend.UserInDate) return false;
            IsBusy = true; OnChanged?.Invoke();
            try
            {
                bool saved = await ((AccountDeckUpgradeStore)_store).TryUpgradeAsync(id);
                if (saved) _progress = _store.Load();
                return saved;
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("[CharacterUpgrade] " + error.Message); return false; }
            finally { IsBusy = false; OnChanged?.Invoke(); }
        }

        public bool TryUpgrade(string id)
        {
            if (UsesAccountData) return false;
            if (string.IsNullOrEmpty(id) || !_progress.TryUpgrade(id)) return false;
            _store.Save(_progress);
            OnChanged?.Invoke();
            return true;
        }
    }
}
