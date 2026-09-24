/// <summary>
/// 배치된 토템을 끌어서 옮기는 동안의 효과 범위 미리보기 (씬 수명, DragHandler가 사용).
/// 손가락 아래에 놓을 수 있는 칸이 있으면 그 칸 기준으로, 없으면(그리드 밖·봉인·스턴 유닛 칸) 원래 칸 기준으로 칠한다.
/// 칸이 바뀔 때만 다시 칠한다. 손을 떼면 DragHandler.EndPress가 범위를 지운다.
/// </summary>
public sealed class TotemDragRangePreview
{
    private readonly GridManager _grid;
    private TotemBase _totem;
    private GridCell _origin;
    private GridCell _shownCell;

    /// <summary>씬 그리드를 받는다.</summary>
    public TotemDragRangePreview(GridManager grid) => _grid = grid;

    /// <summary>이동 드래그 시작 — 원래 칸 기준 범위를 칠한다.</summary>
    public void Begin(TotemBase totem, GridCell origin)
    {
        _totem = totem;
        _origin = origin;
        _shownCell = null;
        Show(origin);
    }

    /// <summary>손가락 아래 칸이 바뀌었을 수 있을 때 호출한다.</summary>
    public void Hover(GridCell cell)
    {
        if (_totem == null) return;
        var target = CanDropOn(cell) ? cell : _origin;
        if (target != _shownCell) Show(target);
    }

    /// <summary>드래그 종료 — 상태만 비운다 (범위 지우기는 호출자 담당).</summary>
    public void End()
    {
        _totem = null;
        _origin = null;
        _shownCell = null;
    }

    private void Show(GridCell cell)
    {
        _shownCell = cell;
        if (_totem == null || cell == null) { _grid?.ClearTotemRangePreview(); return; }
        _totem.PreviewMoveTo(cell, _grid);
    }

    // DragHandler.OnEndDrag / SetDropPreview와 같은 기준: 사용 가능한 칸이고 스턴 유닛이 없는 칸.
    private static bool CanDropOn(GridCell cell) =>
        cell != null && cell.Model != null && cell.Model.IsAvailable &&
        !(cell.OccupyingUnit != null && cell.OccupyingUnit.IsStunned);
}
