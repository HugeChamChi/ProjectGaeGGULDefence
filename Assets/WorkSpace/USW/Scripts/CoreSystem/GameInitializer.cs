using VContainer;
using VContainer.Unity;
using UnityEngine;

public class GameInitializer : IInitializable, IAsyncStartable
{
    [Inject] private GlobalUIManager _globalUIManager;
    [Inject] private GameDataManager _gameDataManager;
    [Inject] private AudioManager _audioManager;
    [Inject] private ProjectilePool _projectilePool;
    [Inject] private CurrencyManager _currencyManager;
    [Inject] private ExpManager _expManager;
    [Inject] private GridManager _gridManager;
    [Inject] private PopulationManager _populationManager;
    [Inject] private TotemBuffManager _totemBuffManager;
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
    [Inject] private ChieftainSpawner _chieftainSpawner;
    [Inject] private UIManager _uIManager;
    [Inject] private DamageFloaterManager _damageFloaterManager;
    [Inject] private CurrencyFloaterManager _currencyFloaterManager;

    public void Initialize()
    {
        Debug.Log("[GameInitializer] 시작");
        if (_gameDataManager != null) _gameDataManager.Init();
        if (_currencyManager != null) _currencyManager.Init();
        if (_expManager != null) _expManager.Init();
        if (_gridManager != null) _gridManager.Init();
        if (_populationManager != null) _populationManager.Init();
        if (_totemBuffManager != null) _totemBuffManager.Init();
        if (_mergeManager != null) _mergeManager.Init();
        if (_levelUpManager != null) _levelUpManager.Init();
        if (_upgradeManager != null) _upgradeManager.Init();
        if (_gameManager != null) _gameManager.Init();
        if (_waveManager != null) _waveManager.Init();
        if (_timerController != null) _timerController.Init();
        if (_totemSpawner != null) _totemSpawner.Init();
        if (_bossManager != null) _bossManager.Init();
        if (_currencyFloaterManager != null) _currencyFloaterManager.Init();
        if (_unitSpawner != null) _unitSpawner.Init();
        if (_chieftainSpawner != null) _chieftainSpawner.Init();
        Debug.Log("=========================================\n[GameInitializer] 모든 VContainer 매니저(Init) 초기화 완벽 성공! 🎉\n=========================================");
    }

    public async System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellation)
    {
        if (_globalUIManager != null)
        {
            await _globalUIManager.FadeOutAsync();
        }
    }
}
