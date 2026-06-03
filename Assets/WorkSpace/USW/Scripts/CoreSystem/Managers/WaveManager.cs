using UnityEngine;
using VContainer;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// 웨이브 흐름 관리
///
/// 순차 보스 소환 흐름:
///   SpawnWaveBosses()
///     └─ SpawnNextBoss()
///           보스 사망
///             ├─ 다음 보스 있음 → 토템 보상 → SpawnNextBoss()
///             └─ 모두 처치    → OnAllBossesDefeated()
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Inject] private IObjectResolver _resolver;
 
    public void Init()
    {
        if (_gameDataManager == null) _gameDataManager = _resolver.Resolve<GameDataManager>();
        if (_bossManager == null) _bossManager = _resolver.Resolve<BossManager>();
        if (_gridManager == null) _gridManager = _resolver.Resolve<GridManager>();
        if (_currencyManager == null) _currencyManager = _resolver.Resolve<CurrencyManager>();
        if (_gameManager == null) _gameManager = _resolver.Resolve<GameManager>();
        if (_timerManager == null) _timerManager = _resolver.Resolve<TimerController>();
    }

    private GameDataManager _gameDataManager;
    private BossManager _bossManager;
    private GridManager _gridManager;
    private CurrencyManager _currencyManager;
    private GameManager _gameManager;
    private TimerController _timerManager;

    [SerializeField] private StageData stageData;
    [SerializeField] private GameConfig config;

    public event System.Action<int> OnWaveChanged;
    public event System.Action<System.Action> OnTotemSelectionRequested;

    public int CurrentWave { get; private set; } = 0;
    public int TotalWaves  => stageData != null ? stageData.waves.Length : 0;

    private List<BossEntry> _pendingBosses = new List<BossEntry>();
    private int             _bossIndex     = 0;

    // ── 외부 호출 ──────────────────────────────────────────────

    public void StartWave() => SpawnWaveBosses();

    // ── 웨이브 소환 ────────────────────────────────────────────

    private void SpawnWaveBosses()
    {
        if (stageData == null || stageData.waves == null || CurrentWave >= stageData.waves.Length)
        {
            Debug.LogError("[WaveManager] StageData 또는 waves 배열이 올바르지 않습니다.");
            return;
        }

        var wave = stageData.waves[CurrentWave];

        // 유효한 보스만 추림
        _pendingBosses.Clear();
        foreach (var entry in wave.bosses)
            if (entry?.prefab != null) _pendingBosses.Add(entry);

        if (_pendingBosses.Count == 0)
        {
            Debug.LogWarning("[WaveManager] 유효한 보스 없음 — 웨이브 즉시 완료");
            OnAllBossesDefeated();
            return;
        }

        // GameDataManager에 현재 라운드 ID 전달 (Normal 기준: 100 + wave 인덱스)
        _gameDataManager?.SetCurrentBossRound(100 + CurrentWave);

        OnWaveChanged?.Invoke(CurrentWave + 1);

        _bossIndex = 0;
        SpawnNextBoss();
    }

    /// <summary>_bossIndex번째 보스 소환</summary>
    private void SpawnNextBoss()
    {
        SpawnNextBossAsync().Forget();
    }

    private async UniTaskVoid SpawnNextBossAsync()
    {
        var gameConfig = config != null ? config : (_gameManager != null ? _gameManager.Config : null);
        float delay = gameConfig != null ? gameConfig.bossSpawnDelaySeconds : 5f;

        // 보스가 나오기 전까지 타이머를 초기값(예: 30초)으로 설정하되 흐르지 않게 일시정지
        if (gameConfig != null)
        {
            _timerManager?.StartTimer(gameConfig.countdownSeconds);
            _timerManager?.StopTimer();
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());

        var entry = _pendingBosses[_bossIndex];

        _bossManager.SpawnSingleBoss(entry, OnSingleBossDefeated);

        // 보스 스폰 후 일시정지된 타이머(30초부터 시작) 재개
        _timerManager?.ResumeTimer();

        // 유닛 타겟 갱신 — 셀 참조 유지해야 행별 배율/디버프 정상 작동
        var mainBoss = _bossManager.CurrentBoss;
        foreach (var cell in _gridManager.GetOccupiedCells())
        {
            if (cell.OccupyingUnit == null) continue;
            cell.OccupyingUnit.OnRemoved();
            cell.OccupyingUnit.OnPlaced(_currencyManager, mainBoss, cell);
        }
    }

    /// <summary>보스 1마리 처치 시 호출 — 토템 선택 UI 표시 후 흐름 재개</summary>
    private void OnSingleBossDefeated()
    {
        WaitAndShowTotemSelectionAsync().Forget();
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid WaitAndShowTotemSelectionAsync()
    {
        await Cysharp.Threading.Tasks.UniTask.Delay(1000, cancellationToken: this.GetCancellationTokenOnDestroy());
        OnTotemSelectionRequested?.Invoke(OnTotemSelectionDone);
    }

    private void OnTotemSelectionDone()
    {
        ProceedAfterTotemSelectionAsync().Forget();
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid ProceedAfterTotemSelectionAsync()
    {
        // 토템 배치 및 적용 효과 확인하는 유예 시간 5초 지급
        await Cysharp.Threading.Tasks.UniTask.Delay(5000, cancellationToken: this.GetCancellationTokenOnDestroy());

        _bossIndex++;

        if (_bossIndex < _pendingBosses.Count)
            SpawnNextBoss();
        else
            OnAllBossesDefeated();
    }

    /// <summary>웨이브의 모든 보스 처치 완료</summary>
    private void OnAllBossesDefeated()
    {
        CurrentWave++;

        if (CurrentWave >= TotalWaves)
        {
            _gameManager.OnAllWavesCleared();
            return;
        }

        SpawnWaveBosses();
    }
}
