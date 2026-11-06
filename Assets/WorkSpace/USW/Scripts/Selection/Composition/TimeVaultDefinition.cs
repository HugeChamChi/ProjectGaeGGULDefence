using System;

/// <summary>C06 시간 금고의 런 1회 유예.</summary>
[Serializable]
public sealed class TimeVaultDefinition : SelectionEffectDefinition
{
    /// <summary>시간초과 대신 허용하는 마지막 실시간 전투 구간.</summary>
    public float GraceSeconds = 15f;
}
