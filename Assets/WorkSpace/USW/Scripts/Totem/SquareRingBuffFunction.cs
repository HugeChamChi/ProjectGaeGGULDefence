using System;

/// <summary>토템의 전체 효과 범위 중 지정한 사각 고리에만 버프를 적용한다.</summary>
[Serializable, DisplayName("사각 고리별 버프")]
public class SquareRingBuffFunction : ITotemFunction
{
    /// <summary>이 효과가 적용될 고리.</summary>
    public TotemSquareRingRange Range = new TotemSquareRingRange();
    /// <summary>기존 단순 버프의 종류와 부호 있는 수치. 0.1은 10% 증가.</summary>
    public SimpleBuffFunction Buff = new SimpleBuffFunction();

    /// <inheritdoc />
    public void Apply(TotemBase totem, GridCell cell, TotemBuffManager buffManager)
    {
        if (totem?.CurrentCell == null || cell == null || Range == null) return;
        if (Range.Contains(cell.GridPosition - totem.CurrentCell.GridPosition))
            Buff?.Apply(totem, cell, buffManager);
    }
}
