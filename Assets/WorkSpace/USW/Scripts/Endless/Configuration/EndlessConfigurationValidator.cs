using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>Importer와 런타임 시작 경계에서 함께 사용하는 제작 설정 검증.</summary>
public static class EndlessConfigurationValidator
{
    /// <summary>현재 지원하는 키 형식. 대소문자를 구분한다.</summary>
    public static bool IsValidKey(string key) => key != null && Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");

    /// <summary>활성화 가능 여부를 검사한다. 초안/비활성 설정은 오류 사유와 함께 false다.</summary>
    public static bool TryValidateForRun(EndlessModeData mode, out string error)
    {
        var errors = new List<string>();
        if (mode == null) { error = "무한 설정이 없습니다."; return false; }
        if (!IsValidKey(mode.Key)) errors.Add("잘못된 난이도 키");
        if (!mode.Enabled) errors.Add("비활성 초안 설정");
        if (mode.CycleLength <= 0) errors.Add("순환 길이는 양수여야 합니다.");
        if (mode.PenaltyInterval <= 0 || mode.PenaltyFirstApplyRound <= 1) errors.Add("잘못된 패널티 적용 경계");
        if (mode.PenaltyDrawCount != 2) errors.Add("승천은 서로 다른 2개 후보를 제시해야 합니다.");
        var positions = new HashSet<int>();
        if (mode.Slots == null || mode.Slots.Count != mode.CycleLength) errors.Add("순환 위치 수가 순환 길이와 다릅니다.");
        if (mode.Slots != null)
            foreach (var slot in mode.Slots)
            {
                if (slot == null || slot.Position < 1 || slot.Position > mode.CycleLength || !positions.Add(slot.Position))
                { errors.Add("순환 위치 누락/중복/범위 오류"); continue; }
                if (!Enum.IsDefined(typeof(EndlessBossRole), slot.Role) || slot.Role == EndlessBossRole.Unspecified) errors.Add($"{slot.Position}번 보스 역할 미정");
                if (!IsValidKey(slot.BossKey) || slot.Boss == null || slot.Boss.Prefab == null) errors.Add($"{slot.Position}번 보스 연결 미정");
            }
        var growth = mode.Growth;
        if (growth == null || !growth.IsConfigured) errors.Add("성장 설정 미정");
        else if (growth.FirstGrowthRound < 1 || growth.IntervalRounds < 1 || !NonnegativeFinite(growth.HpPercentPerStep) || !NonnegativeFinite(growth.DefenseAddedPerStep) || !NonnegativeFinite(growth.ExpPercentPerStep)) errors.Add("성장 간격/수치 오류");
        if (!TryValidatePool(mode.PenaltyPool, out string poolError)) errors.Add(poolError);
        if (!Enum.IsDefined(typeof(EndlessBossSelection), mode.BossSelection)) errors.Add("미지원 보스 선택 방식");
        if (mode.BossSelection == EndlessBossSelection.ShuffleAfterOpening && mode.Slots != null)
        {
            var checkedRoles = new HashSet<EndlessBossRole>();
            foreach (var slot in mode.Slots)
            {
                if (slot == null || slot.Role == EndlessBossRole.Unspecified || !checkedRoles.Add(slot.Role)) continue;
                var pool = slot.Role == EndlessBossRole.Breather ? mode.BreatherBossPool : mode.HardBossPool;
                if (!TryValidateBossPool(pool, slot.Role, out string bossPoolError)) errors.Add(bossPoolError);
            }
        }
        error = string.Join("\n", errors);
        return errors.Count == 0;
    }

    /// <summary>초기 런타임이 구현할 수 있는 패널티인지 검사한다. 보스 대상 후보는 아직 예약이다.</summary>
    public static bool IsSupportedPenalty(RunPenaltyData penalty)
    {
        if(penalty==null || !penalty.IsConfigured || !IsValidKey(penalty.Key) || string.IsNullOrWhiteSpace(penalty.DisplayName)
            || double.IsNaN(penalty.Delta) || double.IsInfinity(penalty.Delta)) return false;
        switch(penalty.Target)
        {
            case RunPenaltyTarget.UnitAttack: case RunPenaltyTarget.UnitAttackFrequency: case RunPenaltyTarget.ExperienceGain:
                return penalty.Unit==RunPenaltyUnit.Percent && penalty.Delta>-100 && penalty.Delta<=0;
            case RunPenaltyTarget.ChoiceReduction: return penalty.Unit==RunPenaltyUnit.Flat && penalty.Delta==-1;
            case RunPenaltyTarget.PermanentCellSeal: return penalty.Unit==RunPenaltyUnit.Flat && penalty.Delta==1;
            default:return false;
        }
    }

    /// <summary>활성 후보와 가중치를 검사한다. 미정 후보는 비활성으로만 보관한다.</summary>
    public static bool TryValidatePool(RunPenaltyPoolData pool, out string error)
    {
        if (pool == null || !IsValidKey(pool.Key) || pool.Entries == null) { error = "패널티 풀 미연결/잘못된 키"; return false; }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        bool any = false;
        int repeatable = 0;
        double sum = 0d;
        foreach (var entry in pool.Entries)
        {
            if (entry == null || entry.Penalty == null || !IsValidKey(entry.Penalty.Key) || !keys.Add(entry.Penalty.Key))
            { error = "패널티 풀 후보 연결 누락/중복"; return false; }
            if (!entry.Enabled) continue;
            if (!entry.WeightConfirmed || !NonnegativeFinite(entry.Weight) || entry.Weight <= 0d || !IsSupportedPenalty(entry.Penalty))
            { error = $"{entry.Penalty.Key}: 가중치/효과 미확정 또는 미지원"; return false; }
            any = true;
            sum += entry.Weight;
            if (!entry.Penalty.OncePerRun) repeatable++;
        }
        if (repeatable < 2) { error = "서로 다른 반복 가능한 숫자형 후보가 최소 2개 필요합니다."; return false; }
        if (!any || double.IsInfinity(sum)) { error = "유효한 추첨 후보가 없거나 가중치 합이 범위를 초과합니다."; return false; }
        error = "";
        return true;
    }

    /// <summary>Requires distinct registered definitions and at least one active candidate in the expected category.</summary>
    public static bool TryValidateBossPool(EndlessBossPoolData pool, EndlessBossRole role, out string error)
    {
        error = $"{role}: 보스 풀 미연결/잘못된 분류";
        if (pool == null || !IsValidKey(pool.Key) || pool.Role != role || pool.Entries == null
            || (role != EndlessBossRole.Breather && role != EndlessBossRole.Hard)) return false;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var definitions = new HashSet<BossData>();
        bool any = false;
        foreach (var entry in pool.Entries)
        {
            if (entry == null || !IsValidKey(entry.BossKey) || entry.Boss == null
                || !keys.Add(entry.BossKey) || !definitions.Add(entry.Boss))
            { error = $"{pool.Key}: 후보 연결 누락 또는 키/보스 정의 중복"; return false; }
            if (!entry.Enabled) continue;
            if (entry.Boss.Prefab == null) { error = $"{pool.Key}/{entry.BossKey}: 프리팹 미연결"; return false; }
            any = true;
        }
        if (!any) { error = $"{pool.Key}: 활성 보스 후보가 없습니다."; return false; }
        error = "";
        return true;
    }

    private static bool NonnegativeFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}
