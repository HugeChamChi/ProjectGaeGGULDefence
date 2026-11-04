using System;

/// <summary>자폭 선택지 GammanBomb의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class GammanBombDefinition : SelectionEffectDefinition
{
    /// <summary>운반할 실제 공유 자폭 프리팹.</summary>
    public SelfDestructDrone Prefab;
}
