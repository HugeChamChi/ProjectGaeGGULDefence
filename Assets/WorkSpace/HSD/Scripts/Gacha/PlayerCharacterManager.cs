using System;
using System.Globalization;
using BackEnd;
using Cysharp.Threading.Tasks;

/// <summary>Compatibility facade for gacha ownership, backed by common character records.</summary>
public class PlayerCharacterManager : Global.IClearable
{
    public bool IsDirty { get => Player.Characters.IsDirty || Player.PlayerData.IsDirty; set => Player.Characters.IsDirty = value; }
    public static string UnitId(int id) => "unit:" + id.ToString(CultureInfo.InvariantCulture);
    public static string ChiefId(int id) => "chief:" + id.ToString(CultureInfo.InvariantCulture);
    public int GetCount(int id)
    {
        int count = Player.Characters.Data?.Characters.Find(x => x.Id == UnitId(id))?.Count ?? 0;
        int rewards = Player.PlayerData.IsReady ? Player.PlayerData.Data.RewardInventory.Count(RewardType.Unit, id) : 0;
        return count > 0 || rewards > 0 ? 1 : 0;
    }
    public int GetChiefCount(int id) => (Player.Characters.Data?.Characters.Find(x => x.Id == ChiefId(id))?.Count ?? 0) > 0 ? 1 : 0;
    public void AddChief(int id, int count = 1) => AddRecord(ChiefId(id), count);
    public void AddCharacter(int id, int count = 1) { if (Player.Characters.IsReady) Player.PlayerData.AddOwnedCharacter(id, count); }
    private void AddRecord(string id, int count)
    {
        if (!Player.Characters.IsReady || count <= 0) return;
        var entries = Player.Characters.Data.Characters;
        var entry = entries.Find(x => x.Id == id);
        if (entry != null && entry.Count == 1) return;
        if (entry == null) { entry = new AccountCharacterData.Entry { Id = id }; entries.Add(entry); }
        entry.Count = 1; Player.Characters.Changed();
    }
    public void AddCharacters(int[] ids) { foreach (int id in ids) AddCharacter(id); }
    public void AddCharacter(ICharacterData character) => AddCharacter(character.Id);
    public void AddCharacters(ICharacterData[] characters) { foreach (var character in characters) AddCharacter(character); }
    public async UniTask SaveAsync()
    {
        await Player.Characters.SaveAsync();
        await Player.PlayerData.SaveAsync();
    }
    public async UniTask InitalizeAsync()
    {
        if (!Player.Characters.IsReady) throw new InvalidOperationException("Character data is not ready.");
        bool normalized = false;
        foreach (var entry in Player.Characters.Data.Characters)
            if (entry.Count > 1) { entry.Count = 1; normalized = true; }
        if (normalized) { Player.Characters.Changed(); await Player.Characters.SaveAsync(); }
        if (Player.Characters.Data.LegacyOwnershipImported) return;
        string owner = Backend.UserInDate;
        var done = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.GameData.GetMyData("PlayerOwnedCharacterData", new Where(), result => done.TrySetResult(result));
        var response = await done.Task;
        AccountJsonStore.RequireOwner(owner, default);
        if (!response.IsSuccess()) throw new InvalidOperationException("Legacy ownership read failed: " + response.GetStatusCode());
        var rows = response.FlattenRows();
        // Ownership is a set: union duplicate rows without summing quantities.
        for (int row = 0; row < rows.Count; row++)
        {
            if (!rows[row].ContainsKey("OwnedCharacterCounts") || rows[row]["OwnedCharacterCounts"] == null)
                throw new InvalidOperationException("Legacy inventory is missing ownership.");
            var counts = rows[row]["OwnedCharacterCounts"];
            foreach (string key in counts.Keys)
            {
                int id = int.Parse(key, CultureInfo.InvariantCulture), count = int.Parse(counts[key].ToString(), CultureInfo.InvariantCulture);
                if (count < 0) throw new InvalidOperationException("Invalid legacy ownership.");
                if (count == 0) continue;
                var entry = Player.Characters.Data.Characters.Find(x => x.Id == UnitId(id));
                if (entry == null) Player.Characters.Data.Characters.Add(new AccountCharacterData.Entry { Id = UnitId(id), Count = 1 });
                else entry.Count = 1;
            }
        }
        foreach (var entry in Player.Characters.Data.Characters) entry.Count = entry.Count > 0 ? 1 : 0;
        Player.Characters.Data.LegacyOwnershipImported = true;
        Player.Characters.Changed(); await SaveAsync();
        if (Player.Characters.IsDirty) throw new InvalidOperationException("Character migration save is pending.");
    }
    public void Load(Action onCompleted = null) => LoadCore(onCompleted).Forget();
    private async UniTaskVoid LoadCore(Action done) { try { await InitalizeAsync(); } finally { done?.Invoke(); } }
    public void Clear() { }
}
