using System.Collections.Generic;

/// <summary>동일 유닛 종류의 모든 개체/등급에서 발생한 최종 피해 합산.</summary>
public sealed class DamageContributionEntry
{
    private readonly Queue<Hit> _recent = new Queue<Hit>();
    private decimal _recentDamage;

    /// <summary>종류를 대표하는 유닛 데이터.</summary>
    public UnitData Unit { get; }
    /// <summary>현재 배치된 개체 수. 피해 누적 기록과 독립이다.</summary>
    public int UnitCount { get; internal set; }
    /// <summary>누적 최종 피해. decimal 정확도를 유지한다.</summary>
    public decimal TotalDamage { get; private set; }

    internal DamageContributionEntry(UnitData unit) => Unit = unit;
    internal void Add(decimal damage, double time)
    {
        TotalDamage += damage;
        _recentDamage += damage;
        _recent.Enqueue(new Hit(time, damage));
    }
    internal void Advance(double time, double window)
    {
        while (_recent.Count > 0 && _recent.Peek().Time <= time - window)
            _recentDamage -= _recent.Dequeue().Damage;
    }
    internal void Clear() { TotalDamage = 0m; _recentDamage = 0m; _recent.Clear(); }

    /// <summary>최근 전투 시간창의 초당 피해. 일시정지 시간은 포함하지 않는다.</summary>
    public decimal RecentDps(double combatSeconds, double window)
    {
        double duration = System.Math.Min(combatSeconds, window);
        return duration > 0 ? _recentDamage / (decimal)duration : 0m;
    }

    private readonly struct Hit
    {
        internal readonly double Time;
        internal readonly decimal Damage;
        internal Hit(double time, decimal damage) { Time = time; Damage = damage; }
    }
}
