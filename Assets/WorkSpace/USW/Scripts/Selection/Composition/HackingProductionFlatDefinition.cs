using System;

/// <summary>델탕 해킹 생산 가산 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class HackingProductionFlatDefinition : SelectionEffectDefinition
{
    /// <summary>생산 스택 개수.</summary>
    public int StackCount;
}
