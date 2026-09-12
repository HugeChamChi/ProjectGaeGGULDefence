/// <summary>부여 순간의 출처·스택·화염구 즉발 피해 전 HP. 시트 데이터와 별개다.</summary>
public readonly struct DebuffApplyContext
{
    /// <summary>런타임 출처 식별값.</summary>
    public int SourceId { get; }
    /// <summary>요청 스택.</summary>
    public int Stacks { get; }
    /// <summary>피해 전 HP의 고정소수 정수 단위.</summary>
    public long SnapshotHpUnits { get; }
    /// <summary>부여 맥락 생성.</summary>
    public DebuffApplyContext(int sourceId, int stacks, long snapshotHpUnits)
    { SourceId = sourceId; Stacks = stacks; SnapshotHpUnits = snapshotHpUnits; }
}
