using UnityEngine;
using VContainer;

/// <summary>
/// 드론 버퍼 — 배치 시 드론 1마리 생성, 스킬 발동 시 모든 드론에 버프 적용.
/// 유닛 제거 시 소속 드론을 풀로 반환한다.
/// </summary>
public class Drone_Gamman : DroneSpawnerBase
{


    [Header("Buffer Settings")]
    [SerializeField] private float atkBuffMultiplier = 1f;
    [SerializeField] private float speedBuffMultiplier = 1f;
    [SerializeField] private float buffDuration = 5f;

    protected override void OnSkillFull()
    {
        ApplyDroneBuff();
    }
    /// <summary>일반 스킬 또는 알팡 긴급 교신으로 같은 버프를 적용한다.</summary>
    public void ApplyDroneBuff()
    {
        if (unitData == null) return;
        _audioManager?.PlaySFX("05.Drone_Buff");
        var effect = DroneSelections?.Get(DroneSelectionKind.GammanFrequency);
        float bonus = effect != null ? Mathf.Min(effect.MaxValue, (_droneManager?.DroneCount ?? 0) * effect.Value) : 0f;
        _droneManager?.ApplyDroneBuff(atkBuffMultiplier + bonus, speedBuffMultiplier + bonus, buffDuration);
        FlashOwnedDrones();
    }
}
