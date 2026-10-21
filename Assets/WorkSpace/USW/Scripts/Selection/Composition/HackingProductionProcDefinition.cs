using System;

/// <summary>델탕 해킹 1회가 확률로 생산량 배수를 얻는 설정(9108 넹 !). 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class HackingProductionProcDefinition : SelectionEffectDefinition
{
    /// <summary>해킹 1회당 발동 확률.</summary>
    public float Chance;
    /// <summary>발동 시 그 해킹의 생산량 배수.</summary>
    public int Multiplier = 2;
}
