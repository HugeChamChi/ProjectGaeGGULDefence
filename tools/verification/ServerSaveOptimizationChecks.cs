using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class ServerSaveOptimizationChecks
{
    private static int passed;
    private static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); passed++; }
    private static FieldInfo Field(string name) => typeof(Player).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
    private static PlayerData Clone(PlayerData data) => JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));

    private sealed class Backend : BackendGameData
    {
        public PlayerData Server = new() { Gold = 100, Stamina = 30, MaxStamina = 30, SaveId = "base" };
        public int Writes, Reads;
        public bool Auto = true;
        public readonly Queue<(PlayerData data, Action<bool> done)> Pending = new();
        public override void GameDataGet(Action<PlayerData> callback = null) { Reads++; callback?.Invoke(Clone(Server)); }
        public override void GameDataUpdate(PlayerData data, Action completed = null, Action<bool> onResult = null)
        {
            Writes++;
            if (Auto) { Server = Clone(data); onResult?.Invoke(true); completed?.Invoke(); }
            else Pending.Enqueue((Clone(data), saved => { onResult?.Invoke(saved); completed?.Invoke(); }));
        }
        public void Complete(bool saved)
        { var pending = Pending.Dequeue(); if (saved) Server = pending.data; pending.done(saved); }
    }

    private sealed class Records : IRoundRecordStore
    {
        public int Loads, Merges;
        public bool Fail;
        public UniTask<Dictionary<string, int>> LoadAsync(string owner, CancellationToken token)
        { Loads++; return UniTask.FromResult(new Dictionary<string, int> { ["NORMAL"] = 2 }); }
        public UniTask<int> MergeAsync(string owner, string mode, int round, CancellationToken token)
        { Merges++; if (Fail) throw new Exception("isolated failure"); return UniTask.FromResult(round); }
    }

    public static async Task<string> Run()
    {
        if (Application.isPlaying) throw new Exception("Run only in stopped Editor.");
        // Never flush the user's real account during an isolated check.
        Global.IClearable[] others = { Player.Mail, Player.Shop, Player.Daily, Player.RoundRecords,
            Player.DailyData, Player.WeeklyData, Player.Characters, Player.LobbyProgress };
        foreach (var manager in others) if (manager.IsDirty) throw new Exception("Real account has pending changes; check aborted.");
        if (Field("_debounceCts").GetValue(null) != null || Field("_flush").GetValue(null) != null || Player.IsBattleActive)
            throw new Exception("Save activity exists; check aborted.");
        passed = 0;
        var original = Player.PlayerData;
        var prefix = "SaveOptimizationCheck." + Guid.NewGuid().ToString("N") + ".";
        var backend = new Backend();
        var controller = new PlayerDataController(() => "isolated", () => 1000, prefix);
        controller.Inject(backend);
        var records = new Records();
        var recordManager = new RoundRecordManager(records, () => "isolated", prefix);
        try
        {
            Field("<PlayerData>k__BackingField").SetValue(null, controller);
            await controller.InitalizeAsync();
            await Player.UpdateDirtyDataAsync();
            Check(backend.Writes == 0, "Clean flush sends no writes");

            controller.AddGold(1);
            object timer = Field("_debounceCts").GetValue(null);
            controller.AddGold(2);
            Check(ReferenceEquals(timer, Field("_debounceCts").GetValue(null)) && backend.Writes == 0,
                "Repeated changes share one pending timer and do not write immediately");
            backend.Auto = false;
            var first = Player.UpdateDirtyDataAsync().AsTask();
            var second = Player.UpdateDirtyDataAsync().AsTask();
            Check(backend.Writes == 1 && controller.IsDirty, "Overlapping flushes dispatch once and retain dirty until ACK");
            controller.AddGold(4);
            backend.Auto = true;
            backend.Complete(true);
            await Task.WhenAll(first, second);
            Check(backend.Writes == 2 && backend.Server.Gold == 107 && !controller.IsDirty,
                "An edit during save gets a follow-up write and clears only after latest ACK");
            await Player.UpdateDirtyDataAsync();
            Check(backend.Writes == 2 && Field("_debounceCts").GetValue(null) == null,
                "Explicit flush cancels redundant scheduled work");

            Player.BeginBattle();
            controller.AddGold(1);
            Check(backend.Writes == 2 && Field("_debounceCts").GetValue(null) == null,
                "Battle changes remain dirty without a periodic timer");
            Player.EndBattle();
            Check(!Player.IsBattleActive && backend.Writes == 3 && !controller.IsDirty,
                "Battle end flushes accumulated account changes once");
            Player.EndBattle();
            Check(backend.Writes == 3, "Scene destruction after settlement adds no write");

            controller.AddGold(1);
            Player.SetApplicationPaused(true);
            Player.SetApplicationPaused(true);
            Check(backend.Writes == 4 && !controller.IsDirty, "Duplicate pause callbacks share one flush");
            int reads = backend.Reads;
            Player.SetApplicationPaused(false);
            Check(backend.Reads == reads, "Short app switch does not re-read account data");
            Player.SetApplicationPaused(true);
            Field("_pausedAt").SetValue(null, Time.realtimeSinceStartupAsDouble - 61);
            Player.SetApplicationPaused(false);
            Check(backend.Reads == reads + 1, "Long background interval still refreshes server state");

            int scheduledWrites = backend.Writes;
            controller.AddGold(1);
            Field("_firstPendingSave").SetValue(null, Time.realtimeSinceStartupAsDouble - 31);
            Field("_lastPendingSave").SetValue(null, Time.realtimeSinceStartupAsDouble - 31);
            typeof(Player).GetMethod("DebouncedSaveTask", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { CancellationToken.None });
            Check(backend.Writes == scheduledWrites + 1 && !controller.IsDirty, "Quiet period reaching 30 seconds flushes batch");
            controller.AddGold(1);
            Field("_firstPendingSave").SetValue(null, Time.realtimeSinceStartupAsDouble - 61);
            Field("_lastPendingSave").SetValue(null, Time.realtimeSinceStartupAsDouble);
            typeof(Player).GetMethod("DebouncedSaveTask", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { CancellationToken.None });
            Check(backend.Writes == scheduledWrites + 2 && !controller.IsDirty, "Continuous changes flush at 60-second batch limit");

            controller.AddGold(1);
            backend.Auto = false;
            var failed = Player.UpdateDirtyDataAsync().AsTask();
            backend.Complete(false);
            await failed;
            Check(controller.IsDirty, "Failed server response keeps dirty state");
            int writes = backend.Writes;
            for (int i = 0; i < 5; i++) await Player.UpdateDirtyDataAsync();
            Check(backend.Writes == writes, "Retry backoff prevents repeated failed requests");

            await recordManager.InitializeAsync();
            await recordManager.SaveAsync();
            Check(records.Loads == 1 && records.Merges == 0, "Clean record save performs no read or write");
            recordManager.Record("isolated", "NORMAL", 5);
            await recordManager.SaveAsync();
            Check(records.Loads == 1 && records.Merges == 1 && !recordManager.IsDirty,
                "Dirty record save merges without a redundant initial read");
            recordManager.Record("isolated", "NORMAL", 6);
            records.Fail = true;
            await recordManager.SaveAsync();
            Check(recordManager.IsDirty && recordManager.GetBest("NORMAL") == 6, "Failed record merge retains pending maximum");
            records.Fail = false;
            await recordManager.SaveAsync();
            Check(!recordManager.IsDirty && records.Loads == 1, "Record retry confirms pending maximum without extra load");
            return "PASS " + passed + ": isolated backend only; batching, overlapping saves, latest ACK, battle end, pause/resume, failed write backoff, record request counts. No live server writes.";
        }
        finally
        {
            typeof(Player).GetMethod("CancelDebouncedSave", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Field("<IsBattleActive>k__BackingField").SetValue(null, false);
            Field("_paused").SetValue(null, false);
            controller.Clear(); recordManager.Clear();
            Field("<PlayerData>k__BackingField").SetValue(null, original);
            foreach (string suffix in new[] { "isolated", "isolated.NORMAL.best", "isolated.NORMAL.pending", "isolated.HARD.best", "isolated.HARD.pending" })
                PlayerPrefs.DeleteKey(prefix + suffix);
            PlayerPrefs.Save();
        }
    }
}
