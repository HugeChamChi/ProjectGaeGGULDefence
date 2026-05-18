using System;
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
public class MergeManager : InGameSingleton<MergeManager>
{
    public event Action<UnitBase, bool> OnUnitSelected;
    public event Action                 OnSelectionCleared;

    private UnitBase _selectedUnit;

    // ── 외부 호출 ──────────────────────────────────────────────

    /// <summary>DragHandler.OnPointerClick에서 호출</summary>
    public void OnUnitClicked(UnitBase unit)
    {
        if (unit == Manager.Chieftain?.ChieftainUnit) return;

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

        var newUnit = Manager.LevelUp?.HasMergeKeepsTribe == true
            ? Manager.UnitFactory.CreateRandomUnitByTribeAndTier(tribe, nextTier)
            : Manager.UnitFactory.CreateRandomUnitOfTier(nextTier);
        if (newUnit == null) return;

        spawnCell.TryPlaceUnit(newUnit);
        newUnit.transform.SetParent(spawnCell.transform, false);

        Manager.UnitFactory.InitUnitRectTransform(newUnit);

        var drag = newUnit.GetComponent<DragHandler>();
        if (drag != null) drag.SetOriginCell(spawnCell);

        newUnit.OnPlaced(Manager.Currency, Manager.Boss.CurrentBoss, spawnCell);
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
        return GetMergeTargets(unit).Count >= 2;
    }

    private List<(UnitBase unit, GridCell cell)> GetMergeTargets(UnitBase unit)
    {
        var result = new List<(UnitBase, GridCell)>();
        foreach (var cell in Manager.Grid.AllCells())
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
