using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 인게임 씬 배선 담당. 로직 없음 — UI 이벤트와 시스템 메서드를 연결하는 역할만.
/// </summary>
public class InGameInstaller : MonoBehaviour
{
    [Header("Unit Action Popup")]
    [SerializeField] private UnitActionPopupUI _unitActionPopup;
    [SerializeField] private MergeButtonUI _mergeButton;
    [SerializeField] private SellButtonUI _sellButton;
    [SerializeField] private GaeGGUL.UI.Unit.UI_UnitInfoPanel _unitInfoPanel;

    [Header("Totem Action Popup")]
    [SerializeField] private TotemActionPopupUI _totemActionPopup;
    [SerializeField] private SellTotemButtonUI _sellTotemButton;
    [SerializeField] private UI_TotemInfoPanel _totemInfoPanel;

    [Header("Boss Encounter")]
    [SerializeField] private UI_BossEncounter _bossEncounterUI;

    [Header("Wave Info")]
    [SerializeField] private UI_WaveText _waveTextUI;

    private void Start()
    {
        WireUnitActionPopup();
        WireTotemActionPopup();
        WireBossEncounter();
        WireWaveUI();
    }

    // ── Wave UI ────────────────────────────────────────────────

    private void WireWaveUI()
    {
        if (_waveTextUI == null) return;
        if (Manager.Wave == null) return;

        Manager.Wave.OnWaveChanged += OnWaveChanged;
    }

    private void OnWaveChanged(int waveNum)
    {
        _waveTextUI.UpdateWaveText(waveNum);
    }

    // ── Boss Encounter ─────────────────────────────────────────

    private void WireBossEncounter()
    {
        if (_bossEncounterUI == null) return;
        if (Manager.Boss == null) return;

        Manager.Boss.OnBossEntryed += OnBossSpawned;
    }

    private void OnBossSpawned(BossEntry prevEntry, BossEntry nextEntry)
    {
        int waveNum = Manager.Wave != null ? Manager.Wave.CurrentWave + 1 : 1;

        _bossEncounterUI.PlayBossTransitionSequence(prevEntry?.bossIcon, nextEntry?.bossIcon, waveNum).Forget();
    }

    // ── Unit Action ────────────────────────────────────────────

    private void WireUnitActionPopup()
    {
        if (_unitActionPopup == null) { Debug.LogError("[InGameInstaller] _unitActionPopup 미연결"); return; }
        if (_mergeButton == null) Debug.LogError("[InGameInstaller] _mergeButton 미연결");
        if (_sellButton == null) Debug.LogError("[InGameInstaller] _sellButton 미연결");
        if (_unitInfoPanel == null) Debug.LogError("[InGameInstaller] _unitInfoPanel 미연결");
        if (Manager.Merge == null) { Debug.LogError("[InGameInstaller] Manager.Merge null — MergeManager 씬에 없음"); return; }

        Manager.Merge.OnUnitSelected += _unitActionPopup.Show;
        Manager.Merge.OnUnitSelected += HandleUnitSelected;
        Manager.Merge.OnSelectionCleared += _unitActionPopup.Hide;
        Manager.Merge.OnSelectionCleared += _unitInfoPanel.Close;

        _mergeButton.OnMergeRequested += Manager.Merge.ExecuteMerge;
        _sellButton.OnSellRequested += OnSellUnitRequested;
        _unitActionPopup.OnDismissRequested += Manager.Merge.ClearSelection;
    }

    private void HandleUnitSelected(UnitBase unit, bool canMerge)
    {
        _unitInfoPanel.SetData(unit);
    }

    private void OnSellUnitRequested(UnitBase unit)
    {
        Manager.Spawner.SellUnit(unit);
        Manager.Merge.ClearSelection();
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
    }

    private void HandleTotemClicked(TotemBase totem)
    {
        _totemActionPopup.Show(totem);
        _totemInfoPanel.SetData(totem.Data);
        Manager.Grid?.ShowTotemRangePreview(totem);
    }

    private void OnSellTotemRequested(TotemBase totem)
    {
        Manager.Grid?.ClearTotemRangePreview();
        Manager.Totem.SellTotem(totem);
    }

    private void ClearTotemRangePreview()
    {
        Manager.Grid?.ClearTotemRangePreview();
    }

    // ── 정리 ───────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (Manager.Wave != null)
        {
            Manager.Wave.OnWaveChanged -= OnWaveChanged;
        }

        if (Manager.Boss != null)
        {
            Manager.Boss.OnBossEntryed -= OnBossSpawned;
        }

        var merge = Manager.Merge;
        if (merge != null)
        {
            merge.OnUnitSelected -= _unitActionPopup.Show;
            merge.OnUnitSelected -= HandleUnitSelected;
            merge.OnSelectionCleared -= _unitActionPopup.Hide;
            merge.OnSelectionCleared -= _unitInfoPanel.Close;
            _mergeButton.OnMergeRequested -= merge.ExecuteMerge;
            _unitActionPopup.OnDismissRequested -= merge.ClearSelection;
        }

        _sellButton.OnSellRequested -= OnSellUnitRequested;
        DragHandler.OnTotemClickedGlobal -= HandleTotemClicked;
        _totemActionPopup.OnSellTotemRequested -= OnSellTotemRequested;
        _totemActionPopup.OnDismissRequested -= ClearTotemRangePreview;
        _totemActionPopup.OnDismissRequested -= _totemActionPopup.Hide;
    }
}