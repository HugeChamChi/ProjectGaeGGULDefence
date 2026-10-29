using System;

/// <summary>해킹 초과 생산이 있는 해킹 1회당 마나 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class OverflowManaDefinition : SelectionEffectDefinition
{
    /// <summary>초과가 발생한 해킹 1회당 회복하는 자기 마나.</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("ManaPerStack")]
    public int ManaPerCast;
}
