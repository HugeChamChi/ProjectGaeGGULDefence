/// <summary>인게임 봉인 실험실에서 비교하는 바탕 패턴.</summary>
public enum SealedGridPatternKind
{
    /// <summary>기존 움직이는 사선과 작은 자물쇠.</summary>
    MovingStripes,
    /// <summary>중앙 여백을 둔 큰 X와 작은 자물쇠.</summary>
    CrossLock,
    /// <summary>가는 X와 작은 자물쇠.</summary>
    ThinCrossLock,
    /// <summary>네 모서리 클램프와 작은 자물쇠.</summary>
    CornerLock,
    /// <summary>교차 체인과 작은 자물쇠.</summary>
    ChainLock,
    /// <summary>단순 테두리와 작은 자물쇠.</summary>
    FrameLock,
    /// <summary>금지 원과 작은 자물쇠.</summary>
    ProhibitionLock,
    /// <summary>격자와 작은 자물쇠.</summary>
    LatticeLock,
    /// <summary>이중 테두리와 작은 자물쇠.</summary>
    DoubleFrameLock
}
