using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Account session, signed pending journal and cloud synchronization for reached rounds.
/// Device storage is never a source of truth: the cached best is display-only, rewards read the
/// server-confirmed value, and a pending journal entry is merged only when its signature matches.
/// </summary>
public sealed class RoundRecordManager : Global.IClearable
{
    private static readonly string[] Modes = { "NORMAL", "HARD" };
    private const int RequestTimeoutSeconds = 15;
    // Deters editing the saved file by hand. It does not stop memory or binary tampering;
    // that is covered by server-side run validation (TODO G13).
    private const string JournalSecret = "GGD.RoundRecords.Journal.7f3c1a9e";
    private readonly IRoundRecordStore _store;
    private readonly Func<string> _currentAccount;
    private readonly string _keyPrefix;
    private readonly Dictionary<string, int> _confirmed = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _pending = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _cached = new Dictionary<string, int>();
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

    /// <summary>
    /// Best round for display only. Unknown is distinct from a confirmed zero. Uses the server value when
    /// loaded, otherwise the device cache, plus any verified pending result. Never gate rewards on this.
    /// </summary>
    public int? GetBest(string mode)
    {
        if (!OwnsAccount || mode == null) return null;
        bool known = _confirmed.TryGetValue(mode, out int best) || _cached.TryGetValue(mode, out best);
        if (_pending.TryGetValue(mode, out int pending)) { known = true; best = Math.Max(best, pending); }
        return known ? best : (int?)null;
    }

    /// <summary>Best round confirmed by the server in this session; null until loaded. Use this for rewards.</summary>
    public int? GetConfirmedBest(string mode) =>
        OwnsAccount && mode != null && _confirmed.TryGetValue(mode, out int value) ? value : (int?)null;

    /// <summary>Replaces the account session and retries this account's durable pending journal.</summary>
    public async UniTask InitializeAsync()
    {
        Clear();
        Account = _currentAccount();
        if (string.IsNullOrEmpty(Account)) { Status = "로그인 후 기록 확인"; OnChanged?.Invoke(); return; }
        _session = new CancellationTokenSource();
        foreach (string mode in Modes)
        {
            if (PlayerPrefs.HasKey(Key(mode, "best"))) _cached[mode] = Math.Max(0, PlayerPrefs.GetInt(Key(mode, "best")));
            int pending = PlayerPrefs.GetInt(Key(mode, "pending"), 0);
            if (pending <= 0) continue;
            if (PlayerPrefs.GetString(Key(mode, "pendingSig"), "") == Sign(mode, pending)) _pending[mode] = pending;
            else
            {
                // Unsigned or edited journal entries are dropped and never reach the server.
                DeletePending(mode);
                Debug.LogWarning("[RoundRecords] Discarded unverified pending record for " + mode);
            }
        }
        await RefreshAsync();
    }

    /// <summary>Commits a higher reached round locally before any network operation. Equal/lower results are idempotent.</summary>
    public bool Record(string account, string mode, int reachedRound)
    {
        if (!OwnsAccount || account != Account || (mode != "NORMAL" && mode != "HARD") || reachedRound < 1) return false;
        // Compare against trusted values only, so an edited display cache cannot affect recording.
        int trusted = Math.Max(GetConfirmedBest(mode) ?? 0, _pending.TryGetValue(mode, out int queued) ? queued : 0);
        if (reachedRound <= trusted) return false;
        _pending[mode] = reachedRound;
        _cached[mode] = GetBest(mode) ?? reachedRound;
        PlayerPrefs.SetInt(Key(mode, "best"), _cached[mode]);
        PlayerPrefs.SetInt(Key(mode, "pending"), reachedRound);
        PlayerPrefs.SetString(Key(mode, "pendingSig"), Sign(mode, reachedRound));
        PlayerPrefs.Save();
        Status = "기록 저장 대기";
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Loads remote maxima then retries pending saves; bounded and safe across scene/account changes.</summary>
    public UniTask RefreshAsync() => SynchronizeAsync(true);

    private async UniTask SynchronizeAsync(bool refresh)
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
            Dictionary<string, int> remote = null;
            if (refresh)
            {
                remote = await _store.LoadAsync(account, timeout.Token);
                if (!IsCurrent(generation)) return;
            }
            foreach (string mode in Modes)
            {
                int confirmed;
                if (remote != null)
                {
                    confirmed = remote.TryGetValue(mode, out int value) ? value : 0;
                    ApplyConfirmed(mode, confirmed);
                }
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
            Status = IsDirty ? "기록 저장 대기, 재연결 시 재시도" : "기록 조회 실패, 재시도 가능";
            Debug.LogWarning("[RoundRecords] " + error.Message);
        }
        finally
        {
            if (IsCurrent(generation)) { _busy = false; OnChanged?.Invoke(); }
        }
    }

    /// <inheritdoc />
    public UniTask SaveAsync() => IsDirty ? SynchronizeAsync(false) : UniTask.CompletedTask;

    /// <summary>Cancels old callbacks and clears presentation, preserving only the owner's durable pending journal.</summary>
    public void Clear()
    {
        _generation++;
        _session?.Cancel(); _session?.Dispose(); _session = null;
        Account = null; _busy = false; _confirmed.Clear(); _pending.Clear(); _cached.Clear();
        Status = "로그인 후 기록 확인";
        OnChanged?.Invoke();
    }

    private bool IsCurrent(int generation) => generation == _generation && OwnsAccount;
    private string Key(string mode, string suffix) => _keyPrefix + Account + "." + mode + "." + suffix;
    private void ApplyConfirmed(string mode, int confirmed)
    {
        confirmed = Math.Max(0, confirmed);
        _confirmed[mode] = _confirmed.TryGetValue(mode, out int previous) ? Math.Max(previous, confirmed) : confirmed;
        if (_pending.TryGetValue(mode, out int pending) && pending <= _confirmed[mode])
        { _pending.Remove(mode); DeletePending(mode); }
        // The server value replaces whatever the device cache held, including an edited number.
        _cached[mode] = GetBest(mode) ?? 0;
        PlayerPrefs.SetInt(Key(mode, "best"), _cached[mode]);
        PlayerPrefs.Save();
    }

    private void DeletePending(string mode)
    {
        PlayerPrefs.DeleteKey(Key(mode, "pending"));
        PlayerPrefs.DeleteKey(Key(mode, "pendingSig"));
    }

    private string Sign(string mode, int round)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(JournalSecret + Account));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(_keyPrefix + "|" + Account + "|" + mode + "|" + round));
        return BitConverter.ToString(hash).Replace("-", "");
    }
}
