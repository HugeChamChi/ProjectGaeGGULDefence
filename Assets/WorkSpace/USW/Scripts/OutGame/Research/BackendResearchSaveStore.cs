using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

[Serializable]
public sealed class AccountResearchData : AccountDocumentData
{
    public List<ResearchSaveData> Trees = new();
    public static void Validate(AccountResearchData data)
    {
        if (data.SchemaVersion != 1 || data.Trees == null) throw new InvalidDataException("Invalid research data.");
        var keys = new HashSet<string>();
        foreach (var tree in data.Trees)
        {
            if (tree == null || string.IsNullOrEmpty(tree.TreeKey) || !keys.Add(tree.TreeKey)) throw new InvalidDataException("Invalid research tree.");
            tree.GetLevels(tree.TreeKey);
        }
    }
}

/// <summary>All account research trees share a dedicated private server row.</summary>
public sealed class BackendResearchSaveStore : IResearchSaveStore, IDisposable
{
    private readonly PlayerPrefsResearchSaveStore _local = new();
    private readonly AccountDocument<AccountResearchData> _document = new("PlayerResearchData", AccountResearchData.Validate);
    private UniTaskCompletionSource _load;
    private int _generation;
    public BackendResearchSaveStore() { Player.OnAccountCleared += Clear; }
    private void Clear() { _generation++; _load = null; _document.Clear(); }
    private async UniTask EnsureLoaded(ResearchSaveAddress address, CancellationToken token)
    {
        AccountJsonStore.RequireOwner(address.AccountKey, token);
        if (_document.IsReady) return;
        var operation = _load;
        if (operation == null)
        {
            operation = _load = new UniTaskCompletionSource();
            LoadCore(operation, _generation).Forget();
        }
        await operation.Task.AttachExternalCancellation(token);
        AccountJsonStore.RequireOwner(address.AccountKey, token);
    }
    private async UniTask LoadCore(UniTaskCompletionSource completion, int generation)
    {
        try { await _document.InitializeAsync(); completion.TrySetResult(); }
        catch (Exception error) { completion.TrySetException(error); }
        finally { if (generation == _generation) _load = null; }
    }
    public async UniTask<ResearchSaveData> LoadAsync(ResearchSaveAddress address, CancellationToken token)
    {
        if (address.AccountKey == ResearchSaveAddress.LocalAccount) return await _local.LoadAsync(address, token);
        await EnsureLoaded(address, token);
        var tree = _document.Data.Trees.Find(x => x.TreeKey == address.TreeKey);
        return tree == null ? new ResearchSaveData { TreeKey = address.TreeKey }
            : JsonConvert.DeserializeObject<ResearchSaveData>(JsonConvert.SerializeObject(tree));
    }
    public async UniTask SaveAsync(ResearchSaveAddress address, ResearchSaveData data, CancellationToken token)
    {
        if (address.AccountKey == ResearchSaveAddress.LocalAccount) { await _local.SaveAsync(address, data, token); return; }
        data.GetLevels(address.TreeKey);
        await EnsureLoaded(address, token);
        var trees = _document.Data.Trees;
        int index = trees.FindIndex(x => x.TreeKey == address.TreeKey);
        var copy = JsonConvert.DeserializeObject<ResearchSaveData>(JsonConvert.SerializeObject(data));
        if (index < 0) trees.Add(copy); else trees[index] = copy;
        _document.Changed(); await _document.SaveAsync();
        AccountJsonStore.RequireOwner(address.AccountKey, token);
        if (_document.IsDirty) throw new IOException("Research save is pending. Retry before switching accounts.");
    }
    public void Dispose() { Player.OnAccountCleared -= Clear; Clear(); }
}
