using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;

/// <summary>
/// 인게임 씬 배선 담당. 로직 없음 — UI 이벤트와 시스템 메서드를 연결하는 역할만.
/// </summary>
public class InGameInstaller : MonoBehaviour
{
    [Inject] private GameManager _gameManager;
    [Inject] private WaveManager _waveManager;
    [Inject] private BossManager _bossManager;
    [Inject] private MergeManager _mergeManager;
    [Inject] private UnitSpawner _spawnerManager;
    [Inject] private GridManager _gridManager;
    [Inject] private TotemSpawner _totemManager;

    [Header("Unit Action Popup")]
    [SerializeField] private GaeGGUL.UI.Unit.UI_UnitInfoPanel _unitInfoPanel;

    [Header("Totem Action Popup")]
    [SerializeField] private TotemActionPopupUI _totemActionPopup;
    [SerializeField] private SellTotemButtonUI _sellTotemButton;
    [SerializeField] private UI_TotemInfoPanel _totemInfoPanel;

    [Header("Boss Encounter")]
    [SerializeField] private UI_BossEncounter _bossEncounterUI;

    [Header("Wave Info")]
    [SerializeField] private UI_WaveText _waveTextUI;


    // 임시
    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        WireUnitActionPopup();
        WireTotemActionPopup();
        WireBossEncounter();
        WireWaveUI();
        WireLevelUpUI();
        WireTotemSelectUI();
    }

    
    // ── UI Decoupling ──────────────────────────────────────────

    [Header("Level Up & Totem UI")]
    [SerializeField] private LevelUpUI _levelUpUI;
    [SerializeField] private TotemSelectUI _totemSelectUI;
    [Inject] private LevelUpManager _levelUpManager;

    private void WireLevelUpUI()
    {
        if (_levelUpUI == null) return;
        if (_gameManager == null) return;
        _gameManager.OnLevelUpStateEntered += _levelUpUI.Show;
    }

    private void WireTotemSelectUI()
    {
        if (_totemSelectUI == null) return;
        if (_waveManager != null) _waveManager.OnTotemSelectionRequested += cb => _totemSelectUI.Show(cb);
        if (_levelUpManager != null) _levelUpManager.OnTotemSelectionRequested += cb => _totemSelectUI.Show(cb);
    }

    // ── Wave UI ────────────────────────────────────────────────

    private void WireWaveUI()
    {
        if (_waveTextUI == null) return;
        if (_waveManager == null) return;

        _waveManager.OnWaveChanged += OnWaveChanged;
    }

    private void OnWaveChanged(int waveNum)
    {
        _waveTextUI.UpdateWaveText(waveNum);
    }

    // ── Boss Encounter ─────────────────────────────────────────

    private void WireBossEncounter()
    {
        if (_bossEncounterUI == null) return;
        if (_bossManager == null) return;

        _bossManager.OnBossEntryed += OnBossSpawned;
    }

    private void OnBossSpawned(BossEntry prevEntry, BossEntry nextEntry)
    {
        int waveNum = _waveManager != null ? _waveManager.CurrentWave + 1 : 1;

        _bossEncounterUI.PlayBossTransitionSequence(prevEntry?.bossIcon, nextEntry?.bossIcon, waveNum).Forget();
    }

    // ── Unit Action ────────────────────────────────────────────

    private void WireUnitActionPopup()
    {
        if (_unitInfoPanel == null) { Debug.LogError("[InGameInstaller] _unitInfoPanel 미연결"); return; }
        if (_unitInfoPanel.MergeButton == null) Debug.LogError("[InGameInstaller] mergeButton 미연결");
        if (_unitInfoPanel.SellButton == null) Debug.LogError("[InGameInstaller] sellButton 미연결");
        if (_mergeManager == null) { Debug.LogError("[InGameInstaller] _mergeManager null — MergeManager 씬에 없음"); return; }

        _mergeManager.OnUnitSelected += _unitInfoPanel.SetData;
        _mergeManager.OnSelectionCleared += _unitInfoPanel.Close;
        _mergeManager.OnSelectionCleared += _totemInfoPanel.Close;

        _unitInfoPanel.MergeButton.OnMergeRequested += _mergeManager.ExecuteMerge;
        _unitInfoPanel.SellButton.OnSellRequested += OnSellUnitRequested;
        _unitInfoPanel.OnDismissRequested += _mergeManager.ClearSelection;
    }

    private void OnSellUnitRequested(UnitBase unit)
    {
        _spawnerManager.SellUnit(unit);
        _mergeManager.ClearSelection();
    }

    // ── Totem Action ───────────────────────────────────────────

    private void WireTotemActionPopup()
    {
        // SellTotemButtonUI에 팝업 참조 주입 (이벤트 발행 경로 확보)
        _sellTotemButton.SetPopup(_totemActionPopup);

        // DragHandler static 이벤트 → 팝업 Show
        DragHandler.OnTotemClickedGlobal += HandleTotemClicked;

        // 판매 이벤트 → TotemSpawner
        _totemActionPopup.OnSellTotemRequested += OnSellTotemRequested;

        // 외부 클릭 → 팝업 닫기
        _totemActionPopup.OnDismissRequested += ClearTotemRangePreview;
        _totemActionPopup.OnDismissRequested += _totemActionPopup.Hide;
        _totemActionPopup.OnDismissRequested += _totemInfoPanel.Close;
        _totemActionPopup.OnDismissRequested += _unitInfoPanel.Close;
    }

    private void HandleTotemClicked(TotemBase totem)
    {
        _totemActionPopup.Show(totem);
        _totemInfoPanel.SetData(totem.Data);
        _gridManager?.ShowTotemRangePreview(totem);
    }

    private void OnSellTotemRequested(TotemBase totem)
    {
        _gridManager?.ClearTotemRangePreview();
        _totemManager.SellTotem(totem);
    }

    private void ClearTotemRangePreview()
    {
        _gridManager?.ClearTotemRangePreview();
    }

    // ── 정리 ───────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (_waveManager != null)
        {
            _waveManager.OnWaveChanged -= OnWaveChanged;
        }

        if (_bossManager != null)
        {
            _bossManager.OnBossEntryed -= OnBossSpawned;
        }

        var merge = _mergeManager;
        if (merge != null)
        {
            merge.OnUnitSelected -= _unitInfoPanel.SetData;
            merge.OnSelectionCleared -= _unitInfoPanel.Close;
            _unitInfoPanel.MergeButton.OnMergeRequested -= merge.ExecuteMerge;
            _unitInfoPanel.OnDismissRequested -= merge.ClearSelection;
        }

        _unitInfoPanel.SellButton.OnSellRequested -= OnSellUnitRequested;
        DragHandler.OnTotemClickedGlobal -= HandleTotemClicked;
        _totemActionPopup.OnSellTotemRequested -= OnSellTotemRequested;
        _totemActionPopup.OnDismissRequested -= ClearTotemRangePreview;
        _totemActionPopup.OnDismissRequested -= _totemActionPopup.Hide;
    }
}