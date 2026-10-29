using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Server-time attendance flow over the account PlayerData snapshot. Never falls back to the device clock.</summary>
public sealed class AttendanceService
{
    /// <summary>Outcome of one claim attempt.</summary>
    public enum ClaimResult { Claimed, ClaimedSavePending, AlreadyClaimed, NotReady, TimeUnavailable, Invalid }

    private readonly PlayerDataController _data;
    private readonly Func<CancellationToken, UniTask<DateTime?>> _serverUtc;
    private readonly bool _usesSharedClock;
    private bool _busy;

    /// <summary>Authored rewards; null when the Resources asset is missing.</summary>
    public AttendanceConfig Config { get; }
    /// <summary>Last server UTC time and the realtime it was observed at, for countdown display.</summary>
    public DateTime? LastServerUtc { get; private set; }
    private float _observedAt;

    /// <summary>Uses Player.PlayerData and BACKND server time by default; tests supply both.</summary>
    public AttendanceService(AttendanceConfig config, PlayerDataController data = null, Func<CancellationToken, UniTask<DateTime?>> serverUtc = null)
    { Config = config; _data = data ?? Player.PlayerData; _usesSharedClock = serverUtc == null; _serverUtc = serverUtc ?? ServerClock.FetchUtcAsync; }

    /// <summary>Uses the server clock already synchronized by lobby initialization; performs no request or grant.</summary>
    public bool UseSynchronizedClock()
    {
        if (!_usesSharedClock) return LastServerUtc != null && _data.IsReady;
        if (!ServerClock.IsSynced || !_data.IsReady) return false;
        LastServerUtc = ServerClock.UtcNow; _observedAt = Time.realtimeSinceStartup;
        return true;
    }

    /// <summary>Progress currently held in the account snapshot.</summary>
    public AttendanceRules.Status Evaluate() =>
        _data?.Data == null || Config == null || LastServerUtc == null ? default
        : AttendanceRules.Evaluate(_data.Data, Config.DayKey(EstimatedServerUtc));

    /// <summary>Server time advanced locally since the last fetch.</summary>
    public DateTime EstimatedServerUtc => (LastServerUtc ?? DateTime.UtcNow).AddSeconds(Time.realtimeSinceStartup - _observedAt);

    /// <summary>Re-reads server time; false when it is unavailable.</summary>
    public async UniTask<bool> RefreshAsync(CancellationToken token)
    {
        var now = await _serverUtc(token);
        if (now == null) return false;
        LastServerUtc = now; _observedAt = Time.realtimeSinceStartup; return true;
    }

    /// <summary>Re-reads the account and server day, then grants today's reward once.</summary>
    public async UniTask<(ClaimResult result, int day)> ClaimAsync(CancellationToken token)
    {
        if (_busy) return (ClaimResult.NotReady, 0);
        if (Config == null || !Config.IsValid) return (ClaimResult.Invalid, 0);
        _busy = true;
        try
        {
            if (_data == null || !_data.IsReady) return (ClaimResult.NotReady, 0);
            // Pick up a claim another device already saved before deciding.
            await _data.ResumeAsync();
            token.ThrowIfCancellationRequested();
            if (!_data.IsReady) return (ClaimResult.NotReady, 0);
            if (!await RefreshAsync(token)) return (ClaimResult.TimeUnavailable, 0);
            string today = Config.DayKey(LastServerUtc.Value);
            _data.RegisterAttendanceVisit(today);
            if (!AttendanceRules.Evaluate(_data.Data, today).CanClaim) return (ClaimResult.AlreadyClaimed, 0);
            int day = _data.TryClaimAttendance(Config, today);
            if (day == 0) return (ClaimResult.Invalid, 0);
            bool saved = await _data.TrySaveAsync();
            return (saved ? ClaimResult.Claimed : ClaimResult.ClaimedSavePending, day);
        }
        finally { _busy = false; }
    }
}
