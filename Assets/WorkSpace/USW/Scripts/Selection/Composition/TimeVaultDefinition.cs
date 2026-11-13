using System;

/// <summary>C06 비상 전력의 런 1회 유예.</summary>
[Serializable]
public sealed class TimeVaultDefinition : SelectionEffectDefinition
{
    /// <summary>시간초과 대신 허용하는 마지막 전투 시계 구간.</summary>
    public float GraceSeconds = 15f;
}
