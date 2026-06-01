using System;
using VContainer;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 합성 시스템.
///
/// 조건: 동일 unitType + 동일 UnitTier 유닛이 필드에 2마리 이상
/// 결과: 2마리 제거 → 다음 tier 랜덤 유닛 1마리 스폰 (첫 번째 빈 칸)
///
/// 도메인 이벤트 (InGameInstaller가 UI에 연결)
///   OnUnitSelected(unit, canMerge) — 유닛 선택됨
///   OnSelectionCleared             — 선택 해제됨
/// </summary>
public class MergeManager : MonoBehaviour
{
    [Inject] private IObjectResolver _resolver;
 
    public void Init()
    {
        if (_chieftainManager == null) _chieftainManager = _resolver.Resolve<ChieftainSpawner>();
        if (_levelUpManager == null) _levelUpManager = _resolver.Resolve<LevelUpManager>();
        if (_unitFactoryManager == null) _unitFactoryManager = _resolver.Resolve<UnitFactory>();
        if (_spawnerManager == null) _spawnerManager = _resolver.Resolve<UnitSpawner>();
        if (_gridManager == null) _gridManager = _resolver.Resolve<GridManager>();

        
    }

    private ChieftainSpawner _chieftainManager;
    private LevelUpManager _levelUpManager;
    private UnitFactory _unitFactoryManager;
    private UnitSpawner _spawnerManager;
    private GridManager _gridManager;

    public event Action<UnitBase, bool> OnUnitSelected;
    public event Action                 OnSelectionCleared;

    private UnitBase _selectedUnit;

    // ── 외부 호출 ──────────────────────────────────────────────

    /// <summary>DragHandler.OnPointerClick에서 호출</summary>
    public void OnUnitClicked(UnitBase unit)
    {
        if (unit == _chieftainManager?.ChieftainUnit) return;

        if (_selectedUnit == unit) { ClearSelection(); return; }

        _selectedUnit = unit;
        OnUnitSelected?.Invoke(unit, CanMerge(unit));
    }

    /// <summary>MergeButtonUI.OnMergeRequested → InGameInstaller → 이 메서드</summary>
    public void ExecuteMerge()
    {
        if (_selectedUnit == null) return;
        if (!CanMerge(_selectedUnit)) { ClearSelection(); return; }

        var targets   = GetMergeTargets(_selectedUnit);
        var spawnCell = targets[0].cell;
        var nextTier  = (Tier)((int)_selectedUnit.unitData.unitTier + 1);
        var tribe     = _selectedUnit.unitData.unitTribe;

        ClearSelection();

        foreach (var (unit, cell) in targets)
        {
            unit.OnRemoved();
            cell.RemoveUnit();
            UnityEngine.Object.Destroy(unit.gameObject);
        }

        var newUnit = _levelUpManager?.HasMergeKeepsTribe == true
            ? _unitFactoryManager.CreateRandomUnitByTribeAndTier(tribe, nextTier)
            : _unitFactoryManager.CreateRandomUnitOfTier(nextTier);
        if (newUnit == null) return;

        // Use PlaceUnitWithEffect with the spawnCell as origin (so the effect plays without a long line traversal)
        _spawnerManager.PlaceUnitWithEffect(newUnit, spawnCell, spawnCell.transform.position);
    }

    /// <summary>선택 해제 및 OnSelectionCleared 이벤트 발행</summary>
    public void ClearSelection()
    {
        _selectedUnit = null;
        OnSelectionCleared?.Invoke();
    }

    /// <summary>DragHandler 호환용 — ClearSelection 위임</summary>
    public void HideButton() => ClearSelection();

    // ── 내부 로직 ──────────────────────────────────────────────

    public bool CanMerge(UnitBase unit)
    {
        if (unit?.unitData == null) return false;
        if (unit.unitData.unitTier == Tier.Legend) return false;
        if (unit.unitData.unitTier == Tier.Chieftain) return false;
        return GetMergeTargets(unit).Count >= 2;
    }

    private List<(UnitBase unit, GridCell cell)> GetMergeTargets(UnitBase unit)
    {
        var result = new List<(UnitBase, GridCell)>();
        foreach (var cell in _gridManager.AllCells())
        {
            var u = cell.OccupyingUnit;
            if (u != null &&
                u.unitData != null &&
                u.unitData.unitType == unit.unitData.unitType &&
                u.unitData.unitTier == unit.unitData.unitTier)
            {
                result.Add((u, cell));
                if (result.Count >= 2) break;
            }
        }
        return result;
    }
}
