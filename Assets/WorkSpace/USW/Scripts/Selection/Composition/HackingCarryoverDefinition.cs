using System;

/// <summary>보스 교체 해킹 이월 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class HackingCarryoverDefinition : SelectionEffectDefinition
{
    /// <summary>이월 비율.</summary>
    public float Ratio;
}
