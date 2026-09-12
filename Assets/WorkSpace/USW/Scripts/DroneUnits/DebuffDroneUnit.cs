using UnityEngine;

/// <summary>
/// 디버프 드론 소환 유닛.
/// 배치 시 드론 1마리 생성(등급 무관 고정), 스킬 발동 시 보스 피해 증폭 디버프 적용.
/// 유닛 제거 시 소속 드론을 풀로 반환한다.
/// </summary>
public class DebuffDroneUnit : DroneSpawnerBase
{



    protected override void OnSkillFull()
    {

        if (unitData == null) return;
        _audioManager?.PlaySFX("05.Drone_Debuff");
    }
}
