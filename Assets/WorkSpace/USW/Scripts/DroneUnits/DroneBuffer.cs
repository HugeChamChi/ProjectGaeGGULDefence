using UnityEngine;
using VContainer;

/// <summary>
/// 드론 버퍼 — 배치 시 드론 1마리 생성, 스킬 발동 시 모든 드론에 버프 적용.
/// 유닛 제거 시 소속 드론을 풀로 반환한다.
/// </summary>
public class DroneBuffer : DroneSpawnerBase
{
    [Inject] private DroneManager _droneManager;

    [Header("Buffer Settings")]
    [SerializeField] private float atkBuffMultiplier = 1f;
    [SerializeField] private float speedBuffMultiplier = 1f;
    [SerializeField] private float buffDuration = 5f;

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();

        if (unitData == null) return;
        _audioManager?.PlaySFX("05.Drone_Buff");
        _droneManager?.ApplyDroneBuff(atkBuffMultiplier, speedBuffMultiplier, buffDuration);
    }
}
