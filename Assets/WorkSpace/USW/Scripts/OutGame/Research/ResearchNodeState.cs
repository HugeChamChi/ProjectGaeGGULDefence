/// <summary>강화 노드 상태.</summary>
public enum ResearchNodeState
{
    /// <summary>앞 노드를 아직 끝내지 않았거나 전체 강화 횟수가 모자라 올릴 수 없다.</summary>
    Locked = 0,
    /// <summary>지금 올릴 수 있다 (0레벨이거나 진행 중).</summary>
    Available = 1,
    /// <summary>최대 레벨.</summary>
    Maxed = 2,
    /// <summary>택1 갈림길에서 다른 쪽을 골라 막혔다 ([선택 바꾸기]로 풀 수 있다).</summary>
    Blocked = 3,
}
