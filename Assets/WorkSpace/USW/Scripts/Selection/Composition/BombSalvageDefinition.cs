using System;

/// <summary>자폭 선택지 BombSalvage의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class BombSalvageDefinition : SelectionEffectDefinition
{
    /// <summary>BombSalvageHits 설정.</summary>
    public int HitsRequired = 10;
    /// <summary>BombSalvageFood 설정.</summary>
    public float FoodAmount = 10f;
}
