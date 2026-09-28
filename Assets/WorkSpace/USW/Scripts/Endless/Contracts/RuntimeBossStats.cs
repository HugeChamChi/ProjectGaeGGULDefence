/// <summary>원본 SO를 변경하지 않고 보스 소환기에 전달하는 이번 라운드의 최종 수치.</summary>
public readonly struct RuntimeBossStats
{
    /// <summary>외형/표시의 원본 BossData.</summary>
    public BossData Source { get; }
    /// <summary>양수이며 CombatHealth.MaximumHp 이하여야 하는 최종 HP.</summary>
    public decimal MaxHp { get; }
    /// <summary>로그 방어식에 전달할 0 이상의 유한한 방어력.</summary>
    public double Defense { get; }
    /// <summary>이번 보스의 총 경험치. 소환기는 이 값 / MaxHp로 데미지당 EXP를 구한다.</summary>
    public float ExpReward { get; }
    /// <summary>화면에 사용할 1 이상의 체력줄 수.</summary>
    public int HpLineCount { get; }

    /// <summary>B의 성장 계산기가 검증한 소환 수치를 구성한다.</summary>
    public RuntimeBossStats(BossData source, decimal maxHp, double defense, float expReward, int hpLineCount)
    {
        Source = source;
        MaxHp = maxHp;
        Defense = defense;
        ExpReward = expReward;
        HpLineCount = hpLineCount;
    }
}
