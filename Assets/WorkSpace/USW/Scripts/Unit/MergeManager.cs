using System;
using VContainer;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 합성 시스템.
///
/// 레벨 유닛은 같은 종류와 기본 레벨끼리 합성해 같은 종류의 기본 레벨 +1 유닛을 만든다(비레벨 유닛은 기존 다음 등급 무작위).
/// 토템 임시 레벨은 재료 조건과 결과 기본 레벨에서 제외한다.
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

    [Inject] private ISelectionEconomyReader _selectionEconomy;
    [Inject] private UnitFactory _unitFactoryManager;
    [Inject] private UnitSpawner _spawnerManager;
    [Inject] private GridManager _gridManager;
    [Inject] private MergeEffectPlayer _mergeEffect;
    [Inject] private IGlitchEffectReader _glitchEffects;
    [Inject] private TimerController _timer;
    [Inject] private GameManager _gameManager;
    [Inject] private BossManager _bossManager;
    private bool _mergeInProgress;

    public event Action<UnitBase, bool> OnUnitSelected;
    public event Action                 OnSelectionCleared;

    private UnitBase _selectedUnit;
    [SerializeField] private GaeGGUL.Tutorial.IngameTutorialSettings _tutorialSettings;

    // ── 외부 호출 ──────────────────────────────────────────────

    /// <summary>DragHandler.OnPointerClick에서 호출</summary>
    public void OnUnitClicked(UnitBase unit)
    {
        if (_gameManager?.IsTimeVaultActive == true) return;
        if (_selectedUnit == unit) { ClearSelection(); return; }

        _selectedUnit = unit;
        OnUnitSelected?.Invoke(unit, CanMerge(unit));
    }

    /// <summary>
    /// 합성 가능한 두 유닛을 겹치면 합성 결과 유닛을 드롭한 셀에 배치한다.
    /// </summary>
    public bool TryMergeByDrag(UnitBase dragged, UnitBase target)
    {
        if (_gameManager?.IsTimeVaultActive == true) return false;
        if (_mergeInProgress || _gameManager != null && _gameManager.CurrentState != GameManager.GameState.Playing) return false;
        if (dragged == null || target == null || dragged.IsStunned || target.IsStunned) return false;
        if (!UnitMergeRules.CanPair(dragged, target)) return false;
        var sourceCell = dragged.currentCell;
        var targetCell = target.currentCell;
        if (sourceCell == null || targetCell == null || sourceCell == targetCell ||
            sourceCell.OccupyingUnit != dragged || targetCell.OccupyingUnit != target ||
            targetCell.Model.IsSealed) return false;
        _mergeInProgress = true;
        try
        {
            return MergeTargets(new List<(UnitBase unit, GridCell cell)>
                { (dragged, sourceCell), (target, targetCell) }, targetCell, (Tier)((int)dragged.OriginalTier + 1));
        }
        finally { _mergeInProgress = false; }
    }

    private bool MergeTargets(List<(UnitBase unit, GridCell cell)> targets, GridCell spawnCell, Tier nextTier)
    {
        if (_unitFactoryManager == null || _spawnerManager == null) return false;
        bool levelMerge = targets[0].unit.UsesTotemLevels;
        int nextLevel = levelMerge ? targets[0].unit.BaseLevel + 1 : 1;
        bool overload = _glitchEffects?.HasOverloadMerge == true &&
            (levelMerge ? nextLevel < UnitBase.MaxUnitLevel : nextTier < Tier.Legend);
        float cost = overload ? _glitchEffects.OverloadMergeCostSeconds : 0f;
        if (overload)
        {
            if (_timer == null) return false;
            if (!_timer.CanSurviveTimeCost(cost))
            {
                _timer.SpendTimeFromMerge(cost, null, _bossManager?.CurrentBoss);
                return false;
            }
            if (UnityEngine.Random.value < _glitchEffects.OverloadMergeUpgradeChance)
            {
                if (levelMerge) nextLevel++;
                else nextTier = (Tier)((int)nextTier + 1);
            }
        }
        if (levelMerge) nextTier = Tier.Normal;
        var newUnit = levelMerge
            ? _unitFactoryManager.CreateUnitFromData(targets[0].unit.OriginalData, Tier.Normal)
            : _tutorialSettings != null
            ? _unitFactoryManager.CreateUnitFromData(_tutorialSettings.MergeUnit, nextTier)
            : _unitFactoryManager.CreateRandomUnitOfTier(nextTier);
        if (newUnit == null) return false;
        if (levelMerge)
        {
            if (!newUnit.UsesTotemLevels)
            {
                UnityEngine.Object.Destroy(newUnit.gameObject);
                return false;
            }
            newUnit.SetBaseLevel(nextLevel);
        }
        // Existing factory preparation failure remains free. Materials are still intact here.
        if (overload && !_timer.SpendTimeFromMerge(cost, newUnit, _bossManager?.CurrentBoss))
        {
            UnityEngine.Object.Destroy(newUnit.gameObject);
            return false;
        }

        ClearSelection();

        // 합성 연출: 재료 잔상 흡입 → 먼지구름 → 소환 줄기가 솟았다 합성 칸에 내리꽂힘 (파괴 전에 잔상 복사)
        Vector3 center = spawnCell.transform.position;
        var materials = new List<UnitBase>(targets.Count);
        foreach (var (unit, _) in targets) materials.Add(unit);
        float lineDelay = _mergeEffect?.PlayMerge(materials, center) ?? 0f;

        foreach (var (unit, cell) in targets)
        {
            unit.OnRemoved();
            cell.RemoveUnit();
            UnityEngine.Object.Destroy(unit.gameObject);
        }

        if (_mergeEffect != null)
            _spawnerManager.PlaceUnitWithEffect(newUnit, spawnCell, _mergeEffect.GetLineOrigin(center),
                lineDelay, _mergeEffect.LineCurveHeight);
        else
            _spawnerManager.PlaceUnitWithEffect(newUnit, spawnCell, center);

        _spawnerManager.RequestMergeSupport();

        // [진로 계승] 진로 계승 보유 시 무작위 노멀 유닛 1기 추가 지급
        if (_selectionEconomy?.HasMergeKeepsTribe == true)
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
        if (_gameManager?.IsTimeVaultActive == true) return false;
        if (unit?.unitData == null) return false;
        if (unit.UsesTotemLevels ? unit.BaseLevel >= UnitBase.MaxUnitLevel : unit.OriginalTier == Tier.Legend) return false;
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
