using System;
using System.Threading;
using System.Threading.Tasks;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

/// <summary>Existing authenticated test session only. No grants/spending; saves current values.</summary>
public static class SaveReliabilityLiveChecks
{
    private static async UniTask<BackendReturnObject> Wait(UniTaskCompletionSource<BackendReturnObject> operation)
    {
        using var timeout = new CancellationTokenSource();
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(20), DelayType.Realtime);
        return await operation.Task.AttachExternalCancellation(timeout.Token);
    }
    private static async UniTask<JsonData> Read(string table)
    {
        var completion = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.GameData.GetMyData(table, new Where(), result => completion.TrySetResult(result));
        var response = await Wait(completion);
        if (!response.IsSuccess()) throw new Exception(table + " read " + response.GetStatusCode());
        var rows = response.FlattenRows();
        if (rows.Count != 1) throw new Exception(table + " expected one row, actual=" + rows.Count);
        return rows[0];
    }
    public static async Task<string> Run()
    {
        if (!Backend.IsLogin || !Player.PlayerData.IsReady) throw new Exception("Requires existing loaded login; does not authenticate.");
        string owner = Backend.UserInDate;
        await Player.UpdateDirtyDataAsync();
        var before = await Read("PlayerData");
        string oldVersion = before.GetString("PlayerSaveId", "");
        if (string.IsNullOrEmpty(oldVersion)) throw new Exception("Expected versioned fixture row.");
        Player.PlayerData.MarkDirty(); // Changes the save version only; no purchase, reward or balance mutation.
        if (!await Player.PlayerData.TrySaveAsync()) throw new Exception("Current snapshot save not acknowledged.");
        var after = await Read("PlayerData");
        if (owner != Backend.UserInDate || after.GetString("PlayerSaveId", "") != Player.PlayerData.Data.SaveId || Player.PlayerData.IsDirty)
            throw new Exception("Snapshot version was not confirmed.");
        foreach (string column in new[] { "Gold", "Diamond" })
            if (before.GetInt(column, -1) != after.GetInt(column, -2)) throw new Exception("Unexpected balance change: " + column);
        foreach (string column in new[] { "ShopPurchaseDay", "ShopPurchases", "CompletedTutorials", "SelectedChiefId", "SelectedIconId", "SelectedFrameId" })
            if (!after.ContainsKey(column)) throw new Exception("New snapshot column missing: " + column);
        // A stale request carries an identical harmless field value. Even a faulty filter cannot grant/spend.
        var stale = new UniTaskCompletionSource<BackendReturnObject>();
        var where = new Where(); where.Equal("PlayerSaveId", oldVersion);
        var param = new Param(); param.Add("ShowDamageNumbers", after.GetInt("ShowDamageNumbers", 1));
        Backend.GameData.Update("PlayerData", where, param, result => stale.TrySetResult(result));
        var rejected = await Wait(stale);
        if (rejected.IsSuccess() || rejected.GetStatusCode() != "404") throw new Exception("Stale version not rejected: " + rejected.GetStatusCode());
        var final = await Read("PlayerData");
        if (final.GetString("PlayerSaveId", "") != after.GetString("PlayerSaveId", "")) throw new Exception("Stale write changed version.");
        var daily = await Read("DailyData"); var weekly = await Read("WeeklyData");
        Player.DailyData.IsDirty = true; Player.WeeklyData.IsDirty = true;
        await Player.DailyData.SaveAsync(); await Player.WeeklyData.SaveAsync();
        var dailyAfter = await Read("DailyData"); var weeklyAfter = await Read("WeeklyData");
        if (daily.GetString("PayloadJson", "") != dailyAfter.GetString("PayloadJson", "") ||
            weekly.GetString("PayloadJson", "") != weeklyAfter.GetString("PayloadJson", "") || Player.DailyData.IsDirty || Player.WeeklyData.IsDirty)
            throw new Exception("Period save/readback mismatch.");
        return "PASS: actual BACKND current snapshot save/re-read, six new columns, unchanged Gold/Diamond, stale-version 404, final version preserved, conditional DailyData/WeeklyData save + readback with unchanged counters and exactly one row. No rewards granted or spending performed. Concurrent device races were not simulated.";
    }
}
