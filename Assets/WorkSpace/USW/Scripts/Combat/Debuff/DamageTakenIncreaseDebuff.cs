/// <summary>한 슬롯을 덮어쓰고 지속시간을 갱신하는 받피증.</summary>
public sealed class DamageTakenIncreaseDebuff : DebuffInstance
{
    /// <summary>불변 정의 주입.</summary>
    public DamageTakenIncreaseDebuff(DebuffDefinition definition) : base(definition) { }
    /// <inheritdoc />
    public override void Reapply(double now, DebuffApplyContext context)
    { SourceId = context.SourceId; Stacks = 1; ExpiresAt = now + Definition.Duration; }
}
