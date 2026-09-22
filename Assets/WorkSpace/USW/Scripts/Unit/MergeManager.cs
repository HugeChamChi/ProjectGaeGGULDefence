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
 
    public void Init()
    {
    }

    private void OnEnable()
    {
        DragHandler.OnUnitClickedEvent += OnUnitClicked;
        DragHandler.OnDragStartedEvent += HideButton;
    }

    private void OnDisable()
    {
        DragHandler.OnUnitClickedEvent -= OnUnitClicked;
        DragHandler.OnDragStartedEvent -= HideButton;
    }

    [Inject] private ChieftainSpawner _chieftainManager;
    [Inject] private LevelUpManager _levelUpManager;
    [Inject] private UnitFactory _unitFactoryManager;
    [Inject] private UnitSpawner _spawnerManager;
    [Inject] private GridManager _gridManager;

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
        var nextTier  = (Tier)((int)_selectedUnit.OriginalTier + 1);
        MergeTargets(targets, targets[0].cell, nextTier);
    }

    /// <summary>
    /// 드래그 합성. 합성 가능한 두 유닛(동일 유닛·동일 등급, 또는 노말 + 와일드카드)을
    /// 드래그로 겹치면 다음 등급 랜덤 유닛 1기를 드롭한 셀에 배치한다. 합성 버튼 경로와 병행된다.
    /// </summary>
    public bool TryMergeByDrag(UnitBase dragged, UnitBase target)
    {
        if (dragged == null || target == null || dragged.IsStunned || target.IsStunned) return false;
        if (!UnitMergeRules.CanPair(dragged, target)) return false;
        var sourceCell = dragged.currentCell;
        var targetCell = target.currentCell;
        if (sourceCell == null || targetCell == null || sourceCell == targetCell ||
            sourceCell.OccupyingUnit != dragged || targetCell.OccupyingUnit != target ||
            sourceCell.Model.IsSealed || targetCell.Model.IsSealed) return false;
        return MergeTargets(new List<(UnitBase unit, GridCell cell)>
            { (dragged, sourceCell), (target, targetCell) }, targetCell, (Tier)((int)dragged.OriginalTier + 1));
    }

    private bool MergeTargets(List<(UnitBase unit, GridCell cell)> targets, GridCell spawnCell, Tier nextTier)
    {
        if (_unitFactoryManager == null || _spawnerManager == null) return false;
        var newUnit = _unitFactoryManager.CreateRandomUnitOfTier(nextTier);
        if (newUnit == null) return false;

        ClearSelection();

        foreach (var (unit, cell) in targets)
        {
            unit.OnRemoved();
            cell.RemoveUnit();
            UnityEngine.Object.Destroy(unit.gameObject);
        }

        // Use PlaceUnitWithEffect with the spawnCell as origin (so the effect plays without a long line traversal)
        _spawnerManager.PlaceUnitWithEffect(newUnit, spawnCell, spawnCell.transform.position);

        _spawnerManager.RequestMergeSupport();

        // [진로 계승] 진로 계승 보유 시 무작위 노멀 유닛 1기 추가 지급
        if (_levelUpManager?.HasMergeKeepsTribe == true)
        {
            var bonusUnit = _unitFactoryManager.CreateRandomNormalUnit();
            if (bonusUnit != null)
            {
                var emptyCells = _gridManager.GetEmptyCells();
                if (emptyCells.Count > 0)
                {
                    var randomCell = emptyCells[UnityEngine.Random.Range(0, emptyCells.Count)];
                    _spawnerManager.PlaceUnitWithEffect(bonusUnit, randomCell);
                }
                else
                {
                    UnityEngine.Object.Destroy(bonusUnit.gameObject);
                }
            }
        }
        return true;
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
        if (unit.OriginalTier == Tier.Legend) return false;
        if (unit.OriginalTier == Tier.Chieftain) return false;
        return GetMergeTargets(unit).Count >= 2;
    }

    private List<(UnitBase unit, GridCell cell)> GetMergeTargets(UnitBase unit)
    {
        var result = new List<(UnitBase, GridCell)>();
        if (unit == null || unit.currentCell == null || unit.currentCell.OccupyingUnit != unit) return result;
        result.Add((unit, unit.currentCell));
        foreach (var cell in _gridManager.AllCells())
        {
            var u = cell.OccupyingUnit;
            if (UnitMergeRules.CanPair(unit, u))
            {
                result.Add((u, cell));
                if (result.Count >= 2) break;
            }
        }
        return result;
    }
}
