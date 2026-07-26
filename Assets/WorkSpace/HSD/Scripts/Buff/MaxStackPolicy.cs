using System;

/// <summary>최대 스택 제한 — 재적용마다 스택 +1, maxStacks에서 잘린다.</summary>
[Serializable]
[DisplayName("최대 스택 제한")]
public class MaxStackPolicy : IBuffStackPolicy
{
    public int maxStacks = 1;
    public bool refreshDurationOnReapply = true;
    public bool RefreshDurationOnReapply => refreshDurationOnReapply;

    public int OnReapply(int currentStack) => currentStack + 1;

    public int Clamp(int stack)
    {
        if (stack < 1) return 1;
        return maxStacks > 0 && stack > maxStacks ? maxStacks : stack;
    }
}
