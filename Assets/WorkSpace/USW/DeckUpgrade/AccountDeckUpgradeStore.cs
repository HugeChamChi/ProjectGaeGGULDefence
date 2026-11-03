using System;
using System.Linq;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Lobby growth uses the same account records as ownership for units and chiefs.</summary>
    public sealed class AccountDeckUpgradeStore : IDeckUpgradeStore
    {
        private int _generation;
        public DeckUpgradeProgress Load()
        {
            if (!Player.Characters.IsReady) throw new InvalidOperationException("Character data is not loaded.");
            var data = Player.Characters.Data;
            _generation = Player.Characters.Generation;
            return new DeckUpgradeProgress { money = data.UpgradeBalance,
                units = data.Characters.Select(x => new DeckUpgradeProgress.Entry { id = x.Id, level = x.Level }).ToList() };
        }
        public void Save(DeckUpgradeProgress progress)
        {
            if (!Player.Characters.IsReady || _generation != Player.Characters.Generation)
                throw new InvalidOperationException("Character account changed.");
            var data = Player.Characters.Data;
            foreach (var saved in progress.units)
            {
                var entry = data.Characters.Find(x => x.Id == saved.id);
                if (entry == null) { entry = new AccountCharacterData.Entry { Id = saved.id }; data.Characters.Add(entry); }
                entry.Level = saved.level;
            }
            data.UpgradeBalance = progress.money;
            Player.Characters.Changed();
        }
    }
}
