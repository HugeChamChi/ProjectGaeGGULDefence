using System;

/// <summary>군단 정기 폭탄 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class FleetBombDefinition : SelectionEffectDefinition
{
    /// <summary>재생 간격(초).</summary>
    public float PeriodSeconds;
    /// <summary>폭탄 개수.</summary>
    public int BombCount;
}
