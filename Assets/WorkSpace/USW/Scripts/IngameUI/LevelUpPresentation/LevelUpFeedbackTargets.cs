using System.Collections.Generic;

/// <summary>카드의 표시 정의를 현재 배치에 대입한다. 카드 Kind나 전투 효과를 해석하지 않는다.</summary>
public static class LevelUpFeedbackTargets
{
    /// <summary>현재 저작 정의의 목적지를 조회한다. 프리뷰 난수를 소비하지 않는다.</summary>
    public static LevelUpFeedbackDestination Resolve(LevelUpData data, GridManager grid, List<UnitBase> result)
    {
        result.Clear();
        if (data == null) return LevelUpFeedbackDestination.None;
        var definition = SelectionDefinitionReader.CopyDefinition(data);
        var destination = LevelUpFeedbackDestination.None;
        foreach (var target in definition.Feedback)
        {
            if (target.Target == SelectionFeedbackTarget.ChiefSkill)
            { destination |= LevelUpFeedbackDestination.ChiefSkill; continue; }
            if (grid == null) continue;
            foreach (var cell in grid.GetOccupiedCells())
            {
                var unit = cell.OccupyingUnit;
                if (unit == null || !Matches(target, unit, cell.GridPosition.y, grid.Rows) || result.Contains(unit)) continue;
                result.Add(unit);
            }
        }
        if (result.Count > 0) destination |= LevelUpFeedbackDestination.Units;
        return destination;
    }

    private static bool Matches(SelectionFeedbackDefinition target, UnitBase unit, int row, int rows)
    {
        if (target.RequiresHighOwnedTier && unit.OriginalTier != Tier.Epic && unit.OriginalTier != Tier.Legend) return false;
        if (target.Row == SelectionFeedbackRow.Front && row > 1) return false;
        if (target.Row == SelectionFeedbackRow.Back && row < rows - 2) return false;
        if (target.Tribes.Length > 0)
        {
            if (unit.unitData == null) return false;
            bool found = false;
            foreach (var tribe in target.Tribes) if (tribe == unit.unitData.unitTribe) { found = true; break; }
            if (!found) return false;
        }
        return target.Target switch
        {
            SelectionFeedbackTarget.AllUnits => true,
            SelectionFeedbackTarget.Betan => unit is Drone_Betan,
            SelectionFeedbackTarget.Gamman => unit is Drone_Gamman,
            SelectionFeedbackTarget.Zeltan => unit is Drone_Zeltan,
            SelectionFeedbackTarget.Deltan => unit is Drone_Deltan,
            SelectionFeedbackTarget.CombatDroneOwner => unit is DroneSpawnerBase,
            _ => false
        };
    }
}
