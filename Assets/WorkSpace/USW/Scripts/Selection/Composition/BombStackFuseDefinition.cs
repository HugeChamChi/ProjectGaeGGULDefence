using System;

/// <summary>자폭 선택지 BombStackFuse의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class BombStackFuseDefinition : SelectionEffectDefinition
{
    /// <summary>BombFuseStacks 설정.</summary>
    public int MaximumStacks = 1;
    /// <summary>BombFuseBonusPerStack 설정.</summary>
    public float BonusPerStack = .15f;
}
