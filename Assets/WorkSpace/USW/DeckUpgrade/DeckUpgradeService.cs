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

        /// <summary>Current preview-wide chief multiplier bonus.</summary>
        public float ChiefBonus => _progress.ChiefBonus;
        public float GetChiefBonus(string id) => (_progress.Level(id) - 1) * DeckUpgradeProgress.ChiefBonusPerLevel;

        /// <summary>Gets the preview level of a stable existing save ID.</summary>
        public int GetLevel(string id) => _progress.Level(id);

        /// <summary>Gets the preview cost of the next level.</summary>
        public int GetCost(string id) => _progress.Cost(id);

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
