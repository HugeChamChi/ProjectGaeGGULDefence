using System;
using UnityEngine;

/// <summary>
/// 특정 조건을 만족할 때만 내부 버프(buff)를 적용하는 기능.
/// </summary>
[Serializable]
[DisplayName("조건부 버프")]
public class ConditionalBuffFunction : ITotemFunction
{
    [SerializeReference, SelectableReference]
    public ITotemCondition condition;
    public SimpleBuffFunction buff = new SimpleBuffFunction();

    public void Apply(TotemBase totem, GridCell cell, TotemBuffManager buffManager)
    {
        if (condition == null || buff == null) return;
        if (condition.IsMet(totem, cell))
            buff.Apply(totem, cell, buffManager);
    }
}
