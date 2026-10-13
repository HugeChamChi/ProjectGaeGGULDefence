using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
public class GameConfig : ScriptableObject
{
    public int   gridColumns       = 6;
    public int   gridRows          = 4;
    public float countdownSeconds  = 30f;
    [SerializeField, Min(0f)] private float _bossKillBonusSeconds = 30f;
    /// <summary>Seconds added once for each accepted boss defeat; subsequent bosses keep the remaining time.</summary>
    public float BossKillBonusSeconds => Mathf.Max(0f, _bossKillBonusSeconds);

    [Header("Endless Boss Time Rewards")]
    [SerializeField, Min(0f)] private float _breatherBossKillBonusSeconds = 10f;
    [SerializeField, Min(0f)] private float _hardBossKillBonusSeconds = 30f;
    /// <summary>Seconds added for an endless Breather boss defeat.</summary>
    public float BreatherBossKillBonusSeconds => Mathf.Max(0f, _breatherBossKillBonusSeconds);
    /// <summary>Seconds added for an endless Hard boss defeat.</summary>
    public float HardBossKillBonusSeconds => Mathf.Max(0f, _hardBossKillBonusSeconds);

    /// <summary>Captures the completed round's authored role and time reward; null mode preserves finite/tutorial rewards.</summary>
    public bool TryGetBossKillBonusSeconds(EndlessModeData mode, int round, out EndlessBossRole role, out float seconds, out string error)
    {
        role = EndlessBossRole.Unspecified;
        seconds = 0f;
        error = "";
        if (mode == null) { seconds = BossKillBonusSeconds; return true; }
        if (round < 1 || mode.CycleLength < 1 || mode.Slots == null)
        { error = "Invalid endless reward round or cycle."; return false; }
        int position = (round - 1) % mode.CycleLength + 1;
        foreach (var slot in mode.Slots)
        {
            if (slot == null || slot.Position != position) continue;
            role = slot.Role;
            switch (role)
            {
                case EndlessBossRole.Breather: seconds = BreatherBossKillBonusSeconds; return true;
                case EndlessBossRole.Hard: seconds = HardBossKillBonusSeconds; return true;
                default: error = $"Round {round}: unspecified endless boss reward role."; return false;
            }
        }
        error = $"Round {round}: no endless boss reward slot at position {position}.";
        return false;
    }
    [Tooltip("타이머 보너스 카운트업 완료 후 처치 공지까지의 실제 시간")]
    [SerializeField, Min(0f)] private float _bossClearNoticeDelaySeconds = 0.4f;
    /// <summary>Unscaled delay between the completed timer addition and the clear notice.</summary>
    public float BossClearNoticeDelaySeconds => Mathf.Max(0f, _bossClearNoticeDelaySeconds);
    public float bossSpawnDelaySeconds = 5f;
    public float cellSize          = 1.5f;

    [Header("Totem Rewards (finite stages)")]
    [SerializeField, Min(1)] private int _totemRewardCycleLength = 10;
    [SerializeField] private int[] _totemRewardPositions = { 3, 6, 10 };
    /// <summary>유한 스테이지의 토템 보상 구간. 무한 모드는 해당 모드 SO의 어려운 보스 역할을 사용한다.</summary>
    public bool GrantsFiniteTotemReward(int round)
    {
        if (round < 1 || _totemRewardCycleLength < 1 || _totemRewardPositions == null) return false;
        int position = (round - 1) % _totemRewardCycleLength + 1;
        return System.Array.IndexOf(_totemRewardPositions, position) >= 0;
    }

    [Header("Economy")]
    public float startingFood = 20f;   // food given at game start
    public float placeCost    = 10f;   // cost to place one unit

    [Header("Combat")]
    [Tooltip("모든 유닛·드론 공통 기본 치명타 확률 (0~1). 레벨업·토템 보너스는 이 위에 더한다.\n" +
             "엑셀 GameBalance.xlsx CombatCommon!baseCritChancePct → Tools/USW/Balance/CombatCommon Excel Import")]
    [Range(0f, 1f)] public float baseCritChance = 0f;
    [Tooltip("실제 타격 피해 편차 (0.1 = 0.9~1.1배 고르게, 평균 1이라 기대 피해·밸런스 불변). 같은 유닛이 매번 똑같은 숫자를 내지 않게 한다.\n" +
             "엑셀 GameBalance.xlsx CombatCommon!damageVariancePct → Tools/USW/Balance/CombatCommon Excel Import")]
    [Range(0f, 0.5f)] public float damageVariance = 0f;
    [Tooltip("치명타 배율 편차 (0.1 = 치명 배율 × 0.9~1.1, 평균 유지). 치명타 숫자끼리도 매번 달라진다.\n" +
             "엑셀 GameBalance.xlsx CombatCommon!critDamageVariancePct → Tools/USW/Balance/CombatCommon Excel Import")]
    [Range(0f, 0.5f)] public float critDamageVariance = 0f;

    [Header("Visuals")]
    public UnitTierPalette tierPalette; // 등급별 비주얼 설정
    [Header("Defeat Presentation")]
    [Tooltip("패배 판정 이후 결과창까지의 실제 시간. 연출 초깃값이며 SO에서 조정합니다.")]
    [SerializeField, Min(0f)] private float _defeatDuration = 0.8f;
    [SerializeField, Range(0.01f, 1f)] private float _defeatTimeScale = 0.2f;
    /// <summary>Unscaled seconds before the fully paused result screen.</summary>
    public float DefeatDuration => Mathf.Max(0f, _defeatDuration);
    /// <summary>Speed of remaining battle visuals after defeat is committed.</summary>
    public float DefeatTimeScale => Mathf.Clamp(_defeatTimeScale, 0.01f, 1f);
}
