using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Authenticated account snapshots; unowned local development saves are never imported automatically.</summary>
public sealed class AccountResearchSaveStore : IResearchSaveStore
{
    private static void Require(ResearchSaveAddress address)
    {
        if (!Player.PlayerData.IsReady || address.AccountKey != BackEnd.Backend.UserInDate)
            throw new InvalidOperationException("Upgrade save requires the loaded account.");
    }
    public async UniTask<ResearchSaveData> LoadAsync(ResearchSaveAddress address, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Require(address);
        var progress = Player.PlayerData.Data.UpgradeProgress;
        progress.Validate();
        var existing = progress.Trees.Find(x => x.TreeKey == address.TreeKey);
        if (existing != null) return JsonUtility.FromJson<ResearchSaveData>(JsonUtility.ToJson(existing));
        if (PlayerPrefs.HasKey(address.StorageKey))
        {
            var local = await new PlayerPrefsResearchSaveStore().LoadAsync(address, token);
            Require(address);
            Player.PlayerData.SetUpgradeProgress(local, "account-local-v1");
            if (!await Player.PlayerData.TrySaveAsync()) throw new InvalidOperationException("Upgrade import awaiting server confirmation.");
            return local;
        }
        return new ResearchSaveData { TreeKey = address.TreeKey };
    }
    public void Checkpoint(ResearchSaveAddress address, ResearchSaveData data)
    { Require(address); Player.PlayerData.SetUpgradeProgress(data); }
    public async UniTask SaveAsync(ResearchSaveAddress address, ResearchSaveData data, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Checkpoint(address, data);
        if (!await Player.PlayerData.TrySaveAsync().AttachExternalCancellation(token))
            throw new InvalidOperationException("Upgrade save awaiting server confirmation.");
        Require(address);
    }
}
