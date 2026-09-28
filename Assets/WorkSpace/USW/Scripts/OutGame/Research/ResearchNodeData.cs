using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 강화 노드 하나 (ResearchTreeData 안에 목록으로 저장).
/// 위치는 칸 좌표: Row는 아래에서 위로 0, 1, 2… / Column은 -1 왼쪽, 0 가운데, 1 오른쪽, 2 옆가지(오른쪽 바깥, 특별 노드 자리).
/// 해금: 부모 조건(모두 또는 하나) + 트리 전체 강화 횟수(RequiredTotalLevel) + 택1 그룹에서 다른 쪽을 고르지 않았을 것.
/// </summary>
[Serializable]
public class ResearchNodeData
{
    /// <summary>표시·저장 키. 예: "3-2" → 화면에는 "강화 3-2".</summary>
    public string Id;
    public ResearchStat Stat;
    public Sprite Icon;
    public ResearchNodeShape Shape;
    [Min(1)] public int MaxLevel = 3;
    [Tooltip("레벨당 오르는 값. 3레벨이면 이 값 × 3")]
    public float ValuePerLevel = 1f;
    public int Row;
    [Range(-1, 2)] public int Column;

    [Header("해금 조건")]
    [Tooltip("이 노드들이 최대 레벨이어야 열린다. 비우면 부모 조건 없음")]
    public List<string> Parents = new List<string>();
    [Tooltip("true면 부모 중 하나만 최대여도 열린다 (택1 갈림길 뒤 합류 노드)")]
    public bool RequireAnyParent;
    [Tooltip("트리 전체 강화 횟수(모든 노드 레벨 합)가 이 값 이상이어야 열린다. 관문 노드용, 0이면 조건 없음")]
    [Min(0)] public int RequiredTotalLevel;
    [Tooltip("같은 그룹 이름끼리는 하나만 고를 수 있다 (택1). 비우면 택1 아님")]
    public string ExclusiveGroup;

    [Header("추천")]
    [Tooltip("작을수록 먼저 추천한다. 0 이하면 추천하지 않는다")]
    public int RecommendOrder;
}
