using UnityEngine;
using VContainer;
using System;
using Cysharp.Threading.Tasks;

// ════════════════════════════════════════════════════════
// GameManager — InGameSingleton 교체
// ════════════════════════════════════════════════════════
public class GameManager : MonoBehaviour
{
    [Inject] private IObjectResolver _resolver;
 
    public void Init()
    {
        if (_uiManager == null) _uiManager = _resolver.Resolve<UIManager>();
        if (_waveManager == null) _waveManager = _resolver.Resolve<WaveManager>();
        if (_timerManager == null) _timerManager = _resolver.Resolve<TimerController>();
        if (_currencyManager == null) _currencyManager = _resolver.Resolve<CurrencyManager>();
        if (_expManager == null) _expManager = _resolver.Resolve<ExpManager>();
        if (_gridManager == null) _gridManager = _resolver.Resolve<GridManager>();

        
    }

    private UIManager _uiManager;
    private WaveManager _waveManager;
    private TimerController _timerManager;
    private CurrencyManager _currencyManager;
    private ExpManager _expManager;
    
    private GridManager _gridManager;

    public event Action OnLevelUpStateEntered;

    public enum GameState { Idle, Playing, LevelUp, Win, Lose }
    public GameState CurrentState { get; private set; } = GameState.Idle;

    [SerializeField] private GameConfig config;
    public GameConfig Config => config;

    public void OnStartButtonPressed()
    {
        if (CurrentState != GameState.Idle) return;
        if (config == null) { Debug.LogError("GameManager: config 미연결"); return; }

        CurrentState = GameState.Playing;

        _uiManager.HideStartButton();
        _waveManager.StartWave();

        _timerManager.OnTimeUp += HandleTimeUp;
        BossBase.OnAnyBossDied += HandleBossKilled;

        _currencyManager.AddCurrency(config.startingFood);
        _expManager.OnLevelUp += HandleLevelUp;
    }

    private void HandleLevelUp()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.LevelUp;
        OnLevelUpStateEntered?.Invoke();
    }

    public void OnLevelUpChoiceMade()
    {
        CurrentState = GameState.Playing;
        _expManager.FlushPendingLevelUp();
    }

    public void OnAllWavesCleared()
    {
        if (CurrentState != GameState.Playing) return;
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
        BossBase.OnAnyBossDied -= HandleBossKilled;
        _timerManager.StopTimer();
        StopAllUnits();
        _uiManager.ShowResult(isWin);
    }

    private void StopAllUnits()
    {
        foreach (var cell in _gridManager.GetOccupiedCells())
            cell.OccupyingUnit?.OnRemoved();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // 앱이 백그라운드로 전환될 때 디바운싱 타이머를 무시하고 무조건 즉시 저장 시도
            UnityEngine.Debug.Log("[GameManager] 앱 백그라운드 전환 감지. 데이터 강제 업데이트 진행...");
            //Player.UpdateDirtyDataAsync().Forget();
        }
    }

    private void OnApplicationQuit()
    {
        // 앱이 강제로 종료될 때 최후의 저장 시도
        UnityEngine.Debug.Log("[GameManager] 앱 강제 종료 감지. 데이터 강제 업데이트 진행...");
        //Player.UpdateDirtyDataAsync().Forget();
    }
}
