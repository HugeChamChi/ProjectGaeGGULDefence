using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>중심 기준 상대 좌표 목록을 감싸는 래퍼. List&lt;Vector2Int&gt;를 필드로 직접 두면
/// Alchemy 인스펙터가 무조건 기본 리스트뷰로만 그려서, 일반 클래스로 감싸 전용
/// CustomPropertyDrawer(OffsetGridDataDrawer)가 적용되도록 한다 — Alchemy는 Generic 타입
/// 필드에 한해 등록된 클래식 PropertyDrawer가 있으면 그걸 우선 사용한다.</summary>
[Serializable]
public class OffsetGridData
{
    public List<Vector2Int> cells = new List<Vector2Int>
    {
        new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        new Vector2Int(-1,  0),                         new Vector2Int(1,  0),
        new Vector2Int(-1,  1), new Vector2Int(0,  1), new Vector2Int(1,  1),
    };
}
