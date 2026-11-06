using System;

/// <summary>C02 가속 운전의 타이머 추가 소모와 임시 공속.</summary>
[Serializable]
public sealed class AcceleratedDriveDefinition : SelectionEffectDefinition
{
    /// <summary>기본 타이머 감소량에 더할 비율.</summary>
    public float ExtraDrainRatio = 0.1f;
    /// <summary>임시 밸런스: 모든 본체와 소유 전투 드론의 공속 증가율.</summary>
    public float AttackSpeedBonus = 0.2f;
}
