using UnityEngine;
using VContainer;
using System;
using Cysharp.Threading.Tasks;

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
    public void StartGame(bool startWave)
    {
        if (CurrentState != GameState.Idle) return;
        if (config == null) { Debug.LogError("GameManager: config 미연결"); return; }
        if (!_waveManager.TryPrepareRun(out string error))
        {
            Debug.LogError($"[GameManager] Cannot start run: {error}");
            OnRunFailed?.Invoke(error);
            return;
        }

        CurrentState = GameState.Playing;

        _uiManager.HideStartButton();

        _timerManager.OnTimeUp += HandleTimeUp;
        BossBase.OnAnyBossDied += HandleBossKilled;

        _currencyManager.AddCurrency(config.startingFood);
        _expManager.OnLevelUp += HandleLevelUp;

        if (startWave) _waveManager.StartWave();
        if (CurrentState == GameState.Faulted) return;
        OnGameStart?.Invoke();
    }

    private void HandleLevelUp()
    {
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
        // 타이머 시작/리셋은 WaveManager가 SpawnNextBossAsync에서 수행합니다.
    }
    private void HandleTimeUp()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Lose;
        EndGame(false);
    }

    private void EndGame(bool isWin)
    {
        _waveManager.StopRun();
        UnsubscribeGameplay();
        _timerManager.StopTimer();
        StopAllUnits();
        _uiManager.ShowResult(isWin);
    }

    private void HandleRunFailed(string error)
    {
        CurrentState = GameState.Faulted;
        _waveManager.StopRun();
        UnsubscribeGameplay();
        _timerManager?.StopTimer();
        StopAllUnits();
        _timeScale.Pause(this);
        Debug.LogError($"[GameManager] Run stopped: {error}");
        OnRunFailed?.Invoke(error);
    }

    private void UnsubscribeGameplay()
    {
        BossBase.OnAnyBossDied -= HandleBossKilled;
        if (_timerManager != null) _timerManager.OnTimeUp -= HandleTimeUp;
        if (_expManager != null) _expManager.OnLevelUp -= HandleLevelUp;
    }

    private void OnDestroy()
    {
        if (_run != null) { _run.OnFailed -= HandleRunFailed; _run.Stop(); }
        UnsubscribeGameplay();
        _timeScale?.Release(this);
    }

    private void StopAllUnits()
    {
        if (_gridManager == null) return;
        foreach (var cell in _gridManager.GetOccupiedCells())
            cell.OccupyingUnit?.OnRemoved();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // 앱이 백그라운드로 전환될 때 디바운싱 타이머를 무시하고 무조건 즉시 저장 시도
            UnityEngine.Debug.Log("[GameManager] 앱 백그라운드 전환 감지. 데이터 강제 업데이트 진행...");
            Player.UpdateDirtyDataAsync().Forget();
        }
    }

    private void OnApplicationQuit()
    {
        // 앱이 강제로 종료될 때 최후의 저장 시도
        UnityEngine.Debug.Log("[GameManager] 앱 강제 종료 감지. 데이터 강제 업데이트 진행...");
        Player.UpdateDirtyDataAsync().Forget();
    }
}
