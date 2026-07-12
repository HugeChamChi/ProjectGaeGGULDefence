using UnityEngine;

/// <summary>
/// 단순 수치+범위 토템 공용 스크립트 (총 33개 토템 공유).
///
/// TotemData SO의 수치를 읽어 effectRange 셀에만 셀별 버프를 기록한다.
/// 범위 순회 / attackDisabledRange / ApplyBuff/RemoveBuff는 RangedBuffTotemBase가 담당.
/// </summary>
public class GenericBuffTotem : RangedBuffTotemBase
{
    protected override void PaintRangeBuffs(GridCell cell)
    {
        foreach (var fn in totemData.functions)
            fn?.Apply(this, cell, _totemBuffManager);
    }
}
