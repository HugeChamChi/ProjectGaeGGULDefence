/// <summary>한 번의 집결이 시작할 때 고정하는 추가 사격 설정.</summary>
public readonly struct ChiefVolleySettings
{
    /// <summary>전체 사격 횟수. 0이면 일반 집결.</summary>
    public int Count { get; }
    /// <summary>사격 사이 대기 시간(초).</summary>
    public float Interval { get; }
    /// <summary>각 사격에 기록할 최종 피해 비율.</summary>
    public float DamageRatio { get; }
    /// <summary>현재 조회값을 복사하여 시전 도중 카드 변경과 독립시킨다.</summary>
    public ChiefVolleySettings(int count, float interval, float damageRatio)
    { Count = count; Interval = interval; DamageRatio = damageRatio; }
}
