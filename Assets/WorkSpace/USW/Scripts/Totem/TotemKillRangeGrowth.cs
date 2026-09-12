using System.Collections.Generic;
using UnityEngine;

/// <summary>TD1002: 배치 이후 보스 처치에 따라 범위만 성장한다. 이동/회전은 누적을 보존한다.</summary>
public sealed class TotemKillRangeGrowth : RangedBuffTotemBase
{
    private int _killCount;
    /// <summary>이 토템이 배치되어 있는 동안 누적한 처치 수.</summary>
    public int KillCount => _killCount;

    protected override void ApplyBuff() => BossBase.OnAnyBossDied += OnBossKilled;
    protected override void RemoveBuff() => BossBase.OnAnyBossDied -= OnBossKilled;

    private void OnBossKilled()
    {
        if (!IsActive || Data == null) return;
        int maximum = 0;
        foreach (var stage in Data.GrowthStages)
            if (stage != null) maximum = Mathf.Max(maximum, stage.RequiredKills);
        if (_killCount >= maximum) return;
        _killCount++;
        _totemBuffManager?.RebuildCellBuffFlags();
        if (_gridManager != null && _gridManager.IsPreviewingTotem(this))
            _gridManager.ShowTotemRangePreview(this);
    }

    /// <inheritdoc />
    public override List<GridCell> GetAffectedCells()
    {
        var cells = new List<GridCell>();
        if (!IsActive || Data == null || CurrentCell == null || _gridManager == null) return cells;
        TotemGrowthStage selected = null;
        foreach (var stage in Data.GrowthStages)
            if (stage != null && stage.RequiredKills <= _killCount &&
                (selected == null || stage.RequiredKills > selected.RequiredKills)) selected = stage;
        if (selected == null) return base.GetAffectedCells();
        foreach (var offset in selected.Offsets)
        {
            var position = CurrentCell.GridPosition + RotateOffset(offset);
            var cell = _gridManager.GetCell(position.x, position.y);
            if (cell != null && !cells.Contains(cell)) cells.Add(cell);
        }
        return cells;
    }

    /// <inheritdoc />
    public override void PaintAffectedCells()
    {
        if (!IsActive || Data == null) return;
        foreach (var cell in GetAffectedCells()) PaintRangeBuffs(cell);
        foreach (var cell in Data.GetAttackDisabledCells(this, _gridManager))
            cell.SetTotemAttackDisabled(true);
    }

    protected override void PaintRangeBuffs(GridCell cell)
    {
        foreach (var function in Data.functions)
            function?.Apply(this, cell, _totemBuffManager);
    }
}
