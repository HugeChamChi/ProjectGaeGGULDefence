using System;

/// <summary>공유 해킹 상한에 한 번 더하는 선택지 보너스.</summary>
[Serializable]
public sealed class HackingCapacityDefinition : SelectionEffectDefinition
{
    /// <summary>공유 해킹 상한에 한 번 더하는 선택지 보너스.</summary>
    public int AdditionalCapacity;
}
