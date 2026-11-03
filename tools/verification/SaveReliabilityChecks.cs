using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class SaveReliabilityChecks
{
    private static int passed;
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; }
    private static PlayerData Clone(PlayerData data) => JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
    private sealed class Character : ICharacterData
    {
        public int Id { get; set; }
        public Rarity Rarity => default;
        public Sprite Icon => null;
        public string Name => "test";
        public string Description => "test";
    }
    private sealed class FakeBackend : BackendGameData
    {
        public PlayerData Server = new PlayerData { Gold = 100, Diamond = 100, Stamina = 30, MaxStamina = 30, SaveId = "base" };
        public bool Auto = true, Succeed = true;
        public int Writes;
        public readonly Queue<(PlayerData data, Action<bool> callback)> Pending = new();
        public override void GameDataGet(Action<PlayerData> callback = null) => callback?.Invoke(Clone(Server));
        public override void GameDataUpdate(PlayerData data, Action done = null, Action<bool> onResult = null)
        {
            Writes++;
            if (Auto) { if (Succeed) Server = Clone(data); onResult?.Invoke(Succeed); done?.Invoke(); }
            else Pending.Enqueue((Clone(data), saved => { onResult?.Invoke(saved); done?.Invoke(); }));
        }
        public void Complete(bool success) { var next = Pending.Dequeue(); if (success) Server = next.data; next.callback(success); }
    }
    public static async Task<string> Run()
    {
        passed = 0;
        var prefix = "SaveVerification." + Guid.NewGuid().ToString("N") + ".";
        string owner = "isolated-A";
        var backend = new FakeBackend();
        var controller = new PlayerDataController(() => owner, () => 1000, prefix);
        controller.Inject(backend);
        float previousScale = Time.timeScale;
        try
        {
            await controller.InitalizeAsync();
            Check(controller.IsReady && backend.Writes == 0, "Clean load must not write");
            Check(controller.Data.SelectedChiefId == -1, "Legacy sentinel survives local serialization");
            await controller.TrySaveAsync();
            Check(backend.Writes == 0, "Clean save must not write");
            controller.AddGold(10);
            Check(PlayerPrefs.HasKey(prefix + owner), "Mutation journal is synchronous");
            backend.Auto = false;
            var first = controller.TrySaveAsync().AsTask();
            controller.AddGold(5);
            var journal = JsonUtility.FromJson<PlayerDataJournal>(PlayerPrefs.GetString(prefix + owner));
            Check(journal.Data.Gold == 115 && journal.AttemptedSaveIds.Count == 1, "In-flight ancestry and newer balance persist");
            // Process dies after the server committed A but before its callback, with newer local B.
            backend.Server = Clone(backend.Pending.Peek().data);
            controller.Clear();
            Check(!await first, "Old session waiter is cancelled");
            backend.Auto = true;
            await controller.InitalizeAsync();
            Check(!controller.HasSaveConflict && controller.Data.Gold == 115 && backend.Server.Gold == 115, "Lost ACK restart recovers newer local edit");
            Check(!PlayerPrefs.HasKey(prefix + owner), "Only acknowledged latest state removes journal");
            backend.Pending.Dequeue().callback(true);
            Check(controller.Data.Gold == 115 && !controller.IsDirty, "Old callback cannot change new session");

            backend.Auto = false;
            controller.AddGold(1);
            int writes = backend.Writes;
            Time.timeScale = 0;
            bool timedOut = await controller.TrySaveAsync(); // 15 real seconds, no server calls.
            Check(!timedOut && controller.IsDirty, "Caller timeout keeps journal and dirty state at timescale zero");
            var second = controller.TrySaveAsync().AsTask();
            Check(backend.Writes == writes + 1 && backend.Pending.Count == 1, "Timeout does not release the SDK write lock");
            controller.AddGold(2);
            backend.Auto = true;
            backend.Complete(true);
            Check(await second && backend.Server.Gold == 118, "Late ACK drains latest revision without loss");
            Check(backend.Writes == writes + 2, "Exactly one follow-up for the newer revision");
            Time.timeScale = previousScale;

            backend.Succeed = false;
            controller.AddGold(1);
            Check(!await controller.TrySaveAsync(), "Failed save reports false");
            writes = backend.Writes;
            for (int i = 0; i < 10; i++) await controller.TrySaveAsync();
            Check(backend.Writes == writes && PlayerPrefs.HasKey(prefix + owner), "Backoff prevents repeated writes and retains data");
            controller.Clear(); backend.Succeed = true;
            await controller.InitalizeAsync();
            Check(backend.Server.Gold == 119, "Failed write recovers on restart exactly once");

            var purchase = new ShopItemData(1001, ItemType.Character, 101, 3, CurrencyType.Gold, 20);
            Check(controller.TryPurchaseShopItem(purchase, "2026-11-01"), "Atomic purchase accepted");
            journal = JsonUtility.FromJson<PlayerDataJournal>(PlayerPrefs.GetString(prefix + owner));
            Check(journal.Data.Gold == 99 && journal.Data.RewardInventory.Count(RewardType.Unit, 101) == 1 && journal.Data.ShopPurchases.Contains(1001), "Price, units, receipt in one journal");
            Check(controller.TryPurchaseShopItem(purchase, "2026-11-01") && controller.Data.Gold == 99 && controller.Data.RewardInventory.Count(RewardType.Unit, 101) == 1, "Duplicate purchase is idempotent");
            var unsupported = new ShopItemData(1002, ItemType.Item, 101, 1, CurrencyType.Gold, 20);
            Check(!controller.TryPurchaseShopItem(unsupported, "2026-11-01") && controller.Data.Gold == 99, "Unsupported reward never charges");
            controller.CompleteTutorial("save-test"); controller.SelectChief(7); controller.SelectProfile(2, 3);
            controller.Clear(); await controller.InitalizeAsync();
            Check(controller.Data.Gold == 99 && controller.Data.RewardInventory.Count(RewardType.Unit, 101) == 1, "Purchase survives restart before cloud save");
            Check(controller.Data.CompletedTutorials.Contains("save-test") && controller.Data.SelectedChiefId == 7 && controller.Data.SelectedIconId == 2, "Tutorial and selections survive restart");
            Check(controller.TryPurchaseCharacters(10, new ICharacterData[] { new Character { Id = 102 }, new Character { Id = 102 } }), "Gacha transaction accepted");
            Check(controller.Data.Diamond == 90 && controller.Data.RewardInventory.Count(RewardType.Unit, 102) == 1, "Gacha charge and all results in one snapshot");
            Check(!controller.TryPurchaseCharacters(10, new ICharacterData[] { new Character { Id = 103 }, null }) && controller.Data.Diamond == 90 && controller.Data.RewardInventory.Count(RewardType.Unit, 103) == 0, "Invalid gacha result rolls back entire purchase");
            await controller.TrySaveAsync();
            controller.AddGold(9); controller.Clear(); owner = "isolated-B";
            await controller.InitalizeAsync();
            Check(controller.Data.Gold == 99, "Journal does not leak to another account");
            controller.Clear(); owner = "isolated-A";
            backend.Server.SaveId = "another-device";
            await controller.InitalizeAsync();
            Check(controller.HasSaveConflict && !controller.IsReady && PlayerPrefs.HasKey(prefix + owner), "Unknown remote version blocks overwrite and retains recovery data");
            return "PASS " + passed + ": real controller, isolated backend; delayed ACK, 15s timeout, timescale zero, retry budget, restart, account isolation, atomic purchase and conflict retention. No live server writes.";
        }
        finally
        {
            controller.Clear(); Time.timeScale = previousScale;
            PlayerPrefs.DeleteKey(prefix + "isolated-A"); PlayerPrefs.DeleteKey(prefix + "isolated-B"); PlayerPrefs.Save();
        }
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Restore(object target) => target.GetType().GetMethod("RestorePending", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    public static string Formats()
    {
        passed = 0;
        var legacy = JsonUtility.FromJson<PlayerData>("{\"Gold\":7,\"SaveId\":\"legacy\"}");
        Check(legacy.SelectedChiefId == -1 && legacy.SelectedIconId == -1 && legacy.SelectedFrameId == -1, "Missing overlay selections retain legacy sentinel");
        Check(legacy.CompletedTutorials != null && legacy.ShopPurchases != null && legacy.RewardInventory != null, "Legacy journals initialize newly added collections");
        var parse = typeof(BackendGameData).GetMethod("ParsePlayerData", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (string value in new[] { "\"not-a-number\"", "-1", "2147483648", "null" })
        {
            bool rejected = false;
            try { parse.Invoke(new BackendGameData(), new object[] { LitJson.JsonMapper.ToObject("{\"Gold\":" + value + "}") }); }
            catch (TargetInvocationException error) when (error.InnerException is FormatException) { rejected = true; }
            Check(rejected, "Malformed server balance must not become zero: " + value);
        }
        foreach (string value in new[] { "", " ", "null" })
        {
            bool rejected = false;
            try { GGD.Periods.PeriodSerialization.Read<GGD.Periods.DailyData>(value); } catch { rejected = true; }
            Check(rejected, "Empty/null period payload must not overwrite counters");
        }
        return "PASS " + passed + ": legacy local field defaults and malformed balance/period rejection; no server access.";
    }
    public static string Periods()
    {
        if (Application.isPlaying) throw new Exception("Run this isolated clock fixture in Edit Mode.");
        passed = 0;
        var clockType = typeof(ServerClock);
        var time = clockType.GetField("_serverAtSync", BindingFlags.Static | BindingFlags.NonPublic);
        var realtime = clockType.GetField("_realtimeAtSync", BindingFlags.Static | BindingFlags.NonPublic);
        var synced = clockType.GetField("<IsSynced>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object oldTime = time.GetValue(null), oldRealtime = realtime.GetValue(null), oldSynced = synced.GetValue(null);
        string table = "PeriodVerification." + Guid.NewGuid().ToString("N");
        string owner = BackEnd.Backend.UserInDate;
        string key = "Period.Pending.v1." + table + "." + owner;
        var manager = new GGD.Periods.PeriodDataManager<GGD.Periods.DailyData>(table, false);
        void Seed(int count)
        {
            Set(manager, "_owner", owner); Set(manager, "_data", new GGD.Periods.DailyData { NormalPlayCount = count });
            Set(manager, "_period", "2026-11-01"); Set(manager, "_savedPeriod", "2026-11-01");
            Set(manager, "_savedPayload", GGD.Periods.PeriodSerialization.Write(new GGD.Periods.DailyData { NormalPlayCount = count }));
        }
        try
        {
            time.SetValue(null, new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc));
            realtime.SetValue(null, Time.realtimeSinceStartup); synced.SetValue(null, true);
            Seed(4);
            Check(!manager.IsDirty && !PlayerPrefs.HasKey(key), "Clean existing period has no write/journal");
            manager.Data.NormalPlayCount = 5; manager.IsDirty = true;
            Check(manager.IsDirty && PlayerPrefs.HasKey(key), "Dirty period is durably checkpointed");
            manager.Clear(); Seed(4); Restore(manager);
            Check(manager.Data.NormalPlayCount == 5 && manager.IsDirty, "Pending period survives session clear");
            manager.Clear(); Seed(5); Restore(manager);
            Check(!manager.IsDirty && !PlayerPrefs.HasKey(key), "Already committed period drops obsolete pending journal");
            manager.Data.NormalPlayCount = 6; manager.IsDirty = true;
            manager.Clear(); Seed(99);
            bool conflict = false; try { Restore(manager); } catch (TargetInvocationException) { conflict = true; }
            Check(conflict && PlayerPrefs.HasKey(key), "Different remote period snapshot keeps journal and rejects overwrite");
            manager.Clear(); PlayerPrefs.DeleteKey(key); Seed(5);
            time.SetValue(null, new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc));
            Check(manager.Data.NormalPlayCount == 0 && manager.IsDirty, "New period resets whole model and journals it");
            Check(GGD.Periods.PeriodSerialization.Key(new DateTime(2026, 11, 1, 19, 59, 59, DateTimeKind.Utc), true) == "2026-10-26", "Weekly pre-boundary");
            Check(GGD.Periods.PeriodSerialization.Key(new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc), true) == "2026-11-02", "Weekly KST05 boundary");
            return "PASS " + passed + ": actual period manager local journal, clean no-op, restart, lost ACK, conflict and KST rollover; no network.";
        }
        finally
        {
            manager.Clear(); PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
            time.SetValue(null, oldTime); realtime.SetValue(null, oldRealtime); synced.SetValue(null, oldSynced);
        }
    }
}
