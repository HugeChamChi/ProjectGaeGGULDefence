using UnityEngine;

/// <summary>딜표 실험실의 예시 전투와 갱신 주기 설정.</summary>
[CreateAssetMenu(menuName = "USW/UI Lab/Damage Chart Settings")]
public sealed class DamageChartLabSettings : ScriptableObject
{
    /// <summary>종류별 예시 유닛.</summary>
    public DamageChartSample[] Samples;
    /// <summary>최근 DPS 집계 창(전투 초).</summary>
    [Min(.1f)] public float RecentDpsWindow = 5f;
    /// <summary>예시 공격 묶음 간격.</summary>
    [Min(.02f)] public float HitInterval = .22f;
    /// <summary>화면 데이터 갱신 간격.</summary>
    [Min(.05f)] public float RefreshInterval = .16f;
    /// <summary>첫 화면을 준비하는 예시 전투 시간.</summary>
    [Min(0f)] public float WarmupSeconds = 12f;
    /// <summary>순위 역전을 보여 주기 위한 추가 예시 피해량.</summary>
    [Min(1f)] public float BurstDamage = 42000f;
}
