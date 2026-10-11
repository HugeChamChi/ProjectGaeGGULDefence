using System;

/// <summary>합성 지원 확률 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class MergeSupportDefinition : SelectionEffectDefinition
{
    /// <summary>발동 확률(0~1).</summary>
    public float Chance;
}
