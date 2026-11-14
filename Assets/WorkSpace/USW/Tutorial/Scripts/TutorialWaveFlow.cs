using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;

/// <summary>Scene-owned tutorial boss, reward and finite completion sequence.</summary>
public sealed class TutorialWaveFlow : IDisposable
{
    private enum EncounterPhase { Idle, Spawning, Fighting, RewardDelay, AwaitingReward }
    private const int RewardDelayMilliseconds = 1000;
    private readonly EndlessRunService _run;
    private readonly FieldPauseVisuals _fieldPause;
    private readonly BossManager _bossManager;
    private readonly GridManager _gridManager;
    private readonly CurrencyManager _currencyManager;
    private readonly GameManager _gameManager;
    private readonly TimerController _timerManager;
    private readonly UIManager _uiManager;
    private readonly ExpManager _expManager;
    private readonly DroneManager _droneManager;
    private readonly List<BossEntry> _pendingBosses = new List<BossEntry>();
    private StageData _stageData;
    private GameConfig _config;
    private BossClearPresentation _clearPresentation;
    private CancellationToken _destroyToken;
    private CancellationTokenSource _waveCts;
    private EncounterPhase _phase;
    private int _bossIndex;
    private bool _waveStarted;
    private bool _timerStarted;

    /// <summary>Constructs the tutorial flow from scene-scoped dependencies.</summary>
    public TutorialWaveFlow(EndlessRunService run, FieldPauseVisuals fieldPause, IObjectResolver resolver)
    {
        _run = run;
        _fieldPause = fieldPause;
        _bossManager = resolver.Resolve<BossManager>();
        _gridManager = resolver.Resolve<GridManager>();
        _currencyManager = resolver.Resolve<CurrencyManager>();
        _gameManager = resolver.Resolve<GameManager>();
        _timerManager = resolver.Resolve<TimerController>();
        _uiManager = resolver.Resolve<UIManager>();
        resolver.TryResolve(out _expManager);
        resolver.TryResolve(out _droneManager);
    }

    /// <summary>One-based tutorial wave notification.</summary>
    public event Action<int> OnWaveChanged;
    /// <summary>Accepted boss defeat before the result snapshot.</summary>
    public event Action OnBossDefeated;
    /// <summary>Configures lesson gates before any placed unit starts fighting the new boss.</summary>
    public event Action<BossBase, int> OnBossReady;
    /// <summary>Reward request with a stale/duplicate-safe completion.</summary>
    public event Action<Action> OnTotemSelectionRequested;
    /// <summary>Zero-based tutorial wave for existing HUD and lesson bindings.</summary>
    public int CurrentWave { get; private set; }
    /// <summary>Number of authored tutorial waves.</summary>
    public int TotalWaves => _stageData?.waves?.Length ?? 0;

    /// <summary>Preserves existing scene references without rewriting scenes or assets.</summary>
    public void Configure(StageData stage, GameConfig gameConfig, BossClearPresentation presentation, CancellationToken destroyToken)
    {
        _stageData = stage;
        _config = gameConfig;
        _clearPresentation = presentation;
        _destroyToken = destroyToken;
    }

    /// <summary>Prepares finite play without remote balance, penalties or endless boss selection.</summary>
    public bool TryPrepareRun(out string error)
    {
        if (TotalWaves == 0)
        { error = "TutorialWaveFlow: tutorial stage has no waves."; return false; }
        if (!_run.TryStart(null, out error)) return false;
        CancelFlow();
        _waveCts = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);
        foreach (var cell in _gridManager.AllCells()) cell.Model.SetPermanentSeal(false);
        CurrentWave = 0;
        _waveStarted = false;
        _timerStarted = false;
        return true;
    }

    /// <summary>Starts once after the tutorial's first summon gate.</summary>
    public void StartWave()
    {
        if (_waveStarted || !_run.IsActive) return;
        _waveStarted = true;
        SpawnWaveBosses();
    }

    /// <summary>Checks the authored finite reward schedule.</summary>
    public bool GrantsTotemReward(int round, int bossIndex)
    {
        var gameConfig = _config != null ? _config : _gameManager.Config;
        return round >= 1 && bossIndex == _pendingBosses.Count - 1
            && gameConfig != null && gameConfig.GrantsFiniteTotemReward(round);
    }

    private void OnRewardDone(long runId, int round, int bossIndex)
    {
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.AwaitingReward)) return;
        ContinueAfterReward(runId, round);
    }

    private bool OwnsEncounter(long runId, int round, int bossIndex, EncounterPhase phase)
        => _run.Matches(runId, round) && !_run.IsEndless && _bossIndex == bossIndex && _phase == phase;

    /// <summary>Invalidates pending tutorial delays and reward callbacks.</summary>
    public void CancelFlow()
    {
        _fieldPause.Exit(this, false);
        var cts = _waveCts;
        _waveCts = null;
        if (cts != null) { cts.Cancel(); cts.Dispose(); }
        _phase = EncounterPhase.Idle;
    }

    /// <summary>Releases scene-owned asynchronous work.</summary>
    public void Dispose() => CancelFlow();

    private void SpawnWaveBosses()
    {
        if (!_run.IsActive) return;
        CurrentWave = _run.CurrentRound - 1;
        _pendingBosses.Clear();
        if (_stageData?.waves == null || CurrentWave >= _stageData.waves.Length)
        { _run.Fail("TutorialWaveFlow: StageData/waves is missing or out of range."); return; }
        var wave = _stageData.waves[CurrentWave];
        if (wave?.bosses != null)
            foreach (var entry in wave.bosses)
                if (entry?.Prefab != null) _pendingBosses.Add(entry);

        _bossIndex = 0;
        OnWaveChanged?.Invoke(_run.CurrentRound);
        if (!_run.IsActive) return;
        if (_pendingBosses.Count == 0)
        {
            if (TryCompleteRound(_run.RunId, _run.CurrentRound)) FinishRound(_run.RunId, _run.CurrentRound);
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
        var gameConfig = _config != null ? _config : _gameManager.Config;
        if (gameConfig == null) { _run.Fail("TutorialWaveFlow: GameConfig is missing."); return; }
        if (!_timerStarted)
        {
            _timerManager?.StartTimer(gameConfig.countdownSeconds);
            _timerStarted = true;
        }
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
        BossBase encounterBoss = null;
        Action defeated = () => OnSingleBossDefeated(runId, round, bossIndex,
            encounterBoss != null ? (Vector3?)encounterBoss.transform.position : null);
        _bossManager.SpawnSingleBoss(_pendingBosses[bossIndex], defeated);

        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.Fighting)) return;
        _timerManager?.ResumeTimer();
        var mainBoss = _bossManager.CurrentBoss;
        encounterBoss = mainBoss;
        OnBossReady?.Invoke(mainBoss, CurrentWave);
        foreach (var cell in _gridManager.GetOccupiedCells())
        {
            if (!_run.Matches(runId, round)) return;
            // The summon continuation owns the first OnPlaced after its arrival effect.
            if (cell.OccupyingUnit == null || !cell.OccupyingUnit.gameObject.activeInHierarchy) continue;
            cell.OccupyingUnit.OnRemoved();
            cell.OccupyingUnit.OnPlaced(_currencyManager, mainBoss, cell);
        }
    }

    private void OnSingleBossDefeated(long runId, int round, int bossIndex, Vector3? bossPosition)
    {
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.Fighting)) return;
        _phase = EncounterPhase.RewardDelay;
        _timerManager?.StopTimer();
        var gameConfig = _config != null ? _config : _gameManager.Config;

        if (gameConfig == null)
        { _run.Fail("TutorialWaveFlow: GameConfig is missing at boss defeat."); return; }
        float bonusSeconds;
        if (!gameConfig.TryGetBossKillBonusSeconds(null, round,
            out _, out bonusSeconds, out string rewardError))
        { _run.Fail("TutorialWaveFlow: " + rewardError); return; }

        OnBossDefeated?.Invoke();
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.RewardDelay)) return;
        // Accept completion once before settling the final boss's reward.
        if (bossIndex == _pendingBosses.Count - 1 && !TryCompleteRound(runId, round)) return;
        WaitAndShowRewardAsync(runId, round, bossIndex, bossPosition, gameConfig, bonusSeconds, _waveCts.Token).Forget();
    }

    private async UniTaskVoid WaitAndShowRewardAsync(long runId, int round, int bossIndex, Vector3? bossPosition, GameConfig gameConfig, float bonusSeconds, CancellationToken token)
    {
        try
        {
            // Finish an already open choice; don't cover it with the clear presentation.
            while (_gameManager.CurrentState == GameManager.GameState.LevelUp || _fieldPause.AttacksHeld)
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.RewardDelay)) return;
            _fieldPause.HoldAttacks(this);
            try
            {
                _timerManager?.ResetBossKillTime(gameConfig.countdownSeconds);
            }
            finally { _fieldPause.Exit(this, _run.IsActive && !_gameManager.IsFinished); }
            if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.RewardDelay)) return;
            _expManager?.FlushPendingLevelUp();
            while (_gameManager.CurrentState == GameManager.GameState.LevelUp || _fieldPause.AttacksHeld)
                await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        catch (OperationCanceledException) { return; }
        if (await UniTask.Delay(RewardDelayMilliseconds, cancellationToken: token).SuppressCancellationThrow()) return;
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.RewardDelay)) return;
        while (_gameManager.CurrentState == GameManager.GameState.LevelUp || _fieldPause.AttacksHeld)
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
        if (!OwnsEncounter(runId, round, bossIndex, EncounterPhase.RewardDelay)) return;
        _phase = EncounterPhase.AwaitingReward;
        if (!GrantsTotemReward(round, bossIndex))
        {
            OnRewardDone(runId, round, bossIndex);
            return;
        }
        if (OnTotemSelectionRequested == null)
        { _run.Fail("TutorialWaveFlow: no totem reward UI is connected."); return; }
        OnTotemSelectionRequested.Invoke(() => OnRewardDone(runId, round, bossIndex));
    }

    private void ContinueAfterReward(long runId, int round)
    {
        _phase = EncounterPhase.Idle;
        _bossIndex++;
        if (_bossIndex < _pendingBosses.Count) SpawnNextBoss();
        else FinishRound(runId, round);
    }

    private bool TryCompleteRound(long runId, int round)
    {
        if (!_run.TryCompleteRound(runId, round)) return false;
        _droneManager?.SettleWaveFoodDividend(runId, round);
        return true;
    }

    private void FinishRound(long runId, int round)
    {
        if (!_run.Matches(runId, round)) return;
        if (round >= TotalWaves)
        {
            _run.Stop();
            CancelFlow();
            _gameManager.OnAllWavesCleared();
            return;
        }
        if (_run.TryAdvanceRound(runId, round))
        {
            SpawnWaveBosses();
        }
    }
}
