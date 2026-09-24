using System.Collections.Generic;

/// <summary>획득 연출의 빛 구슬이 향할 곳.</summary>
public enum LevelUpFeedbackDestination
{
    /// <summary>특정 대상이 없는 선택지 (경제/특수) — 기본 수집 지점으로.</summary>
    None,
    /// <summary>그리드 위 유닛들에게 분산.</summary>
    Units,
    /// <summary>알팡 등 그리드 밖 족장 스킬 — 족장 스킬 버튼으로.</summary>
    ChiefSkill,
}

/// <summary>
/// 선택지가 "누구를" 강화하는지 판별한다 (연출 전용 — 실제 효과 적용은 LevelUpManager).
/// 드론 선택지는 DroneSelectionKind, 일반 선택지는 applicableTribes/스탯 효과 종류로 판단한다.
/// </summary>
public static class LevelUpFeedbackTargets
{
    /// <summary>대상 유닛을 result에 채우고 목적지 종류를 반환한다.</summary>
    public static LevelUpFeedbackDestination Resolve(LevelUpData data, GridManager grid, ChieftainSpawner chieftain,
                                                     List<UnitBase> result)
    {
        result.Clear();
        if (data == null) return LevelUpFeedbackDestination.None;

        var kind = data.droneEffect?.Kind ?? DroneSelectionKind.None;
        if (kind != DroneSelectionKind.None)
            return ResolveDrone(kind, grid, result);

        // 족장 전용 스탯
        if (data.primaryEffect == LevelUpEffectType.ChieftainAttackPercent ||
            data.primaryEffect == LevelUpEffectType.ChieftainFoodProductionPercent)
        {
            var unit = chieftain != null ? chieftain.ChieftainUnit : null;
            if (unit != null) { result.Add(unit); return LevelUpFeedbackDestination.Units; }
            return LevelUpFeedbackDestination.ChiefSkill;
        }

        if (grid == null || !AffectsUnits(data)) return LevelUpFeedbackDestination.None;

        int rows = grid.Rows;
        foreach (var cell in grid.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit == null || !MatchesTribe(data, unit) || !MatchesRow(data.primaryEffect, cell.GridPosition.y, rows)) continue;
            result.Add(unit);
        }
        return result.Count > 0 ? LevelUpFeedbackDestination.Units : LevelUpFeedbackDestination.None;
    }

    private static LevelUpFeedbackDestination ResolveDrone(DroneSelectionKind kind, GridManager grid, List<UnitBase> result)
    {
        switch (kind)
        {
            case DroneSelectionKind.AlphanCooldown:
            case DroneSelectionKind.AlphanDoubleShot:
            case DroneSelectionKind.AlphanGoldenMonocle:
                return LevelUpFeedbackDestination.ChiefSkill; // 알팡은 그리드 유닛이 아니라 족장 스킬
            case DroneSelectionKind.MergeSupport:
                return LevelUpFeedbackDestination.None;
        }
        if (grid == null) return LevelUpFeedbackDestination.None;

        foreach (var cell in grid.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit != null && MatchesDrone(kind, unit)) result.Add(unit);
        }
        return result.Count > 0 ? LevelUpFeedbackDestination.Units : LevelUpFeedbackDestination.None;
    }

    private static bool MatchesDrone(DroneSelectionKind kind, UnitBase unit)
    {
        switch (kind)
        {
            case DroneSelectionKind.BetanPeriodicBomb:
            case DroneSelectionKind.BetanAttackSpeed:
            case DroneSelectionKind.BetanFleetBomb:
            case DroneSelectionKind.BetanRepairKit:
                return unit is Drone_Betan;
            case DroneSelectionKind.GammanFrequency:
            case DroneSelectionKind.GammanEmergency:
                return unit is Drone_Gamman;
            case DroneSelectionKind.ZeltanAirFryer:
            case DroneSelectionKind.ZeltanColdStorage:
            case DroneSelectionKind.ZeltanMaintenance:
                return unit is Drone_Zeltan;
            case DroneSelectionKind.DeltanDefenseReduction:
            case DroneSelectionKind.DeltanDamageTaken:
            case DroneSelectionKind.DeltanCooldown:
                return unit is Drone_Deltan;
            case DroneSelectionKind.ExtraCombatDrone:
                // 에픽 이상 드론 소환 유닛의 드론 수 +1
                return unit is DroneSpawnerBase && unit.OriginalTier >= Tier.Epic && unit.OriginalTier < Tier.Chieftain;
            default:
                return false;
        }
    }

    /// <summary>유닛에게 적용되는 스탯 효과인지 (경험치/토템 효율/특수 경제 효과는 제외).</summary>
    private static bool AffectsUnits(LevelUpData data)
    {
        if (data.specialEffect == LevelUpSpecialEffect.GrantCourageBuff) return true;
        return IsUnitStat(data.primaryEffect) || IsUnitStat(data.secondaryEffect);
    }

    private static bool IsUnitStat(LevelUpEffectType type)
    {
        switch (type)
        {
            case LevelUpEffectType.AttackPercent:
            case LevelUpEffectType.AttackSpeedPercent:
            case LevelUpEffectType.CritChancePercent:
            case LevelUpEffectType.CritDamagePercent:
            case LevelUpEffectType.FoodProductionPercent:
            case LevelUpEffectType.ProjectileSizePercent:
            case LevelUpEffectType.GaugeSpeedPercent:
            case LevelUpEffectType.FrontRowAttackPercent:
            case LevelUpEffectType.BackRowAttackPercent:
            case LevelUpEffectType.FrontRowSpeedPercent:
            case LevelUpEffectType.BackRowSpeedPercent:
                return true;
            default:
                return false;
        }
    }

    private static bool MatchesTribe(LevelUpData data, UnitBase unit)
    {
        if (data.applicableTribes == null || data.applicableTribes.Length == 0) return true;
        if (unit.unitData == null) return false;
        foreach (var tribe in data.applicableTribes)
            if (unit.unitData.unitTribe == tribe) return true;
        return false;
    }

    // LevelUpManager의 줄 배율 규칙과 동일: 전방 = 0,1줄 / 후방 = 마지막 2줄 (GridPosition.y 기준)
    private static bool MatchesRow(LevelUpEffectType type, int row, int rows)
    {
        switch (type)
        {
            case LevelUpEffectType.FrontRowAttackPercent:
            case LevelUpEffectType.FrontRowSpeedPercent:
                return row <= 1;
            case LevelUpEffectType.BackRowAttackPercent:
            case LevelUpEffectType.BackRowSpeedPercent:
                return row >= rows - 2;
            default:
                return true;
        }
    }
}
