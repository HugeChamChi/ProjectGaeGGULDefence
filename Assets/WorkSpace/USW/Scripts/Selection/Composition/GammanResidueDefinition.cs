using System;

/// <summary>감망 기폭 후 스택 추가 피해의 일부를 여러 틱으로 나눠 주는 여운 설정(9109 넹...). 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class GammanResidueDefinition : SelectionEffectDefinition
{
    /// <summary>기폭 피해 중 스택 추가분에 곱하는 여운 총량 비율.</summary>
    public float DamageRatio;
    /// <summary>여운이 이어지는 시간(초).</summary>
    public float DurationSeconds;
    /// <summary>여운 피해를 나누는 틱 수.</summary>
    public int TickCount;
}
