using System;

/// <summary>베탕 자폭 해킹 전환 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class BombHackingDefinition : SelectionEffectDefinition
{
    /// <summary>폭탄당 해킹 스택.</summary>
    public int StacksPerBomb;
}
