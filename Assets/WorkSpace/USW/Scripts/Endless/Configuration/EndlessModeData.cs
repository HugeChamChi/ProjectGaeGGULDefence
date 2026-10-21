using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>난이도별 무한 설정. 활성화 전에 EndlessConfigurationValidator로 검사한다.</summary>
[CreateAssetMenu(fileName = "EndlessModeData", menuName = "Game/Endless/Mode")]
public sealed class EndlessModeData : ScriptableObject
{
    [SerializeField] private string _key;
    [SerializeField] private bool _enabled;
    [SerializeField] private int _cycleLength = 10;
    [SerializeField] private int _penaltyInterval = 10;
    [SerializeField] private int _penaltyFirstApplyRound = 11;
    [SerializeField] private int _penaltyDrawCount = 2;
    [SerializeField] private EndlessBossSlot[] _slots = Array.Empty<EndlessBossSlot>();
    [SerializeField] private EndlessGrowthSettings _growth = new();
    [SerializeField] private RunPenaltyPoolData _penaltyPool;
    [SerializeField] private EndlessBossSelection _bossSelection;
    [SerializeField] private EndlessBossPoolData _breatherBossPool;
    [SerializeField] private EndlessBossPoolData _hardBossPool;
    [SerializeField, Tooltip("Offline preview/legacy authoring only. Production NORMAL ignores these numeric values and requires the backend snapshot.")]
    private EndlessBossProgressionData _bossProgression;

    /// <summary>난이도 고정 키. 기본 제작 프로필은 NORMAL/HARD.</summary>
    public string Key => _key;
    /// <summary>Normal and the paid boss-test scene share the required NORMAL backend snapshot.</summary>
    public bool RequiresRemoteBalance => _key == "NORMAL" || _key == "HARD_INGAME";
    /// <summary>제작 활성 요청 여부. true여도 검증 실패 시 시작하지 않는다.</summary>
    public bool Enabled => _enabled;
    /// <summary>보스 순환 라운드 수.</summary>
    public int CycleLength => _cycleLength;
    /// <summary>패널티 추첨 경계 간격.</summary>
    public int PenaltyInterval => _penaltyInterval;
    /// <summary>첫 패널티가 활성화되는 1 기반 라운드. 추첨은 직전 라운드 완료 후.</summary>
    public int PenaltyFirstApplyRound => _penaltyFirstApplyRound;
    /// <summary>경계당 제시 후보 수. 현재 계약은 서로 다른 2개 중 1개 선택이다.</summary>
    public int PenaltyDrawCount => _penaltyDrawCount;
    /// <summary>위치별 보스 참조. Position 기준으로 조회하며 배열 순서에 의존하지 않는다.</summary>
    public IReadOnlyList<EndlessBossSlot> Slots => _slots;
    /// <summary>라운드 기반 성장 입력.</summary>
    public EndlessGrowthSettings Growth => _growth;
    /// <summary>사용할 패널티 풀.</summary>
    public RunPenaltyPoolData PenaltyPool => _penaltyPool;
    /// <summary>Explicit fixed-cycle, legacy fixed-opening, or first-round shuffle policy.</summary>
    public EndlessBossSelection BossSelection => _bossSelection;
    /// <summary>Ordinary boss candidates; role-only profiles use this pool starting at round one.</summary>
    public EndlessBossPoolData BreatherBossPool => _breatherBossPool;
    /// <summary>Difficult boss candidates for the role schedule.</summary>
    public EndlessBossPoolData HardBossPool => _hardBossPool;
    /// <summary>Endless-only HP/defense/bar rules; fixed stages and tutorials retain their BossData values.</summary>
    public EndlessBossProgressionData BossProgression => _bossProgression;
}
