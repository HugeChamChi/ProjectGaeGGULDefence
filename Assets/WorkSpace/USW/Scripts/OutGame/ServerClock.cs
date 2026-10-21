using System;
using System.Globalization;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// BACKND server time advanced by a monotonic realtime counter, so changing the phone clock cannot speed up
/// stamina recovery. Resynced on load and foreground return; unsynced time is reported so callers can fail closed.
/// </summary>
public static class ServerClock
{
    private const int TimeoutSeconds = 10;
    private static DateTime _serverAtSync;
    private static float _realtimeAtSync;

    /// <summary>Whether a server time has been observed since the last invalidation.</summary>
    public static bool IsSynced { get; private set; }

    /// <summary>Current server UTC estimate; device UTC only before the first sync.</summary>
    public static DateTime UtcNow => IsSynced ? _serverAtSync.AddSeconds(Time.realtimeSinceStartup - _realtimeAtSync) : DateTime.UtcNow;

    /// <summary>Current server time as Unix seconds.</summary>
    public static long UtcNowSeconds => new DateTimeOffset(UtcNow, TimeSpan.Zero).ToUnixTimeSeconds();

    /// <summary>Marks time as unknown (e.g. after a pause) until the next successful sync.</summary>
    public static void Invalidate() => IsSynced = false;

    /// <summary>Fetches server time and re-anchors the clock; false (and unsynced) on failure.</summary>
    public static async UniTask<bool> SyncAsync(CancellationToken token)
    {
        if (!Application.isPlaying) return false;
        var now = await FetchUtcAsync(token);
        if (now == null) { IsSynced = false; return false; }
        _serverAtSync = now.Value; _realtimeAtSync = Time.realtimeSinceStartup; IsSynced = true;
        return true;
    }

    /// <summary>One BACKND server-time request; null on failure or timeout. Never substitutes the device clock.</summary>
    public static async UniTask<DateTime?> FetchUtcAsync(CancellationToken token)
    {
        if (!Backend.IsLogin) return null;
        var completion = new UniTaskCompletionSource<DateTime?>();
        Backend.Utils.GetServerTime(bro =>
        {
            try
            {
                if (!bro.IsSuccess()) { completion.TrySetResult(null); return; }
                string text = bro.GetReturnValuetoJSON()["utcTime"].ToString();
                completion.TrySetResult(DateTime.SpecifyKind(DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTimeKind.Utc));
            }
            catch (Exception) { completion.TrySetResult(null); }
        });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(TimeoutSeconds), DelayType.Realtime);
        try { return await completion.Task.AttachExternalCancellation(timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
    }
}
