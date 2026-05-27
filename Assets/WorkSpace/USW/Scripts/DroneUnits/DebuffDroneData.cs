using UnityEngine;

/// <summary>
/// 디버프 드론 등급별 설정.
/// damageAmplificationMultiplier = 1.52 → 보스 받는 피해 +52%
/// </summary>
[CreateAssetMenu(fileName = "DebuffDroneData", menuName = "Game/DroneUnits/DebuffDroneData")]
public class DebuffDroneData : ScriptableObject
{
    [Header("디버프 수치")]
    [Tooltip("1.0 = 기본, 1.52 = +52% 피해 증폭")]
    public float damageAmplificationMultiplier = 1f;

    [Header("지속 시간 (초)")]
    public float debuffDuration;

    [Header("소속 드론 스탯 (배치 시 생성)")]
    public float droneAtk            = 8f;
    public float droneAttackInterval = 1.5f;
}
