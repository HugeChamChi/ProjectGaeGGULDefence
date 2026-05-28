using UnityEngine;

/// <summary>
/// 드론 버퍼 등급별 설정.
/// atkBuffMultiplier = 1.55 → +55% 공격력
/// speedBuffMultiplier = 1.45 → +45% 공격속도 (클수록 빠름)
/// </summary>
[CreateAssetMenu(fileName = "DroneBufferData", menuName = "Game/DroneUnits/DroneBufferData")]
public class DroneBufferData : ScriptableObject
{
    [Header("버프 수치")]
    [Tooltip("1.0 = 기본, 1.55 = +55%")]
    public float atkBuffMultiplier   = 1f;
    [Tooltip("1.0 = 기본, 1.45 = +45% 빠름")]
    public float speedBuffMultiplier = 1f;

    [Header("지속 시간 (초)")]
    public float buffDuration;

    [Header("소속 드론 스탯 (배치 시 생성)")]
    public float droneAtk            = 8f;
    public float droneAttackInterval = 1.5f;
}
