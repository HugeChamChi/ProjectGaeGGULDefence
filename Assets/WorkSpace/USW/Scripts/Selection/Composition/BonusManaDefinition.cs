using System;

/// <summary>일반 공격 추가 마나 확률 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class BonusManaDefinition : SelectionEffectDefinition
{
    /// <summary>발동 확률(0~1).</summary>
    public float Chance;
    /// <summary>추가 마나.</summary>
    public int ManaCount;
}
