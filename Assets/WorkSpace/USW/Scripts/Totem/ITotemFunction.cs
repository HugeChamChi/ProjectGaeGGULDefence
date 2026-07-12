/// <summary>
/// 토템이 범위 안 셀에 적용하는 기능(버프 등)의 인터페이스.
/// RebuildCellBuffFlags → PaintAffectedCells 사이클마다 범위 안 각 셀에 대해 호출된다.
/// 셀은 매 사이클 ClearTotemEffects()로 초기화되므로 무상태 재계산이면 충분하다.
/// </summary>
public interface ITotemFunction
{
    void Apply(TotemBase totem, GridCell cell, TotemBuffManager buffManager);
}
