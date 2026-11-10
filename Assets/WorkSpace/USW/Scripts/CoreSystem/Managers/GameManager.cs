using UnityEngine;
using VContainer;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;

// ════════════════════════════════════════════════════════
// GameManager — InGameSingleton 교체
// ════════════════════════════════════════════════════════
public class GameManager : MonoBehaviour
{
 
    public void Init()
    {

        
    }

    [Inject] private IObjectResolver _resolver;
    [Inject] private EndlessRunService _run;
    [Inject] private TimeScaleService _timeScale;
    [Inject] private DefeatPresentation _defeat;
    [Inject] private ResearchRunBonuses _research;
    [Inject] private AccountExperienceRun _accountExperience;
    private CancellationTokenSource _endingCts;
    private readonly object _applicationPauseOwner = new object();
    private int _applicationPauseVersion;
    private ResultScreenData _resultSnapshot;
    private float _survivalSeconds;
    private int _bossKills;
    private string _recordAccount;
    private string _recordMode;
    private string _eventRunKey;
    private bool _savingStart;
    /// <summary>Committed terminal state; late combat, reward and production callbacks must not mutate a run.</summary>
    public bool IsFinished => CurrentState == GameState.Win || CurrentState == GameState.Lose || CurrentState == GameState.Faulted;
    /// <summary>마지막 유예 동안 딜표와 설정 외 플레이어 조작을 차단하는 공통 상태.</summary>
    public bool IsTimeVaultActive => _timerManager != null && _timerManager.TimeVaultActive;
    /// <summary>Snapshot captured before StopRun resets progression values.</summary>
    public ResultScreenData ResultSnapshot => _resultSnapshot;
    /// <summary>True only for direct Editor preview play; always false in Player builds.</summary>
    public bool IsEditorPreview
    {
        get
        {
#if UNITY_EDITOR
            return _isEditorPreview;
#else
            return false;
#endif
        }
    }
#if UNITY_EDITOR
    private bool _isEditorPreview;
    /// <summary>Marks a development-only run before gameplay starts.</summary>
    public void EnableEditorPreview() => _isEditorPreview = true;
#endif
    /// <summary>Last preparation or run failure, suitable for an input rejection notice.</summary>
    public string FailureReason { get; private set; }
    private bool IsProductionBattle => !IsEditorPreview &&
        (gameObject.scene.name == "IngameScene" || gameObject.scene.name == "HardIngameScene");

    private UIManager _uiManager;
    private WaveManager _waveManager;
    private TimerController _timerManager;
    private CurrencyManager _currencyManager;
    private ExpManager _expManager;
    private GridManager _gridManager;

    private void Start()
    {
        _uiManager = _resolver.Resolve<UIManager>();
        _waveManager = _resolver.Resolve<WaveManager>();
        _timerManager = _resolver.Resolve<TimerController>();
        _currencyManager = _resolver.Resolve<CurrencyManager>();
        _expManager = _resolver.Resolve<ExpManager>();
        _gridManager = _resolver.Resolve<GridManager>();
        _run.OnFailed += HandleRunFailed;
        _waveManager.OnBossDefeated += HandleBossKilled;
        _waveManager.OnRunStopped += _accountExperience.Cancel;
    }

    public event Action OnLevelUpStateEntered;
    public event Action OnGameStart;

    public enum GameState { Idle, Playing, LevelUp, Win, Lose, Faulted }
    /// <summary>Configuration/numeric faults are separate from gameplay win/lose results.</summary>
    public event Action<string> OnRunFailed;
    public GameState CurrentState { get; private set; } = GameState.Idle;

    [SerializeField] private GameConfig config;
    public GameConfig Config => config;

    [Tooltip("체크 시 레벨업 상태로 전환하지 않고 Playing 상태를 유지합니다 (스킬 테스트 씬 등에서 사용).")]
    [SerializeField] private bool disableLevelUp = false;

    public void OnStartButtonPressed()
    {
        StartGame(true);
    }

    /// <summary>Starts gameplay; tutorial scenes can reveal the first wave after the first summon.</summary>
    public void StartGame(bool startWave) => StartGameAsync(startWave).Forget();

    private async UniTaskVoid StartGameAsync(bool startWave)
    {
        if (CurrentState != GameState.Idle) return;
        if (_research?.IsReady != true) return;
        if (config == null) { Debug.LogError("GameManager: config 미연결"); return; }
        if (IsProductionBattle && (Player.PlayerData?.IsReady != true
            || string.IsNullOrEmpty(Player.PlayerData.Data.PendingEntryId)))
        {
            FailPreparation("스태미나 출격 예약이 없습니다. 로비에서 다시 출격해 주세요.");
            return;
        }
        if (!_waveManager.TryPrepareRun(out string error))
        {
            Debug.LogError($"[GameManager] Cannot start run: {error}");
            FailPreparation(error);
            return;
        }

        CurrentState = GameState.Playing;
        _eventRunKey = IsProductionBattle ? Player.PlayerData.Data.PendingEntryId : null;
        if (IsProductionBattle) Player.PlayerData.ConsumeEntryReservation();
        // Only production endless battles contribute. Capture ownership before callbacks can outlive login.
        _recordAccount = IsProductionBattle && Player.RoundRecords.OwnsAccount ? Player.RoundRecords.Account : null;
        _recordMode = IsProductionBattle ? "NORMAL" : null;
        _accountExperience.Begin(IsProductionBattle);

        if (IsProductionBattle)
        {
            _savingStart = true;
            string runId = Player.PlayerData.Data.AccountExperienceRunId;
            Player.BeginBattle();
            try { await Player.UpdateDirtyDataAsync(); }
            catch (Exception saveError) { Debug.LogException(saveError); }
            if (this == null) return;
            _savingStart = false;
            if (!Player.PlayerData.IsReady || Player.PlayerData.IsDirty
                || Player.PlayerData.Data.AccountExperienceRunId != runId)
            {
                FailPreparation("출격 시작 상태를 저장하지 못했습니다. 연결 확인 후 다시 시도해 주세요.");
                return;
            }
            if (CurrentState != GameState.Playing) return;
        }

        _uiManager.HideStartButton();

        _timerManager.OnTimeUp += HandleTimeUp;

        _currencyManager.AddCurrency(config.startingFood + _research.Get(ResearchStat.StartFood));
        _expManager.OnLevelUp += HandleLevelUp;

        if (startWave) _waveManager.StartWave();
        if (CurrentState == GameState.Faulted) return;
        OnGameStart?.Invoke();
    }

    private void HandleLevelUp()
    {
        if (IsTimeVaultActive) return;
        if (CurrentState != GameState.Playing) return;
        if (disableLevelUp) return; // 레벨은 오르되 선택 UI는 띄우지 않고 Playing 상태를 유지한다.

        CurrentState = GameState.LevelUp;
        OnLevelUpStateEntered?.Invoke();
    }

    public void OnLevelUpChoiceMade()
    {
        if (CurrentState != GameState.LevelUp) return;
        CurrentState = GameState.Playing;
        _expManager.FlushPendingLevelUp();
    }

    public void OnAllWavesCleared()
    {
        if (CurrentState != GameState.Playing && CurrentState != GameState.LevelUp) return;
        CurrentState = GameState.Win;
        EndGame(true);
    }

    private void HandleBossKilled()
    {
        if (!IsFinished) _bossKills++;
    }
    private void HandleTimeUp()
    {
        if (CurrentState != GameState.Playing && CurrentState != GameState.LevelUp) return;
        CurrentState = GameState.Lose;
        EndGame(false);
    }

    private void EndGame(bool isWin)
    {
        // Terminal state is already committed. Drain earned rewards before result/owner cleanup;
        // settlement updates EXP/level only and cannot reopen a choice or restart combat.
        if (_resolver.TryResolve<ExpEffectController>(out var expEffects))
            _expManager?.SettlePendingExp(expEffects.TakePendingExp());
        _timerManager?.CancelTimeVault();
        if (_resolver.TryResolve<DamageContributionLedger>(out var damageLedger)) damageLedger.FinalizeRun();
        _resultSnapshot = CaptureResult();
        if (IsProductionBattle && _recordAccount == Player.RoundRecords.Account)
        {
            try
            {
                GGD.Events.EventDataBridge.BattleCompleted(_recordAccount, _eventRunKey,
                    gameObject.scene.name == "HardIngameScene", _resultSnapshot.SurvivalSeconds, _resultSnapshot.Round);
            }
            catch (Exception error) { Debug.LogException(error); }
        }
        var accountReceipt = _accountExperience.Finish(isWin);
        _resultSnapshot.AccountExperience = accountReceipt?.Experience ?? 0;
        _resultSnapshot.HasAccountExperience = accountReceipt != null;
        if (IsProductionBattle)
        {
            Player.RoundRecords.Record(_recordAccount, _recordMode, _resultSnapshot.Round);
            // Stage every result before one dirty flush; account progression and missions share one row.
            Player.EndBattle();
        }
        _uiManager?.BlockGameplayInput();
        _waveManager.StopRun();
        UnsubscribeGameplay();
        _timerManager.StopTimer();
        if (isWin || _defeat == null || config == null) { FinishResult(isWin); return; }
        _endingCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        PresentDefeatAsync(_endingCts).Forget();
    }

    private async UniTaskVoid PresentDefeatAsync(CancellationTokenSource cts)
    {
        try { await _defeat.PlayAsync(config, () => FinishResult(false), cts.Token); }
        catch (OperationCanceledException) { }
        finally
        {
            if (_endingCts == cts) _endingCts = null;
            cts.Dispose();
        }
    }

    private void FinishResult(bool isWin)
    {
        // 패배면 결과 화면이 그 보스를 이어받도록 마지막 모습을 찍어 둔다.
        if (!isWin && _resultSnapshot != null && _resolver.TryResolve<BossManager>(out var bosses))
            _resultSnapshot.BossSnapshot = ResultBossSnapshot.Capture(bosses.CurrentBoss, Camera.main);
        // Pause first, then clean up owners/units. No normal-speed frame during the handoff.
        _uiManager.ShowResult(isWin, _resultSnapshot);
        StopAllUnits();
    }

    private ResultScreenData CaptureResult()
    {
        _resolver.TryResolve<BossManager>(out var bosses);
        _resolver.TryResolve<LevelUpManager>(out var choices);
        var boss = bosses?.CurrentBoss;
        int round = Mathf.Max(1, _waveManager.CurrentWave + 1);
        int? previousBest = IsProductionBattle && _recordAccount == Player.RoundRecords.Account
            ? Player.RoundRecords.GetBest(_recordMode) : null;
        return new ResultScreenData
        {
            Round = round, BestRound = previousBest ?? round, BossKills = _bossKills, SurvivalSeconds = _survivalSeconds,
            LastBossDamageRatio = boss != null && boss.MaxHp > 0 ? (float)(1m - boss.CurrentHp / boss.MaxHp) : -1f,
            BuildChoices = choices?.GetResultChoices(), Rewards = Array.Empty<ResultReward>(),
            BossProgressStyle = ResultBossProgressStyle.HpSettlement
        };
    }

    /// <summary>Scene navigation cancels the pending result before releasing time owners.</summary>
    public void CancelEndPresentation()
    {
        _endingCts?.Cancel();
        if (!IsFinished) _accountExperience?.Cancel();
    }
    private void OnDisable() => CancelEndPresentation();
    private void Update()
    {
        if (!_savingStart && (CurrentState == GameState.Playing || CurrentState == GameState.LevelUp)) _survivalSeconds += Time.deltaTime;
    }

    /// <summary>Stops a failed startup without leaving the scene in the waiting-to-start state.</summary>
    public void FailPreparation(string error) => HandleRunFailed(error);

    private void HandleRunFailed(string error)
    {
        if (IsFinished) return;
        FailureReason = error;
        CurrentState = GameState.Faulted;
        _accountExperience?.Cancel();
        if (IsProductionBattle) Player.EndBattle();
        _waveManager?.StopRun();
        UnsubscribeGameplay();
        _timerManager?.StopTimer();
        StopAllUnits();
        _timeScale?.Pause(this);
        Debug.LogError($"[GameManager] Run stopped: {error}");
        OnRunFailed?.Invoke(error);
    }

    private void UnsubscribeGameplay()
    {
        if (_timerManager != null) _timerManager.OnTimeUp -= HandleTimeUp;
        if (_expManager != null) _expManager.OnLevelUp -= HandleLevelUp;
    }

    private void OnDestroy()
    {
        _accountExperience?.Cancel();
        if (IsProductionBattle) Player.EndBattle();
        if (_run != null) { _run.OnFailed -= HandleRunFailed; _run.Stop(); }
        if (_waveManager != null) _waveManager.OnBossDefeated -= HandleBossKilled;
        if (_waveManager != null) _waveManager.OnRunStopped -= _accountExperience.Cancel;
        CancelEndPresentation();
        UnsubscribeGameplay();
        _timeScale?.Release(this);
        _timeScale?.Release(_applicationPauseOwner);
    }

    private void StopAllUnits()
    {
        if (_gridManager == null) return;
        foreach (var cell in _gridManager.GetOccupiedCells())
            cell.OccupyingUnit?.OnRemoved();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        int version = ++_applicationPauseVersion;
        if (pauseStatus) _timeScale?.Pause(_applicationPauseOwner);
        else ResumeCombatAfterPauseAsync(version).Forget();
        if (IsEditorPreview) return;
        Player.SetApplicationPaused(pauseStatus);
    }

    private async UniTask ResumeCombatAfterPauseAsync(int version)
    {
        // Keep every scaled combat clock paused through the first resumed frame.
        await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy());
        if (version == _applicationPauseVersion) _timeScale?.Release(_applicationPauseOwner);
    }

    private void OnApplicationQuit()
    {
        if (IsEditorPreview) return;
        // Best effort only; force-kill cannot guarantee delivery.
        Player.UpdateDirtyDataAsync().Forget();
    }
}
