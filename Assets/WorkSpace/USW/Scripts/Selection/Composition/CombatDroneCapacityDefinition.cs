using System;

/// <summary>실제 에픽 이상 전투 드론 상한 가산 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class CombatDroneCapacityDefinition : SelectionEffectDefinition
{
    /// <summary>추가 개수.</summary>
    public int Count;
}
