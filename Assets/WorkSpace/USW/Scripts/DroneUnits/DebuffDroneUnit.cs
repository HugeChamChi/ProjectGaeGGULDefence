using UnityEngine;
using VContainer;

/// <summary>
/// 디버프 드론 소환 유닛.
/// 배치 시 드론 1마리 생성(등급 무관 고정), 스킬 발동 시 보스 피해 증폭 디버프 적용.
/// 유닛 제거 시 소속 드론을 풀로 반환한다.
/// </summary>
public class DebuffDroneUnit : DroneSpawnerBase
{
    [Inject] private DroneManager _droneManager;

    [SerializeField] private DebuffDroneData[] _dataByTier; // 0=Normal 1=Rare 2=Epic 3=Legend

    private DebuffDroneData Data =>
        unitData != null && _dataByTier != null && (int)unitData.unitTier < _dataByTier.Length
            ? _dataByTier[(int)unitData.unitTier] : null;

    protected override bool HasValidData() => Data != null;
    protected override float GetDroneAtk() => Data.droneAtk;
    protected override float GetDroneAttackInterval() => Data.droneAttackInterval;

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();

        if (Data == null) return;
        _audioManager?.PlaySFX("05.Drone_Debuff");
        _droneManager?.ApplyBossDebuff(Data.damageAmplificationMultiplier, Data.debuffDuration);
    }
}
