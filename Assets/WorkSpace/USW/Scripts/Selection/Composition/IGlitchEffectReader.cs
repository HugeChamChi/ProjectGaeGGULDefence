/// <summary>글리치 반응 설정과 런 공격력 적립의 읽기 전용 계약.</summary>
public interface IGlitchEffectReader
{
    /// <summary>과부하 합성 획득 여부. 성공률0도 비용을 지불하는 활성 설정이다.</summary>
    bool HasOverloadMerge { get; }
    /// <summary>과부하 대상 합성1회의 시간 비용.</summary>
    float OverloadMergeCostSeconds { get; }
    /// <summary>과부하 합성의 추가 승급 확률.</summary>
    float OverloadMergeUpgradeChance { get; }
    /// <summary>디시그망 실제 회복 한 번당 해킹 스택.</summary>
    int RecoveryHackingStacks { get; }
    /// <summary>공유 해킹 저작 데이터.</summary>
    DroneHackingData RecoveryHackingData { get; }
    /// <summary>유닛 글리치당 한 기를 발사할 프리팹. 없으면 미획득.</summary>
    SelfDestructDrone GlitchBombPrefab { get; }
    /// <summary>이번 런에서 적립된 공격력 증가 비율.</summary>
    float GlitchAttackBonus { get; }
}
