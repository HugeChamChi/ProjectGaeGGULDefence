using UnityEngine;

/// <summary>
/// 인게임 씬 배선 담당. 로직 없음 — UI 이벤트와 시스템 메서드를 연결하는 역할만.
/// </summary>
public class InGameInstaller : MonoBehaviour
{
    [Header("Unit Action Popup")]
    [SerializeField] private UnitActionPopupUI _unitActionPopup;
    [SerializeField] private MergeButtonUI     _mergeButton;
    [SerializeField] private SellButtonUI      _sellButton;

    [Header("Totem Action Popup")]
    [SerializeField] private TotemActionPopupUI  _totemActionPopup;
    [SerializeField] private SellTotemButtonUI   _sellTotemButton;

    private void Start()
    {
        WireUnitActionPopup();
        WireTotemActionPopup();
    }

    // ── Unit Action ────────────────────────────────────────────

    private void WireUnitActionPopup()
    {
        if (_unitActionPopup == null) { Debug.LogError("[InGameInstaller] _unitActionPopup 미연결"); return; }
        if (_mergeButton     == null) Debug.LogError("[InGameInstaller] _mergeButton 미연결");
        if (_sellButton      == null) Debug.LogError("[InGameInstaller] _sellButton 미연결");
        if (Manager.Merge    == null) { Debug.LogError("[InGameInstaller] Manager.Merge null — MergeManager 씬에 없음"); return; }

        Manager.Merge.OnUnitSelected     += _unitActionPopup.Show;
        Manager.Merge.OnSelectionCleared += _unitActionPopup.Hide;

        _mergeButton.OnMergeRequested           += Manager.Merge.ExecuteMerge;
        _sellButton.OnSellRequested             += OnSellUnitRequested;
        _unitActionPopup.OnDismissRequested     += Manager.Merge.ClearSelection;
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
        DragHandler.OnTotemClickedGlobal        += _totemActionPopup.Show;

        // 판매 이벤트 → TotemSpawner
        _totemActionPopup.OnSellTotemRequested  += Manager.Totem.SellTotem;

        // 외부 클릭 → 팝업 닫기
        _totemActionPopup.OnDismissRequested    += _totemActionPopup.Hide;
    }

    // ── 정리 ───────────────────────────────────────────────────

    private void OnDestroy()
    {
        var merge = Manager.Merge;
        if (merge != null)
        {
            merge.OnUnitSelected                    -= _unitActionPopup.Show;
            merge.OnSelectionCleared                -= _unitActionPopup.Hide;
            _mergeButton.OnMergeRequested           -= merge.ExecuteMerge;
            _unitActionPopup.OnDismissRequested     -= merge.ClearSelection;
        }

        _sellButton.OnSellRequested                 -= OnSellUnitRequested;
        DragHandler.OnTotemClickedGlobal            -= _totemActionPopup.Show;
        _totemActionPopup.OnSellTotemRequested      -= Manager.Totem.SellTotem;
        _totemActionPopup.OnDismissRequested        -= _totemActionPopup.Hide;
    }
}
