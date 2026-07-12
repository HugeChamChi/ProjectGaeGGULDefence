using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토템의 효과 범위(셀 집합)를 정의하는 인터페이스.
/// </summary>
public interface ITotemRange
{
    /// <summary>실제 배치된 토템 기준으로 영향받는 셀 목록을 계산한다.</summary>
    List<GridCell> GetCells(TotemBase totem, GridManager gridManager);

    /// <summary>배치 전 UI 미리보기용 — 토템 위치(0,0)·회전 0 기준 오프셋 목록.</summary>
    List<Vector2Int> GetPreviewOffsets();
}
