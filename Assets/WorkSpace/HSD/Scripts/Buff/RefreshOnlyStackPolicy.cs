using System;

/// <summary>스택 없음 — 재적용 시 지속시간만 갱신되고 스택 수는 항상 1로 유지된다.</summary>
[Serializable]
[DisplayName("갱신만(스택 없음)")]
public class RefreshOnlyStackPolicy : IBuffStackPolicy
{
    public bool RefreshDurationOnReapply => true;

    public int OnReapply(int currentStack) => 1;

    public int Clamp(int stack) => 1;
}
