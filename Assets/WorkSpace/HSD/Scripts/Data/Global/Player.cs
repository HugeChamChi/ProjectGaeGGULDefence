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
        // 0. 아웃게임 밸런스 차트: 신규 계정 생성과 스태미나 계산이 읽으므로 플레이어 데이터보다 먼저 받는다. 실패하면 로그인에서 멈춘다.
        using (var outgameTimeout = new CancellationTokenSource())
        using (outgameTimeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime))
            await GGD.BackendCharts.OutgameBalance.Chart.LoadAsync(outgameTimeout.Token);
        RequireCurrentInitialization(owner, generation);
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
        using (var runBalanceTimeout = new CancellationTokenSource())
        using (runBalanceTimeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime))
            await UniTask.WhenAll(GGD.BackendCharts.RunBalance.Chart.LoadAsync(runBalanceTimeout.Token), GGD.BackendCharts.SkillBalance.Chart.LoadAsync(runBalanceTimeout.Token), GGD.BackendCharts.DebuffChart.LoadAsync(runBalanceTimeout.Token), GGD.BackendCharts.TotemBalance.Chart.LoadAsync(runBalanceTimeout.Token), GGD.BackendCharts.SelectionCards.Chart.LoadAsync(runBalanceTimeout.Token), GGD.BackendCharts.BossPatterns.Chart.LoadAsync(runBalanceTimeout.Token), GGD.BackendCharts.PenaltyBalance.Chart.LoadAsync(runBalanceTimeout.Token));
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
        _lastPendingSave = -1;
        IsBattleActive = false;
        _paused = false;
        _flush = null;
        _autosaveCts?.Cancel(); _autosaveCts?.Dispose(); _autosaveCts = null;
        Characters.Clear(); LobbyProgress.Clear(); GlobalData.SelectedParty = null;
        OnAccountCleared?.Invoke();
        RoundRecords.Clear();
        GGD.Events.EventChartLoader.Clear();
        UnitBonusChart.Clear();
        GGD.BackendCharts.RunBalance.Chart.Clear();
        GGD.BackendCharts.SkillBalance.Chart.Clear();
        GGD.BackendCharts.DebuffChart.Clear();
        GGD.BackendCharts.TotemBalance.Chart.Clear();
        GGD.BackendCharts.SelectionCards.Chart.Clear();
        GGD.BackendCharts.BossPatterns.Chart.Clear();
        GGD.BackendCharts.PenaltyBalance.Chart.Clear();
        GGD.BackendCharts.OutgameBalance.Chart.Clear();
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
    public static UniTask UpdateDirtyDataAsync()
    {
        if (_flush != null) { _flushRequested = true; return _flush.Task; }
        CancelDebouncedSave();
        var completion = _flush = new UniTaskCompletionSource();
        FlushDirtyDataAsync(completion).Forget();
        return completion.Task;
    }

    private static async UniTaskVoid FlushDirtyDataAsync(UniTaskCompletionSource completion)
    {
        try
        {
            do
            {
                _flushRequested = false;
                var tasks = new System.Collections.Generic.List<UniTask>();

                // Character and Tutorial are aliases of these canonical account documents.
                Global.IClearable[] managers = {
                    PlayerData, Mail, Shop, Daily, RoundRecords, DailyData, WeeklyData, Characters, LobbyProgress
                };

                foreach (var manager in managers)
                    if (manager.IsDirty) tasks.Add(manager.SaveAsync());

                if (tasks.Count > 0)
                {
                    await UniTask.WhenAll(tasks);
                    UnityEngine.Debug.Log($"[Backend DB] 저장 요청 처리. 미확인 변경은 각 매니저에 보관됩니다. 요청 수: {tasks.Count}");
                }
            }
            while (ReferenceEquals(_flush, completion) && _flushRequested);
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
        finally { if (ReferenceEquals(_flush, completion)) _flush = null; }
    }

    private static UniTaskCompletionSource _flush;
    private static bool _flushRequested;
    private static System.Threading.CancellationTokenSource _debounceCts;
    private static CancellationTokenSource _autosaveCts;
    private static double _firstPendingSave = -1;
    private static double _lastPendingSave = -1;
    private static bool _paused;
    private static double _pausedAt;
    /// <summary>Battle deltas stay in memory until settlement; explicit transactions still save immediately.</summary>
    public static bool IsBattleActive { get; private set; }
    private const int DebounceDelayMs = 30000;

    public static void BeginBattle()
    {
        IsBattleActive = true;
        CancelDebouncedSave();
    }

    public static void EndBattle()
    {
        if (!IsBattleActive) return;
        IsBattleActive = false;
        UpdateDirtyDataAsync().Forget();
    }

    /// <summary>Both lobby and battle HUDs may receive the same pause callback.</summary>
    public static void SetApplicationPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        if (paused)
        {
            _pausedAt = UnityEngine.Time.realtimeSinceStartupAsDouble;
            UpdateDirtyDataAsync().Forget();
        }
        else if (UnityEngine.Time.realtimeSinceStartupAsDouble - _pausedAt >= 60)
            PlayerData.ResumeAsync().Forget();
    }

    private static void CancelDebouncedSave()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
        _firstPendingSave = _lastPendingSave = -1;
    }

    /// <summary>
    /// Local journals are immediate. Cloud writes coalesce for 30 seconds, bounded by a 60-second flush.
    /// 유닛 연속 레벨업, 연속 골드 획득 등 잦은 상태 변화가 일어날 때 이 함수를 호출하세요.
    /// </summary>
    public static void RequestDebouncedSave()
    {
        if (IsBattleActive) return;
        _lastPendingSave = UnityEngine.Time.realtimeSinceStartupAsDouble;
        if (_firstPendingSave >= 0) return;
        _firstPendingSave = _lastPendingSave;
        _debounceCts = new System.Threading.CancellationTokenSource();

        DebouncedSaveTask(_debounceCts.Token).Forget();
    }

    private static async UniTaskVoid DebouncedSaveTask(System.Threading.CancellationToken token)
    {
        // One timer per batch. Later changes extend the quiet period without allocating a CTS each time.
        while (true)
        {
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            int delay = (int)Math.Ceiling(Math.Max(0, Math.Min(
                DebounceDelayMs - (now - _lastPendingSave) * 1000,
                60000 - (now - _firstPendingSave) * 1000)));
            if (delay <= 0) break;
            if (await UniTask.Delay(delay, DelayType.Realtime, cancellationToken: token).SuppressCancellationThrow()) return;
        }
        await UpdateDirtyDataAsync();
    }

    private static async UniTaskVoid AutosaveLoopAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(60), DelayType.Realtime, cancellationToken: token);
                try { if (!IsBattleActive && _firstPendingSave < 0) await UpdateDirtyDataAsync(); }
                catch (Exception error) { UnityEngine.Debug.LogWarning("[Save] Pending changes retained: " + error.Message); }
            }
        }
        catch (OperationCanceledException) { }
    }
}
