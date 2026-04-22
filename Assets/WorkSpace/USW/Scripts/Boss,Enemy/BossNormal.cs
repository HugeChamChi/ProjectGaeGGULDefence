using UnityEngine;

/// <summary>
/// 1웨이브 보스 — 기본 패턴 보스
/// BossBase를 상속하여 ExecutePattern() 구현
/// 
/// 패턴 추가 시: BossPatternData SO를 만들고 BossData.patterns[]에 등록
/// </summary>
public class BossNormal : BossBase
{
    public override void ExecutePattern(BossPatternData patternData)
    {
        if (patternData == null) return;

        switch (patternData.patternType)
        {
            case BossPatternType.DamageReduction:
                ApplyDamageReduction(patternData);
                break;

            case BossPatternType.SpeedReduction:
                ApplySpeedReduction(patternData);
                break;

            case BossPatternType.AttackDisable:
                ApplyAttackDisable(patternData);
                break;

            case BossPatternType.DestroyUnit:
                ApplyDestroyUnit(patternData);
                break;

            case BossPatternType.SealCell:
                ApplySealCell(patternData);
                break;

            default:
                Debug.LogWarning($"[BossNormal] 미구현 패턴: {patternData.patternType}");
                break;
        }
    }

    // ── 패턴 구현 ──────────────────────────────────────────────

    private void ApplyDamageReduction(BossPatternData data)
    {
        var cells = GetTargetCells(data);
        foreach (var cell in cells)
            cell.Model.SetDamageModifier(1f - data.damageReductionRate, data.duration);

        Debug.Log($"[BossNormal] 데미지 감소 패턴 발동 — {cells.Count}칸");
    }

    private void ApplySpeedReduction(BossPatternData data)
    {
        var cells = GetTargetCells(data);
        foreach (var cell in cells)
            cell.Model.SetSpeedModifier(1f + data.speedReductionRate, data.duration);

        Debug.Log($"[BossNormal] 속도 감소 패턴 발동 — {cells.Count}칸");
    }

    private void ApplyAttackDisable(BossPatternData data)
    {
        var cells = GetTargetCells(data);
        foreach (var cell in cells)
            cell.Model.SetAttackDisabled(true, data.duration);

        Debug.Log($"[BossNormal] 공격 불가 패턴 발동 — {cells.Count}칸");
    }

    private void ApplyDestroyUnit(BossPatternData data)
    {
        var cells = GetTargetCells(data);
        foreach (var cell in cells)
        {
            var unit = cell.OccupyingUnit;
            if (unit == null) continue;

            unit.OnRemoved();
            cell.RemoveUnit();
            Destroy(unit.gameObject);
        }

        Debug.Log($"[BossNormal] 기물 파괴 패턴 발동");
    }

    private void ApplySealCell(BossPatternData data)
    {
        var cells = GetTargetCells(data);
        foreach (var cell in cells)
            cell.Model.SetSealed(true, data.duration);

        Debug.Log($"[BossNormal] 셀 봉인 패턴 발동 — {cells.Count}칸");
    }

    // ── 범위 셀 계산 ───────────────────────────────────────────
    private System.Collections.Generic.List<GridCell> GetTargetCells(BossPatternData data)
    {
        if (data.rangeType == PatternRangeType.Fixed)
            return GetFixedCells(data.fixedCells);
        else
            return GetRandomCells(data.randomCellCount);
    }

    private System.Collections.Generic.List<GridCell> GetFixedCells(Vector2Int[] coords)
    {
        var list = new System.Collections.Generic.List<GridCell>();
        foreach (var coord in coords)
        {
            var cell = Manager.Grid.GetCell(coord.x, coord.y);
            if (cell != null) list.Add(cell);
        }
        return list;
    }

    private System.Collections.Generic.List<GridCell> GetRandomCells(int count)
    {
        var all  = Manager.Grid.AllCells();
        var list = new System.Collections.Generic.List<GridCell>(all);

        // 피셔-예이츠 셔플로 count개 선택
        for (int i = 0; i < Mathf.Min(count, list.Count); i++)
        {
            int j    = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list.GetRange(0, Mathf.Min(count, list.Count));
    }
}
