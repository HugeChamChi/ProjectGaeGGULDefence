using System;

/// <summary>
/// 토템이 배치된 위치(축 좌표)가 임계값 조건을 만족하는지 판정.
/// 예) TotemUnitedBuff의 "앞쪽에 배치된 경우에만" / TotemPositionBuff의 "앞줄/뒷줄 판정"과 동일한 패턴.
/// </summary>
[Serializable]
public class PositionThresholdCondition : ITotemCondition
{
    public enum Axis { X, Y }
    public enum Comparison { GreaterOrEqual, LessThan }

    public Axis axis = Axis.Y;
    public Comparison comparison = Comparison.LessThan;
    public int threshold;

    public bool IsMet(TotemBase totem, GridCell cell)
    {
        if (totem == null || totem.CurrentCell == null) return false;

        var pos = totem.CurrentCell.GridPosition;
        int value = axis == Axis.X ? pos.x : pos.y;

        return comparison == Comparison.GreaterOrEqual ? value >= threshold : value < threshold;
    }
}
