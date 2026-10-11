/// <summary>배치 불가 그리드 실험실에서 비교하는 표시 형태.</summary>
public enum BlockedGridStyleKind
{
    /// <summary>회색 채움, 가는 사선과 자물쇠.</summary>
    HatchLock,
    /// <summary>잠긴 모서리와 중앙 자물쇠.</summary>
    CornerClamp,
    /// <summary>대각선 체인으로 봉인된 칸.</summary>
    ChainSeal,
    /// <summary>가느다란 큰 X와 테두리.</summary>
    CrossMark
}
