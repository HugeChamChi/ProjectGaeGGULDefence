using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 합성 시스템
///
/// 조건: 동일 unitType + 동일 UnitTier 유닛이 필드에 3마리 이상
/// 결과: 3마리 제거 → 다음 tier 랜덤 유닛 1마리 스폰 (첫 번째 빈 칸)
///
/// Scene 구성:
///   인게임 씬에 빈 GameObject + 이 컴포넌트 추가
/// </summary>
public class MergeManager : InGameSingleton<MergeManager>
{
    // 현재 버튼이 열려있는 유닛 (toggle용)
    private UnitBase _selectedUnit;

    // ── 외부 호출 ──────────────────────────────────────────────

    /// <summary>DragHandler.OnPointerClick에서 호출</summary>
    public void OnUnitClicked(UnitBase unit)
    {
        // 같은 유닛 재클릭 → 버튼 닫기
        if (_selectedUnit == unit)
        {
            HideButton();
            return;
        }

        _selectedUnit = unit;
        bool canMerge = CanMerge(unit);
        MergeButtonUI.Instance.Show(unit, canMerge);
    }

    /// <summary>합성 버튼 클릭 시 MergeButtonUI에서 호출</summary>
    public void ExecuteMerge()
    {
        if (_selectedUnit == null) return;
        if (!CanMerge(_selectedUnit))
        {
            HideButton();
            return;
        }

        var targets = GetMergeTargets(_selectedUnit);
        var spawnCell = targets[0].cell;
        var nextTier  = (UnitTier)((int)_selectedUnit.unitData.unitTier + 1);

        HideButton();

        // 3마리 제거
        foreach (var (unit, cell) in targets)
        {
            unit.OnRemoved();
            cell.RemoveUnit();
            Object.Destroy(unit.gameObject);
        }

        // 다음 티어 랜덤 유닛 스폰
        var newUnit = Manager.UnitFactory.CreateRandomUnitOfTier(nextTier);
        if (newUnit == null) return;

        spawnCell.TryPlaceUnit(newUnit);
        newUnit.transform.SetParent(spawnCell.transform, false);

        var rt = newUnit.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        var drag = newUnit.GetComponent<DragHandler>();
        if (drag != null) drag.SetOriginCell(spawnCell);

        newUnit.OnPlaced(Manager.Currency, Manager.Boss.CurrentBoss, spawnCell);

        Debug.Log($"[Merge] {_selectedUnit?.unitData?.unitName} 합성 완료 → {nextTier} 유닛 스폰");
    }

    public void HideButton()
    {
        _selectedUnit = null;
        if (MergeButtonUI.Instance != null)
            MergeButtonUI.Instance.Hide();
    }

    // ── 내부 로직 ──────────────────────────────────────────────

    public bool CanMerge(UnitBase unit)
    {
        if (unit?.unitData == null) return false;
        if (unit.unitData.unitTier == UnitTier.Unique) return false; // 최고 티어는 합성 불가
        return GetMergeTargets(unit).Count >= 3;
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
                if (result.Count >= 3) break;
            }
        }
        return result;
    }
}
