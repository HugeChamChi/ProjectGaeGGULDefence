using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Account session, durable pending maxima and cloud synchronization for reached rounds.</summary>
public sealed class RoundRecordManager : Global.IClearable
{
    private static readonly string[] Modes = { "NORMAL", "HARD" };
    private const int RequestTimeoutSeconds = 15;
    private readonly IRoundRecordStore _store;
    private readonly Func<string> _currentAccount;
    private readonly string _keyPrefix;
    private readonly Dictionary<string, int> _best = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _pending = new Dictionary<string, int>();
    private CancellationTokenSource _session;
    private bool _busy;
    private int _generation;

    /// <summary>Creates the manager with explicit cloud and authentication dependencies.</summary>
    public RoundRecordManager(IRoundRecordStore store, Func<string> currentAccount, string keyPrefix = "RoundRecords.v1.")
    { _store = store; _currentAccount = currentAccount; _keyPrefix = keyPrefix; }

    /// <summary>Account captured at initialization; never a nickname or device ID.</summary>
    public string Account { get; private set; }
    /// <summary>Record/status changes for lobby and result presentation.</summary>
    public event Action OnChanged;
    /// <summary>Whether pending values still need cloud confirmation.</summary>
    public bool IsDirty { get => _pending.Count > 0; set { } }
    /// <summary>Visible sync state; failures keep the local record and pending journal.</summary>
    public string Status { get; private set; } = "기록 불러오는 중";
    /// <summary>True only while the authenticated owner still matches this session.</summary>
    public bool OwnsAccount => _session != null && Account == _currentAccount();

    /// <summary>Unknown is distinct from a confirmed zero. Cached offline values remain available.</summary>
    public int? GetBest(string mode) => OwnsAccount && mode != null && _best.TryGetValue(mode, out int value) ? value : (int?)null;

    /// <summary>Replaces the account session and retries this account's durable pending journal.</summary>
    public async UniTask InitializeAsync()
    {
        Clear();
        Account = _currentAccount();
        if (string.IsNullOrEmpty(Account)) { Status = "로그인 후 기록 확인"; OnChanged?.Invoke(); return; }
        _session = new CancellationTokenSource();
        foreach (string mode in Modes)
        {
            if (PlayerPrefs.HasKey(Key(mode, "best"))) _best[mode] = Math.Max(0, PlayerPrefs.GetInt(Key(mode, "best")));
            int pending = PlayerPrefs.GetInt(Key(mode, "pending"), 0);
            if (pending > 0) { _pending[mode] = pending; _best[mode] = Math.Max(GetBest(mode) ?? 0, pending); }
        }
        await RefreshAsync();
    }

    /// <summary>Commits a higher reached round locally before any network operation. Equal/lower results are idempotent.</summary>
    public bool Record(string account, string mode, int reachedRound)
    {
        if (!OwnsAccount || account != Account || (mode != "NORMAL" && mode != "HARD") || reachedRound < 1) return false;
        if (reachedRound <= (GetBest(mode) ?? 0)) return false;
        _best[mode] = reachedRound;
        _pending[mode] = reachedRound;
        PlayerPrefs.SetInt(Key(mode, "best"), reachedRound);
        PlayerPrefs.SetInt(Key(mode, "pending"), reachedRound);
        PlayerPrefs.Save();
        Status = "기록 저장 대기";
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Loads remote maxima then retries pending saves; bounded and safe across scene/account changes.</summary>
    public async UniTask RefreshAsync()
    {
        if (!OwnsAccount || _busy) return;
        int generation = _generation;
        string account = Account;
        _busy = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_session.Token);
        using var timeoutTimer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(RequestTimeoutSeconds), DelayType.Realtime);
        try
        {
            Status = IsDirty ? "기록 저장 중" : "기록 불러오는 중";
            OnChanged?.Invoke();
            var remote = await _store.LoadAsync(account, timeout.Token);
            if (!IsCurrent(generation)) return;
            foreach (string mode in Modes)
            {
                int confirmed = remote.TryGetValue(mode, out int value) ? value : 0;
                ApplyConfirmed(mode, confirmed);
                if (_pending.TryGetValue(mode, out int target))
                {
                    confirmed = await _store.MergeAsync(account, mode, target, timeout.Token);
                    if (!IsCurrent(generation)) return;
                    ApplyConfirmed(mode, confirmed);
                }
            }
            Status = IsDirty ? "기록 저장 대기" : "";
        }
        catch (Exception error)
        {
            if (!IsCurrent(generation)) return;
            Status = IsDirty ? "기록 저장 대기 · 재연결 시 재시도" : "기록 조회 실패 · 재시도 가능";
            Debug.LogWarning("[RoundRecords] " + error.Message);
        }
        finally
        {
            if (IsCurrent(generation)) { _busy = false; OnChanged?.Invoke(); }
        }
    }

    /// <inheritdoc />
    public UniTask SaveAsync() => RefreshAsync();

    /// <summary>Cancels old callbacks and clears presentation, preserving only the owner's durable pending journal.</summary>
    public void Clear()
    {
        _generation++;
        _session?.Cancel(); _session?.Dispose(); _session = null;
        Account = null; _busy = false; _best.Clear(); _pending.Clear();
        Status = "로그인 후 기록 확인";
        OnChanged?.Invoke();
    }

    private bool IsCurrent(int generation) => generation == _generation && OwnsAccount;
    private string Key(string mode, string suffix) => _keyPrefix + Account + "." + mode + "." + suffix;
    private void ApplyConfirmed(string mode, int confirmed)
    {
        _best[mode] = Math.Max(GetBest(mode) ?? 0, Math.Max(0, confirmed));
        PlayerPrefs.SetInt(Key(mode, "best"), _best[mode]);
        if (_pending.TryGetValue(mode, out int pending) && pending <= confirmed)
        { _pending.Remove(mode); PlayerPrefs.DeleteKey(Key(mode, "pending")); }
        PlayerPrefs.Save();
    }
}
