using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>캐스터 중심 기준 range.cells에 해당하는 칸 중 무작위 아군 1체에게 버프를 부여하는
/// 스킬 액션. (TotemData의 TotemRelativeOffsetRange와 동일한 "중심 기준 상대 좌표" 패턴 —
/// Inspector에서 OffsetGridDataDrawer가 그리드 클릭 UI로 편집을 지원한다.)</summary>
[Serializable]
public class BuffAllyInRangeSkillAction : ISkillAction
{
    [Tooltip("버프 대상을 찾는 칸 (캐스터 중심 기준 상대 좌표). Inspector의 그리드를 클릭해 편집하세요.")]
    public OffsetGridData range = new OffsetGridData();
    [Tooltip("대상에게 부여할 버프")]
    public BuffData buff;

    public void Execute(UnitBase caster, UnitCombatComponent combat, List<IEffect> hitEffects, List<IAdditionalEffect> additionalEffects)
    {
        var target = GetRandomAllyInRange(caster);
        if (target == null || buff == null) return;
        target.Buffs?.ApplyBuff(buff, caster);
    }

    private UnitBase GetRandomAllyInRange(UnitBase caster)
    {
        var grid = caster._gridManager;
        var selfCell = caster.currentCell;
        if (grid == null || selfCell == null || range?.cells == null) return null;

        var targetCells = new HashSet<Vector2Int>();
        foreach (var offset in range.cells)
            targetCells.Add(selfCell.GridPosition + offset);

        var candidates = new List<UnitBase>();
        foreach (var cell in grid.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit == null || unit == caster) continue;
            if (targetCells.Contains(cell.GridPosition)) candidates.Add(unit);
        }

        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }
}
