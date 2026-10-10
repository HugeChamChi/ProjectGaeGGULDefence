using System;

/// <summary>해킹 초과 생산당 마나 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class OverflowManaDefinition : SelectionEffectDefinition
{
    /// <summary>초과 스택당 마나.</summary>
    public int ManaPerStack;
}
