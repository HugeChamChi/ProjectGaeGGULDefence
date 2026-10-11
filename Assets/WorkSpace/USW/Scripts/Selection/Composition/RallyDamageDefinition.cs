using System;

/// <summary>족장 집결 피해 가산 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class RallyDamageDefinition : SelectionEffectDefinition
{
    /// <summary>가산 비율(0.2 = 20%).</summary>
    public float BonusRatio;
}
