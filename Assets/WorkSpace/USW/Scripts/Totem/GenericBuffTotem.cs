using UnityEngine;

/// <summary>
/// 단순 수치+범위 토템 공용 스크립트 (총 33개 토템 공유).
///
/// TotemData SO의 수치를 읽어 effectRange 셀에만 셀별 버프를 기록한다.
/// 범위 순회 / attackDisabledRange / ApplyBuff/RemoveBuff는 RangedBuffTotemBase가 담당.
/// </summary>
public class GenericBuffTotem : RangedBuffTotemBase
{
    /// <summary>Grouped effects apply only their own functions to their own cells.</summary>
    public override void PaintAffectedCells()
    {
        if (totemData == null || !totemData.HasEffectGroups) { base.PaintAffectedCells(); return; }
        if (CurrentCell == null) return;
        foreach (var group in totemData.EffectGroups)
            if (group != null) foreach (var cell in group.GetCells(this, _gridManager))
                foreach (var function in group.Functions) function?.Apply(this, cell, _totemBuffManager);
    }
    protected override void PaintRangeBuffs(GridCell cell)
    {
        foreach (var fn in totemData.functions)
            fn?.Apply(this, cell, _totemBuffManager);
    }
}
