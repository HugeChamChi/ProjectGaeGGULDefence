using System;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public static class UpgradeServerSaveChecks
{
    private sealed class Backend : BackendGameData
    {
        public PlayerData Stored = new PlayerData { SaveId = "base", Gold = 100, Diamond = 50, Stamina = 30 };
        public int Writes;
        public bool Success = true;
        public override void GameDataGet(Action<PlayerData> done = null) => done?.Invoke(JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(Stored)));
        public override void GameDataUpdate(PlayerData data, Action done = null, Action<bool> onResult = null)
        {
            Writes++; if (Success) Stored = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
            done?.Invoke(); onResult?.Invoke(Success);
        }
    }
    public static async Task<string> Run()
    {
        int passed = 0;
        void Check(bool value) { if (!value) throw new Exception("Upgrade check " + (passed + 1)); passed++; }
        string owner = "upgrade-A", prefix = "UpgradeTest." + Guid.NewGuid().ToString("N");
        var backend = new Backend(); var controller = new PlayerDataController(() => owner, () => 1000, prefix);
        controller.Inject(backend);
        try
        {
            await controller.InitalizeAsync();
            Check(controller.Data.UpgradeProgress.Trees.Count == 0 && backend.Writes == 0);
            var tree = new ResearchSaveData { TreeKey = "test-tree" };
            tree.Entries.Add(new ResearchSaveData.Entry { Id = "test-node", Level = 2 });
            controller.SetUpgradeProgress(tree);
            Check(controller.IsDirty && PlayerPrefs.HasKey(prefix + owner) && backend.Writes == 0);
            tree.Entries[0].Level = 99;
            Check(controller.Data.UpgradeProgress.Trees[0].Entries[0].Level == 2);
            controller.Clear(); await controller.InitalizeAsync();
            Check(controller.Data.UpgradeProgress.Trees[0].Entries[0].Level == 2 && !controller.IsDirty);
            Check(backend.Stored.Gold == 100 && backend.Stored.Diamond == 50);
            int writes = backend.Writes;
            controller.SetUpgradeProgress(controller.Data.UpgradeProgress.Trees[0]); await controller.TrySaveAsync();
            Check(backend.Writes == writes);
            backend.Success = false;
            tree.Entries[0].Level = 3; controller.SetUpgradeProgress(tree);
            Check(!await controller.TrySaveAsync() && controller.IsDirty);
            controller.Clear(); backend.Success = true; await controller.InitalizeAsync();
            Check(backend.Stored.UpgradeProgress.Trees[0].Entries[0].Level == 3);
            controller.SetUpgradeProgress(new ResearchSaveData { TreeKey = "second-tree" });
            Check(controller.Data.UpgradeProgress.Trees.Count == 2);
            controller.Clear(); owner = "upgrade-B"; await controller.InitalizeAsync();
            Check(controller.Data.UpgradeProgress.Trees.Count == 1);
            var parse = typeof(BackendGameData).GetMethod("ParseUpgradeProgress", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (string json in new[] { "", "null", "{}", "{\"SchemaVersion\":2,\"Trees\":[]}", "{\"SchemaVersion\":1,\"Trees\":[{\"TreeKey\":\"x\",\"Entries\":[{\"Id\":\"n\",\"Level\":-1}]}]}" })
            {
                bool rejected = false; try { parse.Invoke(null, new object[] { json }); } catch { rejected = true; } Check(rejected);
            }
            var value = (UpgradeProgressData)parse.Invoke(null, new object[] { "{\"SchemaVersion\":1,\"Trees\":[],\"LocalImportId\":\"\"}" });
            Check(value.Trees.Count == 0);
            return "PASS " + passed + ": upgrade snapshot, detached copy, restart, retry, no-op call budget, free balance preservation, tree/account isolation and invalid server payload rejection. Isolated backend only.";
        }
        finally { controller.Clear(); PlayerPrefs.DeleteKey(prefix + "upgrade-A"); PlayerPrefs.DeleteKey(prefix + "upgrade-B"); PlayerPrefs.Save(); }
    }
    public static async Task<string> Live()
    {
        if (!BackEnd.Backend.IsLogin || !Player.PlayerData.IsReady) throw new Exception("Loaded login required.");
        var root = UnityEngine.Object.FindFirstObjectByType<RootLifetimeScope>();
        if (root == null || !(root.Container.Resolve<IResearchSaveStore>() is AccountResearchSaveStore)) throw new Exception("Production store not registered.");
        if (root.Container.Resolve<ResearchAccountContext>().AccountKey != BackEnd.Backend.UserInDate) throw new Exception("Account context not bound.");
        var before = JsonUtility.ToJson(Player.PlayerData.Data.UpgradeProgress);
        Player.PlayerData.MarkDirty();
        if (!await Player.PlayerData.TrySaveAsync()) throw new Exception("Save not acknowledged.");
        var done = new UniTaskCompletionSource<BackEnd.BackendReturnObject>();
        BackEnd.Backend.GameData.GetMyData("PlayerData", new BackEnd.Where(), response => done.TrySetResult(response));
        var result = await done.Task;
        if (!result.IsSuccess() || result.FlattenRows().Count != 1) throw new Exception("Read failed.");
        var row = result.FlattenRows()[0];
        var parse = typeof(BackendGameData).GetMethod("ParseUpgradeProgress", BindingFlags.NonPublic | BindingFlags.Static);
        var actual = (UpgradeProgressData)parse.Invoke(null, new object[] { row.GetString("UpgradeProgressJson", "") });
        if (JsonUtility.ToJson(actual) != before) throw new Exception("Upgrade snapshot mismatch.");
        string fixture = "__upgrade_verification_" + Guid.NewGuid().ToString("N");
        var store = new AccountResearchSaveStore();
        var address = new ResearchSaveAddress(BackEnd.Backend.UserInDate, fixture);
        try
        {
            var data = await store.LoadAsync(address, System.Threading.CancellationToken.None);
            data.Entries.Add(new ResearchSaveData.Entry { Id = "fixture-node", Level = 2 });
            store.Checkpoint(address, data);
            await store.SaveAsync(address, data, System.Threading.CancellationToken.None);
            var read = new UniTaskCompletionSource<PlayerData>();
            new BackendGameData().GameDataGet(value => read.TrySetResult(value));
            var remote = await read.Task;
            if (remote == null || remote.UpgradeProgress.Trees.Find(x => x.TreeKey == fixture)?.Entries[0].Level != 2)
                throw new Exception("Actual upgrade node did not roundtrip.");
        }
        finally
        {
            Player.PlayerData.Data.UpgradeProgress.Trees.RemoveAll(x => x.TreeKey == fixture);
            Player.PlayerData.MarkDirty();
            if (!await Player.PlayerData.TrySaveAsync()) throw new Exception("Fixture cleanup awaiting save.");
        }
        return "PASS: production DI/account binding, actual UpgradeProgressJson save/read, isolated fixture node level 2 roundtrip and cleanup. Existing upgrades preserved; no currency spending or gameplay-node grants.";
    }
}
