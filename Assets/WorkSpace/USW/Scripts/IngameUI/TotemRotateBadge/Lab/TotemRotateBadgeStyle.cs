/// <summary>토템 회전 가능/불가 배지(A 원형 배지)를 어디에 붙일지 — 실험실(FxLab_TotemRotateBadge) 전용.</summary>
public enum TotemRotateBadgeStyle
{
    /// <summary>표시 없음(현재 게임).</summary>
    None = 0,
    /// <summary>"적용 범위" 상자 오른쪽 위 모서리. 선택지 개요는 범위 상자가 없어 아이콘 모서리.</summary>
    RangeCorner = 1,
    /// <summary>토템 이름 바로 오른쪽.</summary>
    NameSide = 2,
    /// <summary>토템 아이콘 모서리(정보창은 등급 배지 반대쪽 왼쪽 위).</summary>
    IconCorner = 3,
    /// <summary>"적용 범위" 글자 왼쪽에 작은 아이콘. 선택지 개요는 이름 옆.</summary>
    RangeLabel = 4,
}
