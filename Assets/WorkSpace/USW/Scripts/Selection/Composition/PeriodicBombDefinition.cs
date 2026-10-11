using System;

/// <summary>베탕 정기 자폭 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class PeriodicBombDefinition : SelectionEffectDefinition
{
    /// <summary>재생 간격(초).</summary>
    public float PeriodSeconds;
    /// <summary>폭탄 개수.</summary>
    public int BombCount;
}
