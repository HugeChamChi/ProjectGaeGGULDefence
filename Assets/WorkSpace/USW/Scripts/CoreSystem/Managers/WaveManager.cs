using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;

/// <summary>Owns encounter/reward sequencing; EndlessRunService owns run identity and round transitions.</summary>
public class WaveManager : MonoBehaviour
{
    private enum EncounterPhase { Idle, Spawning, Fighting, RewardDelay, AwaitingReward }
    private const int RewardDelayMilliseconds = 1000;

    [Inject] private IObjectResolver _resolver;
    [Inject] private EndlessRunService _run;

    private GameDataManager _gameDataManager;
    private BossManager _bossManager;
    private GridManager _gridManager;
    private CurrencyManager _currencyManager;
    private GameManager _gameManager;
    private TimerController _timerManager;
    private UIManager _uiManager;
    private ExpManager _expManager;
    private readonly EndlessRunLog _log = new EndlessRunLog();

    [SerializeField] private StageData stageData;
    [SerializeField] private GameConfig config;
    [Tooltip("Optional. Unassigned preserves finite/Tutorial play. Linked drafts fail validation.")]
    [SerializeField] private EndlessModeData _endlessMode;

    private readonly List<BossEntry> _pendingBosses = new List<BossEntry>();
    private CancellationTokenSource _waveCts;
    private EncounterPhase _phase;
    private int _bossIndex;
    private bool _waveStarted;

    /// <summary>One-based round notification shared by finite and endless HUD.</summary>
    public event Action<int> OnWaveChanged;
    /// <summary>Reward request. The captured completion is safe against duplicate/stale invocations.</summary>
    public event Action<Action> OnTotemSelectionRequested;
    /// <summary>Invalidates scene UI selections whenever this run stops, including faults.</summary>
    public event Action OnRunStopped;
    /// <summary>Legacy zero-based index; converted only in SyncWaveIndex.</summary>
    public int CurrentWave { get; private set; }
    /// <summary>Authored finite wave count; zero means no finite endpoint in endless mode.</summary>
    public int TotalWaves => _endlessMode != null ? 0 : (stageData?.waves?.Length ?? 0);
    /// <summary>Selected explicit mode, including invalid drafts that cannot start.</summary>
    public EndlessModeData EndlessMode => _endlessMode;

    /// <summary>Preserves the existing initializer contract.</summary>
    public void Init() { }

    private void Start()
    {
        _gameDataManager = _resolver.Resolve<GameDataManager>();
        _bossManager = _resolver.Resolve<BossManager>();
        _gridManager = _resolver.Resolve<GridManager>();
        _currencyManager = _resolver.Resolve<CurrencyManager>();
        _gameManager = _resolver.Resolve<GameManager>();
        _timerManager = _resolver.Resolve<TimerController>();
        _uiManager = _resolver.Resolve<UIManager>();
        _resolver.TryResolve(out _expManager);
        _run.OnFailed += HandleRunFailed;
    }

    /// <summary>Entry-point selection of a NORMAL/HARD profile before starting; null selects finite play.</summary>
    public bool TryConfigureMode(EndlessModeData mode)
    {
        if (_run.IsActive || (_gameManager != null && _gameManager.CurrentState != GameManager.GameState.Idle)) return false;
        _endlessMode = mode;
        return true;
    }

    /// <summary>Validates and resets run state before GameManager enters Playing, including delayed tutorial waves.</summary>
    public bool TryPrepareRun(out string error)
    {
        if (!_run.TryStart(_endlessMode, out error)) return false;
        CancelFlow();
        _waveCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _waveStarted = false;
        SyncWaveIndex();
        if (_run.IsEndless) _log.BeginRun(_run.RunId, _endlessMode.Key);
        return true;
    }

    /// <summary>Starts the prepared run's first wave once. Tutorials may call this after the first summon.</summary>
    public void StartWave()
    {
        if (_waveStarted || !_run.IsActive) return;
        _waveStarted = true;
        SpawnWaveBosses();
    }

    /// <summary>Cancels pending timers/rewards and invalidates callbacks at every run-end boundary.</summary>
    public void StopRun()
    {
        LogRunEnd(_gameManager != null ? _gameManager.CurrentState.ToString() : "Stopped");
        _run?.Stop();
        CancelFlow();
        OnRunStopped?.Invoke();
    }

    private void SyncWaveIndex() => CurrentWave = _run.CurrentRound - 1;

    private void SpawnWaveBosses()
    {
        if (!_run.IsActive) return;
        SyncWaveIndex();
        _pendingBosses.Clear();
        if (_run.IsEndless)
        {
            _pendingBosses.Add(new BossEntry { Data = _run.BossStats.Source });
        }
        else
        {
            if (stageData?.waves == null || CurrentWave >= stageData.waves.Length)
            { _run.Fail("WaveManager: StageData/waves is missing or out of range."); return; }
            var wave = stageData.waves[CurrentWave];
            if (wave?.bosses != null)
                foreach (var entry in wave.bosses)
                    if (entry?.Prefab != null) _pendingBosses.Add(entry);
            // Legacy sheet lookup is confined to finite stages.
            _gameDataManager?.SetCurrentBossRound(100 + CurrentWave);
        }

        _bossIndex = 0;
        OnWaveChanged?.Invoke(_run.CurrentRound);
        if (!_run.IsActive) return;
        if (_pendingBosses.Count == 0)
        {
            if (_run.TryCompleteRound(_run.RunId, _run.CurrentRound)) FinishRound(_run.RunId, _run.CurrentRound);
            return;
        }
        SpawnNextBoss();
    }

    private void SpawnNextBoss()
    {
        _phase = EncounterPhase.Spawning;
        SpawnNextBossAsync(_run.RunId, _run.CurrentRound, _bossIndex, _waveCts.Token).Forget();
    }

    private async UniTaskVoid SpawnNextBossAsync(long runId, int round, int bossIndex, CancellationToken token)
    {
        var gameConfig = config != null ? config : _gameManager.Config;
        if (gameConfig == null) { _run.Fail("WaveManager: GameConfig is missing."); return; }
        _timerManager?.StartTimer(gameConfig.countdownSeconds);
        _timerManager?.StopTimer();
        float remainingDelay = gameConfig.bossSpawnDelaySeconds;
        while (remainingDelay > 0f)
        {
            _uiManager?.UpdateTimerUI(remainingDelay, true);
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
            remainingDelay -= Time.deltaTime;
        }
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.Spawning)) return;
        _phase = EncounterPhase.Fighting;
        Action defeated = () => OnSingleBossDefeated(runId, round, bossIndex);
        if (_run.IsEndless)
        {
            if (!_bossManager.TrySpawnSingleBoss(_run.BossStats, defeated, out string error))
            { _run.Fail(error); return; }
            _log.BossSpawned(round, _run.BossStats);
        }
        else _bossManager.SpawnSingleBoss(_pendingBosses[bossIndex], defeated);

        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.Fighting)) return;
        _timerManager?.ResumeTimer();
        var mainBoss = _bossManager.CurrentBoss;
        foreach (var cell in _gridManager.GetOccupiedCells())
        {
            if (!_run.Matches(runId, round)) return;
            if (cell.OccupyingUnit == null) continue;
            cell.OccupyingUnit.OnRemoved();
            cell.OccupyingUnit.OnPlaced(_currencyManager, mainBoss, cell);
        }
    }

    private void OnSingleBossDefeated(long runId, int round, int bossIndex)
    {
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.Fighting)) return;
        _phase = EncounterPhase.RewardDelay;
        if (_run.IsEndless) _log.BossDefeated(round, CurrentLevel, _run.AttackMultiplier, _run.AttackFrequencyMultiplier);
        // Reserve at combat completion, activate after the reward when entering the next round.
        if (bossIndex == _pendingBosses.Count - 1 && !_run.TryCompleteRound(runId, round)) return;
        WaitAndShowRewardAsync(runId, round, bossIndex, _waveCts.Token).Forget();
    }

    private async UniTaskVoid WaitAndShowRewardAsync(long runId, int round, int bossIndex, CancellationToken token)
    {
        if (await UniTask.Delay(RewardDelayMilliseconds, cancellationToken: token).SuppressCancellationThrow()) return;
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.RewardDelay)) return;
        _phase = EncounterPhase.AwaitingReward;
        if (OnTotemSelectionRequested == null)
        { _run.Fail("WaveManager: no totem reward UI is connected."); return; }
        OnTotemSelectionRequested.Invoke(() => OnRewardDone(runId, round, bossIndex));
    }

    private void OnRewardDone(long runId, int round, int bossIndex)
    {
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.AwaitingReward)) return;
        _phase = EncounterPhase.Idle;
        _bossIndex++;
        if (_bossIndex < _pendingBosses.Count) SpawnNextBoss();
        else FinishRound(runId, round);
    }

    private void FinishRound(long runId, int round)
    {
        if (!_run.Matches(runId, round)) return;
        if (!_run.IsEndless && round >= TotalWaves)
        {
            StopRun();
            _gameManager.OnAllWavesCleared();
            return;
        }
        if (_run.TryAdvanceRound(runId, round)) SpawnWaveBosses();
    }

    private bool OwnsEncounter(long runId, int round, int bossIndex, EncounterPhase phase)
        => _run.Matches(runId, round) && _bossIndex == bossIndex && _phase == phase;

    private void HandleRunFailed(string error)
    {
        LogRunEnd("Faulted: " + error);
        CancelFlow();
    }

    private int CurrentLevel => _expManager != null ? _expManager.CurrentLevel : 0;

    // Tuning log only; records before EndlessRunService.Stop resets run modifiers.
    private void LogRunEnd(string result)
        => _log.EndRun(result, CurrentLevel, _run?.AttackMultiplier ?? 1d, _run?.AttackFrequencyMultiplier ?? 1d);

    private void CancelFlow()
    {
        var cts = _waveCts;
        _waveCts = null;
        if (cts != null) { cts.Cancel(); cts.Dispose(); }
        _phase = EncounterPhase.Idle;
    }

    private void OnDestroy()
    {
        if (_run != null) _run.OnFailed -= HandleRunFailed;
        StopRun();
    }
}
