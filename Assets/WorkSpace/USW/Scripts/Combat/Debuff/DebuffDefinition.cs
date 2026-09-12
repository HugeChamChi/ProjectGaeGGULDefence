using System;

/// <summary>시트/SO에서 검증하여 생성한 불변 효과 정의.</summary>
public sealed class DebuffDefinition
{
    /// <summary>시트 고유 ID.</summary>
    public int Id { get; }
    /// <summary>고유 식별명.</summary>
    public string Key { get; }
    /// <summary>실행 종류.</summary>
    public DebuffKind Kind { get; }
    /// <summary>보스 내 공유 슬롯.</summary>
    public string StackGroup { get; }
    /// <summary>시간제 효과 지속시간.</summary>
    public double Duration { get; }
    /// <summary>스택 상한.</summary>
    public int MaxStacks { get; }
    /// <summary>화상 틱 간격.</summary>
    public double TickInterval { get; }
    /// <summary>화상 스냅샷 비율, 0.01 = 1%.</summary>
    public decimal SnapshotRatio { get; }
    /// <summary>화상 틱 최소 고정소수 피해.</summary>
    public long MinTickUnits { get; }
    /// <summary>아머 방어 효과 약화 강도.</summary>
    public double ArmorStrength { get; }
    /// <summary>받는 피해 최종 배율.</summary>
    public decimal DamageMultiplier { get; }
    /// <summary>화상 총 틱 수.</summary>
    public int TickCount { get; }
    /// <summary>잘못된/모순된 정의는 카탈로그 공개 전에 거부한다.</summary>
    public DebuffDefinition(int id, string key, DebuffKind kind, string group, double duration, int maxStacks,
        double tickInterval = 0, decimal snapshotRatio = 0, decimal minTickDamage = 0,
        double armorStrength = 0, decimal damageMultiplier = 1)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(group) ||
            !Enum.IsDefined(typeof(DebuffKind), kind) || maxStacks < 1 || !Finite(duration) || duration < 0 ||
            !Finite(tickInterval) || !Finite(armorStrength) || armorStrength < 0 || damageMultiplier < 1)
            throw new ArgumentException($"Invalid debuff definition: {id}/{key}");
        if (kind != DebuffKind.ArmorBreak && (duration <= 0 || maxStacks != 1))
            throw new ArgumentException($"Timed debuff must have one slot and positive duration: {key}");
        if (kind == DebuffKind.ArmorBreak && duration != 0)
            throw new ArgumentException($"ArmorBreak must use TargetLifetime: {key}");
        if (kind == DebuffKind.Burn)
        {
            double ticks = duration / tickInterval;
            if (tickInterval <= 0 || !Finite(ticks) || ticks < 1 || ticks > int.MaxValue ||
                Math.Abs(ticks - Math.Round(ticks)) > 1e-9 || snapshotRatio <= 0 || snapshotRatio > 1 || minTickDamage < 1)
                throw new ArgumentException($"Invalid burn ticks/ratio/minimum: {key}");
            TickCount = checked((int)Math.Round(ticks));
        }
        Id = id; Key = key; Kind = kind; StackGroup = group; Duration = duration; MaxStacks = maxStacks;
        TickInterval = tickInterval; SnapshotRatio = snapshotRatio; MinTickUnits = CombatHealth.ToUnits(minTickDamage);
        ArmorStrength = armorStrength; DamageMultiplier = damageMultiplier;
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
