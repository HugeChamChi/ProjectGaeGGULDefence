using System;

/// <summary>보스 생존 동안 유지되는 공유 아머 스택.</summary>
public sealed class ArmorBreakDebuff : DebuffInstance
{
    /// <summary>불변 정의 주입.</summary>
    public ArmorBreakDebuff(DebuffDefinition definition) : base(definition) { }
    /// <inheritdoc />
    public override void Reapply(double now, DebuffApplyContext context)
    {
        SourceId = context.SourceId;
        Stacks += Math.Min(context.Stacks, Definition.MaxStacks - Stacks);
        ExpiresAt = double.PositiveInfinity;
    }
}
