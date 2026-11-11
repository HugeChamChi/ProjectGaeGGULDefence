using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Account balances, durable absolute-save retries, recovery and UI notifications.</summary>
public class PlayerDataController : IDisposable, Global.IClearable
{
    private PlayerData _data;
    private bool _characterPurchase;
    private BackendGameData _backendData;
    private CancellationTokenSource _sessionCts;
    private UniTaskCompletionSource<bool> _save;
    private readonly Func<string> _accountProvider;
    private readonly Func<long> _clock;
    private readonly bool _externalClock;
    private double _nextClockSync;
    private readonly string _journalPrefix;
    private string _account;
    private string _confirmedSaveId = "";
    private readonly System.Collections.Generic.List<string> _attemptedSaveIds = new();
    private double _retryAfter;
    private int _failureCount;
    private int _generation;
    private long _revision;
    private AccountProgressionConfig _accountProgression;
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
        _nextClockSync = Time.realtimeSinceStartupAsDouble + ClockRetrySeconds;
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
    public bool IsReady => !_characterPurchase && _data != null && _account == _accountProvider() && !string.IsNullOrEmpty(_account) && !HasSaveConflict;
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
    /// <summary>Root supplies the build-time account curve before loading. Test controllers may supply their own config.</summary>
    public void ConfigureAccountProgression(AccountProgressionConfig config)
    { config.Validate(); _accountProgression = config; }
    /// <summary>Loads the account, reconciles its journal and starts real-time recovery/retry.</summary>
    public async UniTask InitalizeAsync()
    {
        var completion = new UniTaskCompletionSource();
        InitData(() => completion.TrySetResult());
        await completion.Task;
        if (!IsReady) return;
        bool ownershipChanged = false;
        foreach (var entry in _data.RewardInventory.Owned)
            if (entry.Type == RewardType.Unit && entry.Count > 1) { entry.Count = 1; ownershipChanged = true; }
        if (ownershipChanged) SaveAndRefresh();
        await SyncClockAsync();
        RecoverStamina();
        if (IsDirty) await TrySaveAsync();
    }
    /// <summary>Invalidates callbacks while retaining the owner's pending journal on disk.</summary>
    public void Clear()
    {
        _generation++; _characterPurchase = false;
        _sessionCts?.Cancel(); _sessionCts?.Dispose(); _sessionCts = null;
        _save?.TrySetResult(false); _save = null;
        _data = null; _account = null; IsDirty = false; HasSaveConflict = false;
        _attemptedSaveIds.Clear(); _failureCount = 0; _retryAfter = 0;
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
            if (value == null) { SetStatus("재화 조회 실패, 다시 시도해 주세요."); return; }
            _data = value; _confirmedSaveId = value.SaveId ?? "";
            if (SignedPrefs.HasKey(JournalKey))
            {
                PlayerDataJournal pending = null;
                try { pending = JsonUtility.FromJson<PlayerDataJournal>(SignedPrefs.GetString(JournalKey)); }
                catch (ArgumentException) { HasSaveConflict = true; }
                if (pending?.Data == null || string.IsNullOrEmpty(pending.Data.SaveId)) HasSaveConflict = true;
                else if (pending.Data.SaveId == _confirmedSaveId) DeleteJournal();
                else if ((pending.BaseSaveId ?? "") == _confirmedSaveId || pending.AttemptedSaveIds?.Contains(_confirmedSaveId) == true)
                {
                    _data = pending.Data; IsDirty = true;
                    if (pending.AttemptedSaveIds != null) _attemptedSaveIds.AddRange(pending.AttemptedSaveIds);
                }
                else HasSaveConflict = true;
            }
            if (!HasSaveConflict && _accountProgression != null)
            {
                try { NormalizeAccountProgression(); }
                catch { _data = null; throw; }
            }
            SetStatus(HasSaveConflict ? "저장 충돌, 서버 기록 확인 필요" : IsDirty ? "저장 재시도 대기" : "");
            RefreshUI(_data);
            RecoveryLoopAsync(generation, token).Forget();
        }
        catch (Exception error)
        {
            if (Matches(generation)) { SetStatus("재화 조회 실패, 다시 시도해 주세요."); Debug.LogWarning("[PlayerData] " + error.Message); }
        }
        finally { done?.Invoke(); }
    }
    /// <summary>Explicit user choice to retain the loaded server state after a conflict.</summary>
    public void AcceptServerAfterConflict()
    {
        if (!HasSaveConflict || _account != _accountProvider()) return;
        if (_backendData.HasWriteConflict) { SetStatus("재접속하여 서버 기록을 먼저 확인해 주세요."); return; }
        _accountProgression?.Apply(_data.PlayerLevel, _data.PlayerExp, 0, out _, out _);
        DeleteJournal(); HasSaveConflict = false; IsDirty = false;
        NormalizeAccountProgression();
        SetStatus(IsDirty ? "저장 대기" : ""); RefreshUI(_data); RecoverStamina();
    }
    private void NormalizeAccountProgression()
    {
        if (_accountProgression == null || _data == null) return;
        // Preserve saved EXP through curve changes, without issuing new EXP or level-up rewards.
        _accountProgression.Apply(_data.PlayerLevel, _data.PlayerExp, 0, out int level, out int experience);
        int requirement = _accountProgression.RequiredExperience[level - 1];
        if (_data.PlayerLevel == level && _data.PlayerExp == experience && _data.MaxExp == requirement) return;
        _data.PlayerLevel = level; _data.PlayerExp = experience; _data.MaxExp = requirement;
        SaveAndRefresh();
    }
    /// <summary>Publishes current state without changing amounts.</summary>
    public void RefreshUI(PlayerData data)
    { _data = data; OnUpdateUI?.Invoke(data); OnPlayerProfilePopupUpdated?.Invoke(data); OnPresentationSettingsChanged?.Invoke(); }
    private async UniTaskVoid RecoveryLoopAsync(int generation, CancellationToken token)
    {
        try
        {
            while (Matches(generation))
            {
                if (!ClockReady && Time.realtimeSinceStartupAsDouble >= _nextClockSync) await SyncClockAsync();
                RecoverStamina();
                OnStaminaRecoveryTimer?.Invoke(ClockReady && _data != null && _data.Stamina < _data.MaxStamina
                    ? _backendData.GetTimeUntilNextStaminaRecovery(_data.LastStaminaRecoveryTime, _clock()) : 0);
                if (IsDirty && _failureCount > 0 && Time.realtimeSinceStartupAsDouble >= _retryAfter) await TrySaveAsync();
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
    /// <summary>Applies a verified store purchase and its deduplication ID to one durable balance snapshot.</summary>
    public bool TryApplyDiamondPurchase(GaeGGUL.IAP.DiamondPurchaseReceipt receipt)
    {
        if (!IsReady || receipt == null || string.IsNullOrWhiteSpace(receipt.Id)
            || string.IsNullOrWhiteSpace(receipt.ProductId) || receipt.Diamond <= 0) return false;
        _data.DiamondPurchases ??= new System.Collections.Generic.List<GaeGGUL.IAP.DiamondPurchaseReceipt>();
        var previous = _data.DiamondPurchases.Find(value => value.Id == receipt.Id);
        if (previous != null) return previous.ProductId == receipt.ProductId && previous.Diamond == receipt.Diamond;
        if (!CanAdd(_data.Diamond, receipt.Diamond)) return false;
        _data.Diamond += receipt.Diamond;
        _data.DiamondPurchases.Add(new GaeGGUL.IAP.DiamondPurchaseReceipt
            { Id = receipt.Id, ProductId = receipt.ProductId, Diamond = receipt.Diamond });
        SaveAndRefresh(); return true;
    }

    /// <summary>Claims today's attendance reward and records the day in the same durable snapshot.</summary>
    /// <returns>The claimed day (1-7), or 0 when nothing was granted.</returns>
    public void VisitEventMissions(DateTimeOffset now) { if(IsReady && GGD.Events.EventMissions.Visit(_data,now))SaveAndRefresh(); }
    public void RecordEventBattle(GGD.Events.DataOccurrence occurrence) { if(IsReady && GGD.Events.EventMissions.Battle(_data,occurrence))SaveAndRefresh(); }
    public bool ClaimEventMission(string key,DateTimeOffset now) { if(!IsReady || !GGD.Events.EventMissions.Claim(_data,key,now))return false;SaveAndRefresh();return true; }
    public int TryClaimAttendanceEvent(GGD.Events.AttendanceEventDefinition definition, DateTimeOffset now)
    {
        if (!IsReady) return 0;
        int day = GGD.Events.AttendanceEventRuntime.Claim(_data, definition, now);
        if (day > 0) SaveAndRefresh();
        return day;
    }
    public int TryClaimAttendance(AttendanceConfig config, string today)
    {
        if (!IsReady || config == null || !config.IsValid) return 0;
        RegisterAttendanceVisit(today);
        var status = AttendanceRules.Evaluate(_data, today);
        if (!status.CanClaim) return 0;
        var reward = config.Rewards[status.ClaimableDay - 1];
        if (reward.Items != null && reward.Items.Length > 0)
        {
            var next = RewardInventory.Apply(_data, config.RewardCatalog, "attendance:" + Guid.NewGuid().ToString("N"), reward.Items);
            next.AttendanceCount = status.ClaimableDay;
            next.AttendanceLastDay = today;
            next.AttendancePendingDays--;
            _data = next;
            SaveAndRefresh();
            return status.ClaimableDay;
        }
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
        _data.AttendancePendingDays--;
        SaveAndRefresh(); return status.ClaimableDay;
    }
    public void RegisterAttendanceVisit(string today)
    {
        if (IsReady && AttendanceRules.Accrue(_data, today)) SaveAndRefresh();
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

    /// <summary>Reserves a monotonic run in the existing account journal; old runs cannot replace it.</summary>
    public AccountExperienceTicket BeginAccountExperienceRun()
    {
        if (!IsReady || _data.AccountExperienceSequence < 0 || _data.AccountExperienceSequence == long.MaxValue) return null;
        _data.AccountExperienceSequence++;
        _data.AccountExperienceRunId = Guid.NewGuid().ToString("N");
        SaveAndRefresh();
        return new AccountExperienceTicket(_account, _generation, _data.AccountExperienceRunId, _data.AccountExperienceSequence);
    }

    /// <summary>Rejects callbacks from previous battles, logout/relogin and other accounts.</summary>
    public bool OwnsAccountExperienceRun(AccountExperienceTicket ticket) => IsReady && ticket != null
        && ticket.Account == _account && ticket.Generation == _generation
        && ticket.Sequence == _data.AccountExperienceSequence && ticket.RunId == _data.AccountExperienceRunId;

    /// <summary>Commits progression and its receipt together; saving/replaying never reapplies the delta.</summary>
    public bool TryApplyAccountExperience(AccountExperienceTicket ticket, AccountProgressionConfig config, int experience,
        out AccountExperienceReceipt receipt)
    {
        receipt = null;
        if (ticket == null || !IsReady || ticket.Account != _account || ticket.Generation != _generation || config == null || experience < 0) return false;
        var previous = _data.AccountExperienceReceipt;
        if (previous != null && previous.RunId == ticket.RunId && previous.Sequence == ticket.Sequence)
        { receipt = previous; return true; }
        if (!OwnsAccountExperienceRun(ticket) || (previous != null && previous.Sequence >= ticket.Sequence)) return false;
        int actual = config.Apply(_data.PlayerLevel, _data.PlayerExp, experience, out int level, out int remaining);
        receipt = new AccountExperienceReceipt
        { RunId = ticket.RunId, Sequence = ticket.Sequence, Experience = actual, LevelBefore = _data.PlayerLevel, LevelAfter = level };
        _data.PlayerLevel = level; _data.PlayerExp = remaining;
        _data.MaxExp = config.RequiredExperience[level - 1];
        _data.AccountExperienceReceipt = receipt;
        _data.AccountExperienceRunId = string.Empty;
        SaveAndRefresh();
        return true;
    }
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

    public void SetUpgradeProgress(ResearchSaveData tree, string importId = null)
    {
        if (!IsReady) throw new InvalidOperationException("Upgrade account is unavailable.");
        if (tree == null || string.IsNullOrWhiteSpace(tree.TreeKey)) throw new ArgumentException("Upgrade tree key required.");
        tree.GetLevels(tree.TreeKey);
        var next = JsonUtility.FromJson<ResearchSaveData>(JsonUtility.ToJson(tree));
        var progress = _data.UpgradeProgress;
        progress.Validate();
        var existing = progress.Trees.Find(x => x.TreeKey == tree.TreeKey);
        if (existing != null && JsonUtility.ToJson(existing) == JsonUtility.ToJson(next) && importId == null) return;
        if (existing != null) progress.Trees.Remove(existing);
        progress.Trees.Add(next); progress.Trees.Sort((a, b) => string.CompareOrdinal(a.TreeKey, b.TreeKey));
        if (importId != null) progress.LocalImportId = importId;
        SaveAndRefresh();
    }

    public bool IsShopPurchased(string day, int id, bool weekly = false) => IsReady && (weekly
        ? _data.WeeklyShopPurchasePeriod == day && _data.WeeklyShopPurchases.Contains(id)
        : _data.ShopPurchaseDay == day && _data.ShopPurchases.Contains(id));

    /// <summary>Price, reward and period receipt commit in one durable/account snapshot.</summary>
    public bool TryPurchaseShopItem(ShopItemData item, string day, bool weekly = false, bool repeatable = false)
    {
        if (!IsReady || item == null || item.ItemID < 0 || string.IsNullOrEmpty(day)) return false;
        if (item.Type == ItemType.Character && item.ItemID == 0) return false;
        if (!repeatable && IsShopPurchased(day, item.ShopID, weekly)) return true;
        var next = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(_data));
        if (!DeductPurchase(next, item.CurrencyType, item.Price) || item.Amount <= 0) return false;
        try
        {
            if (item.Type == ItemType.Character) AddPurchasedUnit(next, item.ItemID, item.Amount);
            else if (item.Type == ItemType.Currency && (CurrencyType)item.ItemID == CurrencyType.Gold) next.Gold = checked(next.Gold + item.Amount);
            else if (item.Type == ItemType.Currency && (CurrencyType)item.ItemID == CurrencyType.Diamond) next.Diamond = checked(next.Diamond + item.Amount);
            else return false; // Unsupported legacy item rewards must never charge the player.
        }
        catch (OverflowException) { return false; }
        if (!repeatable)
        {
            if (weekly)
            {
                if (next.WeeklyShopPurchasePeriod != day) { next.WeeklyShopPurchasePeriod = day; next.WeeklyShopPurchases.Clear(); }
                next.WeeklyShopPurchases.Add(item.ShopID);
            }
            else
            {
                if (next.ShopPurchaseDay != day) { next.ShopPurchaseDay = day; next.ShopPurchases.Clear(); }
                next.ShopPurchases.Add(item.ShopID);
            }
        }
        _data = next; SaveAndRefresh(); return true;
    }

    private static bool DeductPurchase(PlayerData next, CurrencyType currency, int price)
    {
        if (price < 0) return false;
        if (currency == CurrencyType.Free) return price == 0;
        if (currency == CurrencyType.Gold && next.Gold >= price) { next.Gold -= price; return true; }
        if (currency == CurrencyType.Diamond && next.Diamond >= price) { next.Diamond -= price; return true; }
        return false;
    }

    private static void AddPurchasedUnit(PlayerData next, int id, int amount)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var owned = next.RewardInventory.Owned.Find(x => x.Type == RewardType.Unit && x.TargetId == id);
        if (owned == null) { owned = new RewardOwnedEntry { Type = RewardType.Unit, TargetId = id }; next.RewardInventory.Owned.Add(owned); }
        owned.Count = amount > 0 ? 1 : owned.Count;
    }

    public async UniTask<bool> TryCommitCharacterUpgradeAsync(int cost, AccountJsonStore.Row characterRow, string characterJson, string currency = "Gold")
    {
        if (IsReady && string.IsNullOrEmpty(_confirmedSaveId)) MarkDirty();
        if (cost <= 0 || !IsReady || _backendData == null || !await TrySaveAsync() || !IsReady || GaeGGUL.DeckUpgrade.DeckUpgradeCurrency.Balance(_data, currency) < cost) return false;
        var next = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(_data));
        GaeGGUL.DeckUpgrade.DeckUpgradeCurrency.Spend(next, currency, cost); next.SaveId = Guid.NewGuid().ToString("N");
        return await CommitCharacterChangeAsync(next, characterRow, characterJson);
    }

    public async UniTask<bool> TryCommitTestCharacterOwnershipAsync(AccountJsonStore.Row characterRow, string characterJson, int[] unitIds, bool owned)
    {
        if (IsReady && string.IsNullOrEmpty(_confirmedSaveId)) MarkDirty();
        if (!IsReady || _backendData == null || !await TrySaveAsync() || !IsReady) return false;
        var next = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(_data));
        if (owned)
        {
            foreach (int id in unitIds) AddPurchasedUnit(next, id, 1);
        }
        else
        {
            foreach (var entry in next.RewardInventory.Owned)
                if (entry.Type == RewardType.Unit) entry.Count = 0;
        }
        next.SaveId = Guid.NewGuid().ToString("N");
        return await CommitCharacterChangeAsync(next, characterRow, characterJson);
    }

    private async UniTask<bool> CommitCharacterChangeAsync(PlayerData next, AccountJsonStore.Row characterRow, string characterJson)
    {
        int generation = _generation;
        _characterPurchase = true;
        try
        {
            bool saved = await _backendData.CommitCharacterUpgradeAsync(next, _confirmedSaveId, characterRow, characterJson);
            if (!Matches(generation)) return false;
            if (!saved)
            {
                HasSaveConflict = true; SetStatus("강화 저장 결과를 확인해야 합니다. 재접속해 주세요."); return false;
            }
            _data = next; _confirmedSaveId = next.SaveId; _revision++;
            _attemptedSaveIds.Clear(); IsDirty = false; DeleteJournal(); SetStatus("");
            return true;
        }
        catch (Exception)
        {
            if (generation == _generation) { HasSaveConflict = true; SetStatus("강화 저장 결과를 확인해야 합니다. 재접속해 주세요."); }
            return false;
        }
        finally
        {
            if (generation == _generation) { _characterPurchase = false; RefreshUI(_data); }
        }
    }

    public void CompleteTutorial(string id)
    {
        if (!IsReady || string.IsNullOrEmpty(id) || _data.CompletedTutorials.Contains(id)) return;
        _data.CompletedTutorials.Add(id); SaveAndRefresh();
    }

    public bool SelectChief(int id)
    {
        if (!IsReady || id < 0) return false;
        if (_data.SelectedChiefId != id) { _data.SelectedChiefId = id; SaveAndRefresh(); }
        return true;
    }
    public bool SelectProfile(int icon, int frame)
    {
        if (!IsReady || icon < 0 || frame < 0) return false;
        if (_data.SelectedIconId != icon || _data.SelectedFrameId != frame)
        { _data.SelectedIconId = icon; _data.SelectedFrameId = frame; SaveAndRefresh(); }
        return true;
    }
    public void AddOwnedCharacter(int id, int count)
    {
        if (!IsReady || id <= 0 || count <= 0) return;
        int existing = _data.RewardInventory.Count(RewardType.Unit, id);
        if ((long)existing + count < 0 || (long)existing + count > int.MaxValue) return;
        AddPurchasedUnit(_data, id, count); SaveAndRefresh();
    }
    public async UniTask<bool> ClaimRoundRewardAsync(RoundReachRewards.Definition reward)
    {
        if (!IsReady || !Player.RoundRecords.OwnsAccount || Player.RoundRecords.Account != _account) return false;
        if (!RoundReachRewards.Apply(_data, reward, Player.RoundRecords.GetConfirmedBest("NORMAL"))) return false;
        SaveAndRefresh();
        return await TrySaveAsync();
    }
    public async UniTask<bool> GrantRewardAsync(RewardCatalog catalog, string grantId, RewardAmount[] rewards)
    {
        if (!IsReady) return false;
        var next = RewardInventory.Apply(_data, catalog, grantId, rewards);
        if (next != null) { _data = next; SaveAndRefresh(); }
        return await TrySaveAsync();
    }
    /// <inheritdoc />
    public async UniTask SaveAsync() { await TrySaveAsync(); }
    /// <summary>Serializes writes; only a confirmed matching revision clears dirty state.</summary>
    public async UniTask<bool> TrySaveAsync()
    {
        if (!IsReady || _backendData == null) return false;
        if (!IsDirty) return true;
        if (_save == null)
        {
            if (Time.realtimeSinceStartupAsDouble < _retryAfter) return false;
            var operation = _save = new UniTaskCompletionSource<bool>();
            SaveCoreAsync(operation).Forget();
            // A fake/local transport is allowed to complete synchronously.
            return await WaitForSaveAsync(operation);
        }
        return await WaitForSaveAsync(_save);
    }

    private async UniTask<bool> WaitForSaveAsync(UniTaskCompletionSource<bool> operation)
    {
        using var timeout = new CancellationTokenSource();
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(SaveTimeoutSeconds), DelayType.Realtime);
        try { return await operation.Task.AttachExternalCancellation(timeout.Token); }
        catch (OperationCanceledException) { return false; }
    }

    private async UniTaskVoid SaveCoreAsync(UniTaskCompletionSource<bool> operation)
    {
        int generation = _generation; bool success = false;
        var token = _sessionCts.Token;
        try
        {
            SetStatus("저장 중");
            for (int attempt = 0; attempt < 3 && IsDirty; attempt++)
            {
                long revision = _revision;
                var snapshot = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(_data));
                if (!_attemptedSaveIds.Contains(snapshot.SaveId)) _attemptedSaveIds.Add(snapshot.SaveId);
                PersistJournal(); // Persist ancestry BEFORE dispatch, including changes made during this write.
                var completion = new UniTaskCompletionSource<bool>();
                _backendData.GameDataUpdate(snapshot, onResult: saved => completion.TrySetResult(saved));
                // Caller timeout does not cancel the SDK request or release the write lock.
                bool saved = await completion.Task.AttachExternalCancellation(token);
                if (!Matches(generation)) return;
                if (!saved) { HasSaveConflict = _backendData.HasWriteConflict; break; }
                _confirmedSaveId = snapshot.SaveId;
                _attemptedSaveIds.Clear();
                if (revision == _revision) { IsDirty = false; DeleteJournal(); } else PersistJournal();
            }
            success = !IsDirty; SetStatus(HasSaveConflict ? "저장 충돌, 변경 내용 보관 중. 재접속 후 확인 필요" : success ? "" : "저장 실패, 변경 내용 보관 중");
        }
        catch (Exception error)
        { if (Matches(generation)) { SetStatus("저장 확인 대기, 재연결 후 재시도"); Debug.LogWarning("[PlayerData] " + error.Message); } }
        finally
        {
            if (Matches(generation))
            {
                _failureCount = success ? 0 : Math.Min(_failureCount + 1, 5);
                _retryAfter = success ? 0 : Time.realtimeSinceStartupAsDouble + Math.Min(300, RetryDelaySeconds * (1 << (_failureCount - 1)));
            }
            operation.TrySetResult(success); if (ReferenceEquals(_save, operation)) _save = null;
        }
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
            if (value == null) { SetStatus("재조회 실패, 마지막 확인 값 표시 중"); return; }
            _accountProgression?.Apply(value.PlayerLevel, value.PlayerExp, 0, out _, out _);
            _confirmedSaveId = value.SaveId ?? ""; _data = value;
            NormalizeAccountProgression();
            RefreshUI(_data); SetStatus(IsDirty ? "저장 대기" : "");
            RecoverStamina(); if (IsDirty) await TrySaveAsync();
        }
        catch (Exception) { if (Matches(generation)) SetStatus("재조회 실패, 마지막 확인 값 표시 중"); }
    }
    private bool Matches(int generation) => generation == _generation && _account == _accountProvider();
    private string JournalKey => _journalPrefix + _account;
    private void PersistJournal() { SignedPrefs.SetString(JournalKey, JsonUtility.ToJson(new PlayerDataJournal { BaseSaveId = _confirmedSaveId, AttemptedSaveIds = _attemptedSaveIds, Data = _data })); PlayerPrefs.Save(); }
    private void DeleteJournal() { SignedPrefs.DeleteKey(JournalKey); PlayerPrefs.Save(); }
    private void SetStatus(string status) { SaveStatus = status; OnSaveStatusChanged?.Invoke(); }
}
