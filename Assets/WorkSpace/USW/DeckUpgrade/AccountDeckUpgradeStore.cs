using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Account growth uses real gold and a conditional two-table transaction.</summary>
    public sealed class AccountDeckUpgradeStore : IDeckUpgradeStore
    {
        private int _generation;
        public DeckUpgradeProgress Load()
        {
            if (!Player.Characters.IsReady) throw new InvalidOperationException("Character data is not loaded.");
            _generation = Player.Characters.Generation;
            return new DeckUpgradeProgress { money = Player.PlayerData.Data?.Gold ?? 0,
                units = Player.Characters.Data.Characters.Select(x => new DeckUpgradeProgress.Entry { id = x.Id, level = x.Level }).ToList() };
        }
        public bool IsOwned(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var parts = id.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int key)) return false;
            return parts[0] == "unit" ? Player.Character.GetCount(key) > 0 : parts[0] == "chief" && Player.Character.GetChiefCount(key) > 0;
        }
        public void Save(DeckUpgradeProgress progress) => throw new InvalidOperationException("Account upgrades require the gold transaction.");
        public async UniTask<bool> TryUpgradeAsync(string id)
        {
            var document = Player.Characters;
            if (!document.IsReady || _generation != document.Generation || !Player.PlayerData.IsReady || !IsOwned(id)) return false;
            if (!await Player.PlayerData.TrySaveAsync()) return false;
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(20), DelayType.Realtime);
            var row = await document.ReadCleanRowAsync(timeout.Token);
            if (_generation != document.Generation) return false;
            var next = JsonConvert.DeserializeObject<AccountCharacterData>(JsonConvert.SerializeObject(document.Data));
            var entry = next.Characters.Find(x => x.Id == id);
            if (entry == null) { entry = new AccountCharacterData.Entry { Id = id, Count = 1 }; next.Characters.Add(entry); }
            if (entry.Level >= DeckUpgradeProgress.MaxLevel) return false;
            int cost = checked(150 * entry.Level);
            if (Player.PlayerData.Data.Gold < cost) return false;
            entry.Count = 1; entry.Level++; next.SaveId = Guid.NewGuid().ToString("N");
            AccountCharacterData.Validate(next);
            document.Lock();
            try
            {
                bool saved = await Player.PlayerData.TryCommitCharacterUpgradeAsync(cost, row, JsonConvert.SerializeObject(next));
                if (!saved || _generation != document.Generation) return false;
                document.ApplyCommitted(next); return true;
            }
            finally { if (_generation == document.Generation) document.Unlock(); }
        }
    }
}
