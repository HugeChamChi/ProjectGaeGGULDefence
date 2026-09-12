using System;

/// <summary>스냅샷 화상. 갱신 시 다음 틱을 유지하고 마지막 틱에서 정수 잔여를 보정한다.</summary>
public sealed class BurnDebuff : DebuffInstance
{
    private const double TimeBoundaryTolerance = 1e-9;
    private double _nextTick;
    private long _regularTick;
    private long _lastTick;
    private int _ticksLeft;
    /// <summary>불변 정의 주입.</summary>
    public BurnDebuff(DebuffDefinition definition) : base(definition) { }
    /// <inheritdoc />
    public override void Reapply(double now, DebuffApplyContext context)
    {
        bool first = Stacks == 0;
        SourceId = context.SourceId; Stacks = 1;
        if (first) _nextTick = now + Definition.TickInterval;
        ExpiresAt = now + Definition.Duration;
        _ticksLeft = Definition.TickCount;
        decimal total = decimal.Round(context.SnapshotHpUnits * Definition.SnapshotRatio, 0, MidpointRounding.AwayFromZero);
        long totalUnits = checked((long)total);
        decimal minimumTotal = (decimal)Definition.MinTickUnits * Definition.TickCount;
        if (totalUnits <= minimumTotal)
        { _regularTick = _lastTick = Definition.MinTickUnits; return; }
        _regularTick = checked((long)decimal.Round((decimal)totalUnits / Definition.TickCount, 0, MidpointRounding.AwayFromZero));
        decimal last = totalUnits - (decimal)_regularTick * (Definition.TickCount - 1);
        // 마지막 틱 최소값까지 지켜야 하는 경계에서는 앞 틱을 1단위 낮춰 잔여를 보존한다.
        if (last < Definition.MinTickUnits)
        {
            _regularTick = totalUnits / Definition.TickCount;
            last = totalUnits - (decimal)_regularTick * (Definition.TickCount - 1);
        }
        _lastTick = checked((long)last);
    }
    /// <inheritdoc />
    public override void Advance(double now, Action<long> burnDamage, Func<bool> alive)
    {
        while (_ticksLeft > 0 && _nextTick <= now + TimeBoundaryTolerance && _nextTick <= ExpiresAt + TimeBoundaryTolerance && alive())
        {
            long damage = _ticksLeft == 1 ? _lastTick : _regularTick;
            _ticksLeft--;
            _nextTick += Definition.TickInterval;
            burnDamage(damage);
        }
    }
}
