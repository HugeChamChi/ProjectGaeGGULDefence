using System;

/// <summary>획득/필드보기 표시 대상. 게임 효과 실행 권한은 없다.</summary>
[Serializable]
public sealed class SelectionFeedbackDefinition
{
    /// <summary>표시 대상 기능 소유자.</summary>
    public SelectionFeedbackTarget Target;
    /// <summary>유닛 대상 행 범위.</summary>
    public SelectionFeedbackRow Row;
    /// <summary>비어 있으면 모든 부족.</summary>
    public UnitTribe[] Tribes = Array.Empty<UnitTribe>();
    /// <summary>실제 소유 등급이 에픽/전설인 유닛만 대상으로 한다.</summary>
    public bool RequiresHighOwnedTier;
}
