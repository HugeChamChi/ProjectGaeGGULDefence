using Cysharp.Threading.Tasks;
using System;
using System.Threading;

// 플레이어와 관련된 플레이어 데이터, 프로필 Unlock여부, 우편 등을 관리하는 클래스
public static class Player
{
    /// <summary>Account-owned reached-round records, including durable unsent maxima.</summary>
    public static RoundRecordManager RoundRecords { get; } = new(new RoundRecordBackendStore(), () => BackEnd.Backend.UserInDate);
    public static PlayerDataController PlayerData   { get; private set; } = new();
    public static ProfileDataManager Profile        { get; private set; } = new();
    public static MailManager Mail                  { get; private set; } = new();
    public static PlayerCharacterManager Character  { get; private set; } = new();
    public static PlayerChiefManager Chief          { get; private set; } = new();
    public static ShopDataManager Shop              { get; private set; } = new();
    public static DailyManager Daily                { get; private set; } = new();
    public static GaeGGUL.Tutorial.PlayerTutorialManager Tutorial { get; private set; } = new();

    public static GGD.Periods.PeriodDataManager<GGD.Periods.DailyData> DailyData { get; } = new("DailyData", false);
    public static GGD.Periods.PeriodDataManager<GGD.Periods.WeeklyData> WeeklyData { get; } = new("WeeklyData", true);

    public static AccountDocument<AccountCharacterData> Characters { get; } = new("PlayerCharacterData", AccountCharacterData.Validate);
    public static AccountDocument<AccountLobbyData> LobbyProgress { get; } = new("PlayerLobbyData", AccountLobbyData.Validate);
    public static event Action OnAccountCleared;
    public static event Action OnAccountReady;
    private static BackendGameData _backendData;
    private static UniTaskCompletionSource _initialization;
    private static int _initializationGeneration;
    private static string _initializingOwner;

    public static void Inject(BackendGameData backendData)
    {
        _backendData = backendData;
        PlayerData.Inject(backendData);
    }

    /// <summary>Coalesces overlapping initialization for the same account without clearing loaded balances twice.</summary>
    public static UniTask InitializeAsync()
    {
        string owner = BackEnd.Backend.UserInDate;
        if (_initialization != null && _initializingOwner == owner) return _initialization.Task;
        if (_initialization != null) ClearAll();
        var completion = _initialization = new UniTaskCompletionSource();
        _initializingOwner = owner;
        InitializeCoreAsync(completion, owner, _initializationGeneration).Forget();
        return completion.Task;
    }

    private static async UniTaskVoid InitializeCoreAsync(UniTaskCompletionSource completion, string owner, int generation)
    {
        try
        {
        // 1. 핵심 데이터(플레이어 정보) 먼저 초기화
        await PlayerData.InitalizeAsync();
        RequireCurrentInitialization(owner, generation);
        if (!PlayerData.IsReady) throw new InvalidOperationException("플레이어 데이터를 불러오지 못했거나 저장 충돌 확인이 필요합니다.");
        await UniTask.WhenAll(Characters.InitializeAsync(), LobbyProgress.InitializeAsync());
        RequireCurrentInitialization(owner, generation);
        await RoundRecords.InitializeAsync();
        RequireCurrentInitialization(owner, generation);

        // 2. 서버 시간과 비교하여 새로운 날인지 판단
        await Daily.InitializeAsync();
        RequireCurrentInitialization(owner, generation);
        await UniTask.WhenAll(DailyData.InitializeAsync(), WeeklyData.InitializeAsync());
        RequireCurrentInitialization(owner, generation);
        PlayerData.RegisterAttendanceVisit(ServerClock.UtcNow.AddHours(9 - DailyManager.ResetHourKst).ToString("yyyy-MM-dd"));
        RequireCurrentInitialization(owner, generation);
        await GGD.Events.EventChartLoader.InitializeAsync(owner);
        RequireCurrentInitialization(owner, generation);
        using (var unitChartTimeout = new CancellationTokenSource())
        using (unitChartTimeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime))
            await UnitBalanceChartLoader.InitializeAsync(unitChartTimeout.Token);
        RequireCurrentInitialization(owner, generation);

        // 3. 나머지는 병렬로 초기화하되, IsNewDay 상태를 참조함
        using (var bonusTimeout = new CancellationTokenSource())
        using (bonusTimeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime))
            await UnitBonusChart.InitializeAsync(bonusTimeout.Token);
        RequireCurrentInitialization(owner, generation);
        await UniTask.WhenAll(
            Profile.InitalizeAsync(),
            Mail.InitalizeAsync(),
            Character.InitalizeAsync(),
            Chief.InitializeAsync(),
            Shop.InitializeAsync(),
            Tutorial.InitializeAsync()
        );
        RequireCurrentInitialization(owner, generation);

        // Persist attendance/reset together after optional daily shop initialization finishes.
        if (Daily.IsNewDay) PlayerData.MarkDirty();
        await PlayerData.TrySaveAsync();
        RequireCurrentInitialization(owner, generation);
        if (_autosaveCts == null)
        {
            _autosaveCts = new CancellationTokenSource();
            AutosaveLoopAsync(_autosaveCts.Token).Forget();
        }
        OnAccountReady?.Invoke();
        completion.TrySetResult();
        }
        catch (OperationCanceledException) { completion.TrySetCanceled(); }
        catch (Exception error) { completion.TrySetException(error); }
        finally { if (ReferenceEquals(_initialization, completion)) { _initialization = null; _initializingOwner = null; } }
    }

    private static void RequireCurrentInitialization(string owner, int generation)
    { if (generation != _initializationGeneration || owner != BackEnd.Backend.UserInDate) throw new OperationCanceledException(); }

    // 모든 하위 매니저들의 데이터를 초기화합니다. (로그아웃, 계정 변경 시 호출)
    public static void ClearAll()
    {
        _initializationGeneration++;
        _initialization?.TrySetCanceled(); _initialization = null; _initializingOwner = null;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
        _firstPendingSave = -1;
        _autosaveCts?.Cancel(); _autosaveCts?.Dispose(); _autosaveCts = null;
        Characters.Clear(); LobbyProgress.Clear(); GlobalData.SelectedParty = null;
        OnAccountCleared?.Invoke();
        RoundRecords.Clear();
        GGD.Events.EventChartLoader.Clear();
        UnitBonusChart.Clear();
        PlayerData.Clear();
        Profile.Clear();
        Mail.Clear();
        Character.Clear();
        Chief.Clear();
        Shop.Clear();
        Daily.Clear();
        DailyData.Clear();
        WeeklyData.Clear();
        Tutorial.Clear();
    }

    /// <summary>
    /// IsDirty가 true인 매니저들만 추출하여 뒤끝 DB에 부분 업데이트(UpdateV2)를 수행합니다.
    /// 게임 중 주요 시점에 동기적으로 바로 저장해야 할 때 호출합니다.
    /// </summary>
    public static async UniTask UpdateDirtyDataAsync()
    {
        var tasks = new System.Collections.Generic.List<UniTask>();

        Global.IClearable[] managers = {
            PlayerData, Profile, Mail, Character, Chief, Shop, Daily, Tutorial, RoundRecords, DailyData, WeeklyData, Characters, LobbyProgress
        };

        foreach (var manager in managers)
        {
            if (manager.IsDirty)
            {
                tasks.Add(manager.SaveAsync());
            }
        }

        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
            UnityEngine.Debug.Log($"[Backend DB] 저장 요청 처리. 미확인 변경은 각 매니저에 보관됩니다. 요청 수: {tasks.Count}");
        }
    }

    private static System.Threading.CancellationTokenSource _debounceCts;
    private static CancellationTokenSource _autosaveCts;
    private static double _firstPendingSave = -1;
    private const int DebounceDelayMs = 30000;

    /// <summary>
    /// Local journals are immediate. Cloud writes coalesce for 30 seconds, bounded by a 60-second flush.
    /// 유닛 연속 레벨업, 연속 골드 획득 등 잦은 상태 변화가 일어날 때 이 함수를 호출하세요.
    /// </summary>
    public static void RequestDebouncedSave()
    {
        if (_firstPendingSave < 0) _firstPendingSave = UnityEngine.Time.realtimeSinceStartupAsDouble;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new System.Threading.CancellationTokenSource();

        DebouncedSaveTask(_debounceCts.Token).Forget();
    }

    private static async UniTaskVoid DebouncedSaveTask(System.Threading.CancellationToken token)
    {
        int delay = (int)Math.Max(0, Math.Min(DebounceDelayMs,
            60000 - (UnityEngine.Time.realtimeSinceStartupAsDouble - _firstPendingSave) * 1000));
        bool isCancelled = await UniTask.Delay(delay, DelayType.Realtime, cancellationToken: token).SuppressCancellationThrow();
        
        if (isCancelled) return;
        _firstPendingSave = -1;
        await UpdateDirtyDataAsync();
    }

    private static async UniTaskVoid AutosaveLoopAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(60), DelayType.Realtime, cancellationToken: token);
                try { await UpdateDirtyDataAsync(); }
                catch (Exception error) { UnityEngine.Debug.LogWarning("[Save] Pending changes retained: " + error.Message); }
            }
        }
        catch (OperationCanceledException) { }
    }
}
