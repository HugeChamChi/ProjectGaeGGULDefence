using System;

/// <summary>C08 과열 압축의 원재료와 비용 및 피해 가중치.</summary>
[Serializable]
public sealed class OverheatCompressionDefinition : SelectionEffectDefinition
{
    /// <summary>과열 스택 한 개를 만드는 초과 생산량.</summary>
    public int OverflowPerStack = 5;
    /// <summary>과열 한 개의 시간 비용.</summary>
    public float CostSeconds = 0.5f;
    /// <summary>과열 스택 한 개의 일반 스택 대비 피해 가중치.</summary>
    public float DamageWeight = 7.5f;
}
