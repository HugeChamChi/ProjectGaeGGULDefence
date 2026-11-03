using System;
using System.Globalization;
using BackEnd;
using Cysharp.Threading.Tasks;

/// <summary>Compatibility facade for gacha ownership, backed by common character records.</summary>
public class PlayerCharacterManager : Global.IClearable
{
    public bool IsDirty { get => Player.Characters.IsDirty; set => Player.Characters.IsDirty = value; }
    public static string UnitId(int id) => "unit:" + id.ToString(CultureInfo.InvariantCulture);
    public static string ChiefId(int id) => "chief:" + id.ToString(CultureInfo.InvariantCulture);
    public int GetCount(int id) => Player.Characters.Data?.Characters.Find(x => x.Id == UnitId(id))?.Count ?? 0;
    public int GetChiefCount(int id) => Player.Characters.Data?.Characters.Find(x => x.Id == ChiefId(id))?.Count ?? 0;
    public void AddChief(int id, int count = 1) => AddRecord(ChiefId(id), count);
    public void AddCharacter(int id, int count = 1) => AddRecord(UnitId(id), count);
    private void AddRecord(string id, int count)
    {
        if (!Player.Characters.IsReady || count <= 0) return;
        var entries = Player.Characters.Data.Characters;
        var entry = entries.Find(x => x.Id == id);
        if (entry == null) { entry = new AccountCharacterData.Entry { Id = id }; entries.Add(entry); }
        entry.Count = checked(entry.Count + count); Player.Characters.Changed();
    }
    public void AddCharacters(int[] ids) { foreach (int id in ids) AddCharacter(id); }
    public void AddCharacter(ICharacterData character) => AddCharacter(character.Id);
    public void AddCharacters(ICharacterData[] characters) { foreach (var character in characters) AddCharacter(character); }
    public UniTask SaveAsync() => Player.Characters.SaveAsync();
    public async UniTask InitalizeAsync()
    {
        if (!Player.Characters.IsReady) throw new InvalidOperationException("Character data is not ready.");
        if (Player.Characters.Data.LegacyOwnershipImported) return;
        string owner = Backend.UserInDate;
        var done = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.GameData.GetMyData("PlayerOwnedCharacterData", new Where(), result => done.TrySetResult(result));
        var response = await done.Task;
        AccountJsonStore.RequireOwner(owner, default);
        if (!response.IsSuccess()) throw new InvalidOperationException("Legacy ownership read failed: " + response.GetStatusCode());
        var rows = response.FlattenRows();
        if (rows.Count > 1) throw new InvalidOperationException("Duplicate legacy ownership rows.");
        if (rows.Count == 1 && rows[0].ContainsKey("OwnedCharacterCounts"))
        {
            var counts = rows[0]["OwnedCharacterCounts"];
            foreach (string key in counts.Keys)
            {
                int id = int.Parse(key, CultureInfo.InvariantCulture), count = (int)counts[key];
                if (count < 0) throw new InvalidOperationException("Invalid legacy ownership.");
                var entry = Player.Characters.Data.Characters.Find(x => x.Id == UnitId(id));
                if (entry == null) Player.Characters.Data.Characters.Add(new AccountCharacterData.Entry { Id = UnitId(id), Count = count });
                else entry.Count = Math.Max(entry.Count, count);
            }
        }
        Player.Characters.Data.LegacyOwnershipImported = true;
        Player.Characters.Changed(); await SaveAsync();
        if (IsDirty) throw new InvalidOperationException("Character migration save is pending.");
    }
    public void Load(Action onCompleted = null) => LoadCore(onCompleted).Forget();
    private async UniTaskVoid LoadCore(Action done) { try { await InitalizeAsync(); } finally { done?.Invoke(); } }
    public void Clear() { }
}
