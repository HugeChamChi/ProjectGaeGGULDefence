using System;

/// <summary>HP 1 = 10,000 정수 단위. 저장과 차감에 부동소수 연산을 사용하지 않는다.</summary>
public sealed class CombatHealth
{
    /// <summary>확정된 소수 4자리 저장 배율.</summary>
    public const long Scale = 10000;
    /// <summary>정확히 저장 가능한 최대 HP.</summary>
    public const decimal MaximumHp = (decimal)long.MaxValue / Scale;
    /// <summary>현재 고정소수 정수 단위.</summary>
    public long CurrentUnits { get; private set; }
    /// <summary>최대 고정소수 정수 단위.</summary>
    public long MaxUnits { get; private set; }
    /// <summary>표시/게임 이벤트에 전달하는 정확한 HP.</summary>
    public decimal Current => (decimal)CurrentUnits / Scale;
    /// <summary>정확한 최대 HP.</summary>
    public decimal Max => (decimal)MaxUnits / Scale;
    /// <summary>사망은 정수 0으로 판정한다.</summary>
    public bool IsDead => CurrentUnits == 0;
    /// <summary>십진 입력을 최종 경계에서 반올림한다. 범위 초과는 거부한다.</summary>
    public static long ToUnits(decimal hp)
    {
        if (hp < 0 || hp > MaximumHp) throw new ArgumentOutOfRangeException(nameof(hp));
        return checked((long)decimal.Round(hp * Scale, 0, MidpointRounding.AwayFromZero));
    }
    /// <summary>새 보스/재사용 시 초기화.</summary>
    public void Reset(decimal hp)
    {
        long units = ToUnits(hp);
        if (units == 0) throw new ArgumentOutOfRangeException(nameof(hp));
        CurrentUnits = MaxUnits = units;
    }
    /// <summary>남은 HP 이하의 실제 차감량 반환.</summary>
    public long ApplyDamage(long units)
    {
        if (units < 0) throw new ArgumentOutOfRangeException(nameof(units));
        long actual = Math.Min(CurrentUnits, units);
        CurrentUnits -= actual;
        return actual;
    }
}
