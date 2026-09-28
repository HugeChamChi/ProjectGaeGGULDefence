/// <summary>한 번의 패널티 추첨 결과. 연출은 이 알림을 사용하고 추첨을 다시 실행하지 않는다.</summary>
public readonly struct RunPenaltyResult
{
    /// <summary>새 런마다 달라지는 런 식별자. 이전 런의 지연 콜백을 거부하는 데 사용한다.</summary>
    public long RunId { get; }
    /// <summary>선택한 효과의 고정 키.</summary>
    public string PenaltyKey { get; }
    /// <summary>선택 후 해당 효과만의 누적 횟수.</summary>
    public int StackCount { get; }
    /// <summary>선택된 효과가 활성화되는 1 기반 다음 라운드.</summary>
    public int EffectiveFromRound { get; }

    /// <summary>이미 확정된 추첨 결과를 구성한다.</summary>
    public RunPenaltyResult(long runId, string penaltyKey, int stackCount, int effectiveFromRound)
    {
        RunId = runId;
        PenaltyKey = penaltyKey;
        StackCount = stackCount;
        EffectiveFromRound = effectiveFromRound;
    }
}
