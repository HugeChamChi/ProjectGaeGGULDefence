using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Account balances, durable absolute-save retries, recovery and UI notifications.</summary>
public class PlayerDataController : IDisposable, Global.IClearable
{
    private PlayerData _data;
    private BackendGameData _backendData;
    private CancellationTokenSource _sessionCts;
    private UniTaskCompletionSource<bool> _save;
    private readonly Func<string> _accountProvider;
    private readonly Func<long> _clock;
    private readonly bool _externalClock;
    private long _nextClockSync;
    private readonly string _journalPrefix;
    private string _account;
    private string _confirmedSaveId = "";
    private int _generation;
    private long _revision;
    private const int SaveTimeoutSeconds = 15;
    private const int RetryDelaySeconds = 30;
    private const int ClockRetrySeconds = 30;

    /// <summary>Uses current authentication and BACKND server time by default; tests supply isolated owners and clocks.</summary>
    public PlayerDataController(Func<string> accountProvider = null, Func<long> clock = null, string journalPrefix = "PlayerData.Pending.v1.")
    {
        _accountProvider = accountProvider ?? (() => Backend.UserInDate);
        _externalClock = clock != null; _clock = clock ?? (() => ServerClock.UtcNowSeconds);
        _journalPrefix = journalPrefix;
    }
    /// <summary>Natural recovery runs only on trusted time: a supplied test clock, or a synced server clock.</summary>
    private bool ClockReady => _externalClock || ServerClock.IsSynced;
    private async UniTask SyncClockAsync()
    {
        if (_externalClock) return;
        _nextClockSync = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ClockRetrySeconds;
        try { await ServerClock.SyncAsync(_sessionCts?.Token ?? CancellationToken.None); }
        catch (OperationCanceledException) { }
    }
    /// <summary>Current state, null before load/after logout.</summary>
    public PlayerData Data => _data;
    /// <inheritdoc />
    public bool IsDirty { get; set; }
    /// <summary>A newer server save conflicts with a local pending snapshot.</summary>
    public bool HasSaveConflict { get; private set; }
    /// <summary>User-facing synchronization status.</summary>
    public string SaveStatus { get; private set; } = "";
    /// <summary>Whether authenticated balances can be changed.</summary>
    public bool IsReady => _data != null && _account == _accountProvider() && !string.IsNullOrEmpty(_account) && !HasSaveConflict;
    /// <summary>Profile refresh callback.</summary>
    public Action<PlayerData> OnPlayerProfilePopupUpdated;
    /// <summary>Balance/profile refresh callback.</summary>
    public Action<PlayerData> OnUpdateUI;
    /// <summary>Recovery countdown; zero hides the timer.</summary>
    public Action<int> OnStaminaRecoveryTimer;
    /// <summary>Account presentation settings changed/cleared.</summary>
    public event Action OnPresentationSettingsChanged;
    /// <summary>Synchronization status changed.</summary>
    public event Action OnSaveStatusChanged;
    /// <summary>Compatibility field for older test tools; production costs are always enforced.</summary>
    public static bool DebugSkipStaminaCost = false;
    /// <summary>Existing Player.Inject boundary.</summary>
    public void Inject(BackendGameData backendData) => _backendData = backendData;
    /// <summary>Loads the account, reconciles its journal and starts real-time recovery/retry.</summary>
    public async UniTask InitalizeAsync()
    {
        var completion = new UniTaskCompletionSource();
        InitData(() => completion.TrySetResult());
        await completion.Task;
        if (!IsReady) return;
        await SyncClockAsync();
        RecoverStamina();
        if (IsDirty) await TrySaveAsync();
    }
    /// <summary>Invalidates callbacks while retaining the owner's pending journal on disk.</summary>
    public void Clear()
    {
        _generation++;
        _sessionCts?.Cancel(); _sessionCts?.Dispose(); _sessionCts = null;
        _save?.TrySetResult(false); _save = null;
        _data = null; _account = null; IsDirty = false; HasSaveConflict = false;
        _backendData?.ClearSession();
        SetStatus(""); RefreshUI(null); OnStaminaRecoveryTimer?.Invoke(0);
    }
    /// <inheritdoc />
    public void Dispose() => Clear();
    /// <summary>Loads the authenticated account; failed reads never fabricate zero balances.</summary>
    public void InitData(Action onCompleted = null)
    {
        Clear(); _account = _accountProvider();
        if (_backendData == null || string.IsNullOrEmpty(_account)) { SetStatus("로그인 후 재화 확인 가능"); onCompleted?.Invoke(); return; }
        _sessionCts = new CancellationTokenSource();
        LoadAsync(_generation, onCompleted, _sessionCts.Token).Forget();
    }
    private async UniTaskVoid LoadAsync(int generation, Action done, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(SaveTimeoutSeconds), DelayType.Realtime);
        try
        {
            SetStatus("재화 불러오는 중");
            var completion = new UniTaskCompletionSource<PlayerData>();
            _backendData.GameDataGet(value => completion.TrySetResult(value));
            var value = await completion.Task.AttachExternalCancellation(timeout.Token);
            if (!Matches(generation)) return;
            if (value == null) { SetStatus("재화 조회 실패 · 다시 시도해 주세요."); return; }
            _data = value; _confirmedSaveId = value.SaveId ?? "";
            if (PlayerPrefs.HasKey(JournalKey))
            {
                var pending = JsonUtility.FromJson<PlayerDataJournal>(PlayerPrefs.GetString(JournalKey));
                if (pending?.Data == null) HasSaveConflict = true;
                else if (pending.Data.SaveId == _confirmedSaveId) DeleteJournal();
                else if ((pending.BaseSaveId ?? "") == _confirmedSaveId) { _data = pending.Data; IsDirty = true; }
                else HasSaveConflict = true;
            }
            SetStatus(HasSaveConflict ? "저장 충돌 · 서버 기록 확인 필요" : IsDirty ? "저장 재시도 대기" : "");
            RefreshUI(_data);
            RecoveryLoopAsync(generation, token).Forget();
        }
        catch (Exception error)
        {
            if (Matches(generation)) { SetStatus("재화 조회 실패 · 다시 시도해 주세요."); Debug.LogWarning("[PlayerData] " + error.Message); }
        }
        finally { done?.Invoke(); }
    }
    /// <summary>Explicit user choice to retain the loaded server state after a conflict.</summary>
    public void AcceptServerAfterConflict()
    {
        if (!HasSaveConflict || _account != _accountProvider()) return;
        DeleteJournal(); HasSaveConflict = false; IsDirty = false; SetStatus(""); RefreshUI(_data); RecoverStamina();
    }
    /// <summary>Publishes current state without changing amounts.</summary>
    public void RefreshUI(PlayerData data)
    { _data = data; OnUpdateUI?.Invoke(data); OnPlayerProfilePopupUpdated?.Invoke(data); OnPresentationSettingsChanged?.Invoke(); }
    private async UniTaskVoid RecoveryLoopAsync(int generation, CancellationToken token)
    {
        long nextRetry = _clock() + RetryDelaySeconds;
        try
        {
            while (Matches(generation))
            {
                if (!ClockReady && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= _nextClockSync) await SyncClockAsync();
                RecoverStamina();
                OnStaminaRecoveryTimer?.Invoke(ClockReady && _data != null && _data.Stamina < _data.MaxStamina
                    ? _backendData.GetTimeUntilNextStaminaRecovery(_data.LastStaminaRecoveryTime, _clock()) : 0);
                if (IsDirty && _clock() >= nextRetry) { nextRetry = _clock() + RetryDelaySeconds; await TrySaveAsync(); }
                await UniTask.Delay(TimeSpan.FromSeconds(1), DelayType.Realtime, cancellationToken: token);
            }
        }
        catch (OperationCanceledException) { }
    }
    /// <summary>Applies elapsed server-time recovery immediately, including foreground return. Paused while server time is unknown.</summary>
    public void RecoverStamina()
    {
        if (!IsReady || _backendData == null || !ClockReady || _data.Stamina >= _data.MaxStamina) return;
        var result = _backendData.CalculateStaminaRecovery(_data.Stamina, _data.LastStaminaRecoveryTime, _data.MaxStamina, _clock());
        if (result.stamina == _data.Stamina && result.recoveryTime == _data.LastStaminaRecoveryTime) return;
        _data.Stamina = result.stamina; _data.LastStaminaRecoveryTime = result.recoveryTime; SaveAndRefresh();
    }
    /// <summary>Grants positive Gold without overflow.</summary>
    public void AddGold(int amount) { if (CanAdd(_data?.Gold ?? 0, amount)) { _data.Gold += amount; SaveAndRefresh(); } }
    /// <summary>Spends positive Gold if sufficiently funded.</summary>
    public bool SpendGold(int amount) { if (!CanSpend(_data?.Gold ?? 0, amount)) return false; _data.Gold -= amount; SaveAndRefresh(); return true; }
    /// <summary>Grants positive Diamond without overflow.</summary>
    public void AddDiamond(int amount) { if (CanAdd(_data?.Diamond ?? 0, amount)) { _data.Diamond += amount; SaveAndRefresh(); } }
    /// <summary>Spends positive Diamond if sufficiently funded.</summary>
    public bool SpendDiamond(int amount) { if (!CanSpend(_data?.Diamond ?? 0, amount)) return false; _data.Diamond -= amount; SaveAndRefresh(); return true; }
    /// <summary>Grants stamina, preserving purchased over-cap amounts.</summary>
    public void AddStamina(int amount) { if (CanAdd(_data?.Stamina ?? 0, amount)) { _data.Stamina += amount; SaveAndRefresh(); } }
    /// <summary>Consumes stamina; leaving full capacity starts a fresh recovery interval.</summary>
    public bool UseStamina(int amount)
    {
        RecoverStamina();
        if (!CanSpend(_data?.Stamina ?? 0, amount)) return false;
        if (_data.Stamina >= _data.MaxStamina) _data.LastStaminaRecoveryTime = _clock();
        _data.Stamina -= amount; SaveAndRefresh(); return true;
    }
    /// <summary>Applies refill cost and reward together in one snapshot.</summary>
    public bool TryRefillStamina(StaminaConfig config)
    {
        RecoverStamina();
        if (config == null || !CanSpend(_data?.Diamond ?? 0, config.diamondCostForRefill) || !CanAdd(_data?.Stamina ?? 0, config.refillAmount)) return false;
        _data.Diamond -= config.diamondCostForRefill; _data.Stamina += config.refillAmount; SaveAndRefresh(); return true;
    }
    /// <summary>Atomically applies one server coupon receipt and its amounts to the local durable snapshot.</summary>
    public bool TryApplyCoupon(CouponReceipt receipt)
    {
        if (!IsReady || receipt == null || string.IsNullOrEmpty(receipt.Id)) return false;
        _data.CouponHistory ??= new System.Collections.Generic.List<CouponReceipt>();
        if (_data.CouponHistory.Exists(item => item.Id == receipt.Id)) return true;
        if (receipt.Gold < 0 || receipt.Diamond < 0 || receipt.Stamina < 0
            || (long)_data.Gold + receipt.Gold > int.MaxValue || (long)_data.Diamond + receipt.Diamond > int.MaxValue
            || (long)_data.Stamina + receipt.Stamina > int.MaxValue) return false;
        _data.Gold += receipt.Gold; _data.Diamond += receipt.Diamond; _data.Stamina += receipt.Stamina;
        _data.CouponHistory.Add(receipt); SaveAndRefresh(); return true;
    }
    /// <summary>Claims today's attendance reward and records the day in the same durable snapshot.</summary>
    /// <returns>The claimed day (1-7), or 0 when nothing was granted.</returns>
    public int TryClaimAttendance(AttendanceConfig config, string today)
    {
        if (!IsReady || config == null || !config.IsValid) return 0;
        var status = AttendanceRules.Evaluate(_data.AttendanceCount, _data.AttendanceLastDay, today);
        if (!status.CanClaim) return 0;
        var reward = config.Rewards[status.ClaimableDay - 1];
        int balance = reward.Currency switch
        {
            MailRewardConfig.Currency.Gold => _data.Gold,
            MailRewardConfig.Currency.Diamond => _data.Diamond,
            _ => _data.Stamina
        };
        if (balance < 0 || balance > int.MaxValue - reward.Amount) return 0;
        switch (reward.Currency)
        {
            case MailRewardConfig.Currency.Gold: _data.Gold += reward.Amount; break;
            case MailRewardConfig.Currency.Diamond: _data.Diamond += reward.Amount; break;
            default: _data.Stamina += reward.Amount; break;
        }
        _data.AttendanceCount = status.ClaimableDay; _data.AttendanceLastDay = today;
        SaveAndRefresh(); return status.ClaimableDay;
    }
    /// <summary>Reserves one paid entry. Failed navigation/retry reuses the reservation without charging twice.</summary>
    public async UniTask<bool> ReserveEntryAsync(int cost)
    {
        if (!IsReady || cost <= 0) return false;
        if (string.IsNullOrEmpty(_data.PendingEntryId))
        {
            RecoverStamina(); if (!CanSpend(_data.Stamina, cost)) return false;
            if (_data.Stamina >= _data.MaxStamina) _data.LastStaminaRecoveryTime = _clock();
            _data.Stamina -= cost; _data.PendingEntryId = Guid.NewGuid().ToString("N"); _data.PendingEntryCost = cost; SaveAndRefresh();
        }
        return await TrySaveAsync();
    }
    /// <summary>Actual production battle start consumes a prepaid reservation.</summary>
    public void ConsumeEntryReservation()
    { if (!IsReady || string.IsNullOrEmpty(_data.PendingEntryId)) return; _data.PendingEntryId = ""; _data.PendingEntryCost = 0; SaveAndRefresh(); }
    /// <summary>Updates the authenticated nickname's PlayerData mirror.</summary>
    public void SetNickname(string name) { if (!IsReady || _data.PlayerName == name) return; _data.PlayerName = name; SaveAndRefresh(); }
    /// <summary>Persists damage-number preference.</summary>
    public void SetDamageNumbers(bool enabled) { if (!IsReady || _data.ShowDamageNumbers == enabled) return; _data.ShowDamageNumbers = enabled; SaveAndRefresh(); }
    /// <summary>Persists vibration preference.</summary>
    public void SetVibration(bool enabled) { if (!IsReady || _data.VibrationEnabled == enabled) return; _data.VibrationEnabled = enabled; SaveAndRefresh(); }
    private bool CanSpend(int balance, int amount) => IsReady && amount > 0 && balance >= amount;
    private bool CanAdd(int balance, int amount) => IsReady && amount > 0 && balance >= 0 && balance <= int.MaxValue - amount;
    private void SaveAndRefresh()
    {
        _revision++; _data.SaveId = Guid.NewGuid().ToString("N"); IsDirty = true; PersistJournal();
        SetStatus("저장 대기"); RefreshUI(_data);
        if (_backendData != null && ReferenceEquals(this, Player.PlayerData)) Player.RequestDebouncedSave();
    }
    /// <summary>Marks an existing direct data mutation at a legacy boundary as a durable change.</summary>
    public void MarkDirty() { if (IsReady) SaveAndRefresh(); }
    /// <inheritdoc />
    public async UniTask SaveAsync() { await TrySaveAsync(); }
    /// <summary>Serializes writes; only a confirmed matching revision clears dirty state.</summary>
    public async UniTask<bool> TrySaveAsync()
    {
        if (!IsReady || _backendData == null) return false;
        if (_save != null) return await _save.Task;
        if (!IsDirty) return true;
        var operation = _save = new UniTaskCompletionSource<bool>();
        int generation = _generation; bool success = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(SaveTimeoutSeconds), DelayType.Realtime);
        try
        {
            SetStatus("저장 중");
            for (int attempt = 0; attempt < 3 && IsDirty; attempt++)
            {
                long revision = _revision;
                var snapshot = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(_data));
                var completion = new UniTaskCompletionSource<bool>();
                _backendData.GameDataUpdate(snapshot, onResult: saved => completion.TrySetResult(saved));
                bool saved = await completion.Task.AttachExternalCancellation(timeout.Token);
                if (!Matches(generation)) return false;
                if (!saved) break;
                _confirmedSaveId = snapshot.SaveId;
                if (revision == _revision) { IsDirty = false; DeleteJournal(); } else PersistJournal();
            }
            success = !IsDirty; SetStatus(success ? "" : "저장 실패 · 변경 내용 보관 중");
        }
        catch (Exception error)
        { if (Matches(generation)) { SetStatus("저장 확인 대기 · 재연결 후 재시도"); Debug.LogWarning("[PlayerData] " + error.Message); } }
        finally { operation.TrySetResult(success); if (ReferenceEquals(_save, operation)) _save = null; }
        return success;
    }
    /// <summary>Refreshes clean data on foreground return without overwriting concurrent local changes.</summary>
    public async UniTask ResumeAsync()
    {
        if (!IsReady) { if (_data == null) await InitalizeAsync(); return; }
        // The phone clock may have changed while paused; re-anchor before any recovery.
        if (!_externalClock) { ServerClock.Invalidate(); await SyncClockAsync(); }
        if (IsDirty && !await TrySaveAsync()) return;
        int generation = _generation; long revision = _revision;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(SaveTimeoutSeconds), DelayType.Realtime);
        try
        {
            var completion = new UniTaskCompletionSource<PlayerData>();
            _backendData.GameDataGet(value => completion.TrySetResult(value));
            var value = await completion.Task.AttachExternalCancellation(timeout.Token);
            if (!Matches(generation) || IsDirty || revision != _revision) return;
            if (value == null) { SetStatus("재조회 실패 · 마지막 확인 값 표시 중"); return; }
            _confirmedSaveId = value.SaveId ?? ""; RefreshUI(value); SetStatus("");
            RecoverStamina(); if (IsDirty) await TrySaveAsync();
        }
        catch (Exception) { if (Matches(generation)) SetStatus("재조회 실패 · 마지막 확인 값 표시 중"); }
    }
    private bool Matches(int generation) => generation == _generation && _account == _accountProvider();
    private string JournalKey => _journalPrefix + _account;
    private void PersistJournal() { PlayerPrefs.SetString(JournalKey, JsonUtility.ToJson(new PlayerDataJournal { BaseSaveId = _confirmedSaveId, Data = _data })); PlayerPrefs.Save(); }
    private void DeleteJournal() { PlayerPrefs.DeleteKey(JournalKey); PlayerPrefs.Save(); }
    private void SetStatus(string status) { SaveStatus = status; OnSaveStatusChanged?.Invoke(); }
}
