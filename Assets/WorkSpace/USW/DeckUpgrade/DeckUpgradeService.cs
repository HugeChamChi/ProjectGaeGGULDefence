using System;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Owns preview progress and upgrade commands independently of the UI.</summary>
    public sealed class DeckUpgradeService
    {
        private readonly IDeckUpgradeStore _store;
        private readonly DeckUpgradeProgress _progress;
        private readonly string _owner;

        /// <summary>Loads one progress instance for this scope.</summary>
        public DeckUpgradeService(IDeckUpgradeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _owner = BackEnd.Backend.UserInDate;
            _progress = store.Load() ?? new DeckUpgradeProgress();
        }

        /// <summary>Raised after a successful upgrade has been saved.</summary>
        public event Action OnChanged;

        /// <summary>Current preview balance; this is not the account's gold.</summary>
        public int Gold => _progress.money;
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
        public bool TryUpgrade(string id)
        {
            if (_store is AccountDeckUpgradeStore && (_owner != BackEnd.Backend.UserInDate || !Player.Characters.IsReady)) return false;
            if (string.IsNullOrEmpty(id) || !_progress.TryUpgrade(id)) return false;
            _store.Save(_progress);
            OnChanged?.Invoke();
            return true;
        }
    }
}
