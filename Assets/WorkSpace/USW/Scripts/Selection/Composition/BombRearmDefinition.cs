using System;

/// <summary>자폭 선택지 BombRearm의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class BombRearmDefinition : SelectionEffectDefinition
{
    /// <summary>BombRearmHits 설정.</summary>
    public int HitsRequired = 5;
    /// <summary>BombRearmExtraBombs 설정.</summary>
    public int ExtraBombs = 2;
}
