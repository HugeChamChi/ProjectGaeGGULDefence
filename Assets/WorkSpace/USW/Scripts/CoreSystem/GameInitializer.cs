using VContainer;
using VContainer.Unity;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class GameInitializer : IInitializable, IAsyncStartable
{
    /// <summary>True after scene initialization, preload and the entry fade have finished.</summary>
    public bool IsReady { get; private set; }
    [Inject] private GlobalUIManager _globalUIManager;
    [Inject] private GameDataManager _gameDataManager;
    [Inject] private AudioManager _audioManager;
    [Inject] private ProjectilePool _projectilePool;
    [Inject] private CurrencyManager _currencyManager;
    [Inject] private ExpManager _expManager;
    [Inject] private GridManager _gridManager;
    [Inject] private TotemBuffManager _totemBuffManager;
    [Inject] private BuffManager _buffManager;
    [Inject] private MergeManager _mergeManager;
    [Inject] private LevelUpManager _levelUpManager;
    [Inject] private UpgradeManager _upgradeManager;
    [Inject] private GameManager _gameManager;
    [Inject] private WaveManager _waveManager;
    [Inject] private TimerController _timerController;
    [Inject] private UnitFactory _unitFactory;
    [Inject] private UnitSpawner _unitSpawner;
    [Inject] private TotemSpawner _totemSpawner;
    [Inject] private BossManager _bossManager;
    [Inject] private ChieftainSelection _chieftainSelection;
    [Inject] private UIManager _uIManager;
    [Inject] private DamageFloaterManager _damageFloaterManager;
    [Inject] private CurrencyFloaterManager _currencyFloaterManager;
    [Inject] private ResearchRunBonuses _research;

    public void Initialize()
    {
        Debug.Log("[GameInitializer] 시작");
        if (_gameDataManager != null) _gameDataManager.Init();
        if (_currencyManager != null) _currencyManager.Init();
        if (_expManager != null) _expManager.Init();
        if (_gridManager != null) _gridManager.Init();
        if (_totemBuffManager != null) _totemBuffManager.Init();
        if (_buffManager != null) _buffManager.Init();
        if (_mergeManager != null) _mergeManager.Init();
        if (_levelUpManager != null) _levelUpManager.Init();
        if (_upgradeManager != null) _upgradeManager.Init();
        if (_gameManager != null) _gameManager.Init();
        if (_waveManager != null) _waveManager.Init();
        if (_timerController != null) _timerController.Init();
        if (_totemSpawner != null) _totemSpawner.Init();
        if (_bossManager != null) _bossManager.Init();
        if (_currencyFloaterManager != null) _currencyFloaterManager.Init();
        if (_unitFactory != null) _unitFactory.Init();
        if (_unitSpawner != null) _unitSpawner.Init();
        if (_chieftainSelection != null) _chieftainSelection.Init();
        Debug.Log("=========================================\n[GameInitializer] 모든 VContainer 매니저(Init) 초기화 완벽 성공! 🎉\n=========================================");
    }

    public async Awaitable StartAsync(System.Threading.CancellationToken cancellation)
    {
        // Direct in-game/tutorial entry can skip the title's Table initialization.
        await Table.Projectile.InitializeAsync().AttachExternalCancellation(cancellation);
        await _research.LoadAsync(cancellation);
        // Prepare only the app-owned approved palette; scene cancellation owns this wait.
        if (_audioManager != null) await _audioManager.PreloadPaletteAsync(cancellation);

        // 3. 로딩이 모두 끝나면 페이드아웃 후 게임 진입
        if (_globalUIManager != null)
        {
            await _globalUIManager.FadeOutAsync();
        }
        cancellation.ThrowIfCancellationRequested();
        IsReady = true;
    }
}
