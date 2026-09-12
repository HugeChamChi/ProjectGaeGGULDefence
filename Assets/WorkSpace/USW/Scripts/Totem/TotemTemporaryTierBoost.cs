using UnityEngine;

/// <summary>TD1005: 범위 한 칸의 유닛 전투 데이터만 한 단계 상승시키고 이탈/제거 시 복구한다.</summary>
public sealed class TotemTemporaryTierBoost : RangedBuffTotemBase
{
    private UnitBase _target;

    protected override void ApplyBuff()
    {
        UnitBase.OnAnyUnitChanged += RefreshTarget;
        RefreshTarget();
    }

    protected override void RemoveBuff()
    {
        UnitBase.OnAnyUnitChanged -= RefreshTarget;
        ReleaseTarget();
    }

    /// <inheritdoc />
    public override void PaintAffectedCells()
    {
        base.PaintAffectedCells();
        RefreshTarget();
    }

    protected override void PaintRangeBuffs(GridCell cell)
    {
        foreach (var function in Data.functions)
            function?.Apply(this, cell, _totemBuffManager);
    }

    private void RefreshTarget()
    {
        UnitBase next = null;
        if (IsActive)
        {
            var cells = GetAffectedCells();
            if (cells.Count == 1 && cells[0].OccupyingUnit != null &&
                cells[0].OccupyingUnit.currentCell == cells[0]) next = cells[0].OccupyingUnit;
        }
        if (_target != next) ReleaseTarget();
        if (next == null || next.HasTemporaryTier) return;
        UnitData replacement = next.OriginalData;
        foreach (var entry in Data.TierUpgrades)
            if (entry != null && entry.OriginalData == next.OriginalData && entry.OriginalTier == next.OriginalTier)
            { replacement = entry.UpgradedData; break; }
        if (next.TryApplyTemporaryTier(this, replacement)) _target = next;
    }

    private void ReleaseTarget()
    {
        if (_target != null) _target.RemoveTemporaryTier(this);
        _target = null;
    }
}
