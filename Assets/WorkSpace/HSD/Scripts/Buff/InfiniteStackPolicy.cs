using System;

/// <summary>무한 중첩 — 재적용마다 스택 +1, 상한 없음.</summary>
[Serializable]
public class InfiniteStackPolicy : IBuffStackPolicy
{
    public bool refreshDurationOnReapply = true;
    public bool RefreshDurationOnReapply => refreshDurationOnReapply;

    public int OnReapply(int currentStack) => currentStack + 1;

    public int Clamp(int stack) => stack < 1 ? 1 : stack;
}
