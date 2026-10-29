using System;

/// <summary>젤탕 배치 시 웨이브 클리어 잔고에 적용하는 전장 공통 식량 배당.</summary>
[Serializable]
public sealed class WaveFoodDividendDefinition : SelectionEffectDefinition
{
    /// <summary>현재 보유 식량에 곱하는 배당 비율. 지급 상한은 없다.</summary>
    public float Ratio;
}
