using UnityEngine;

/// <summary>
/// 드론 버퍼 — 배치 시 드론 1마리 생성, 스킬 발동 시 모든 드론에 버프 적용.
/// 유닛 제거 시 소속 드론을 풀로 반환한다.
/// </summary>
public class DroneBuffer : UnitBase
{
    [SerializeField] private DroneBufferData[] _dataByTier; // 0=Normal 1=Rare 2=Epic 3=Legend

    private DroneBufferData Data =>
        unitData != null && _dataByTier != null && (int)unitData.unitTier < _dataByTier.Length
            ? _dataByTier[(int)unitData.unitTier] : null;

    private DroneUnit _ownedDrone;

    protected override void OnUnitPlaced()
    {
        if (Data == null || Manager.DronePool == null) return;
        _ownedDrone = Manager.DronePool.GetDrone(Data.droneAtk, Data.droneAttackInterval, transform.position);
    }

    protected override void OnUnitRemoved()
    {
        if (_ownedDrone == null) return;
        Manager.DronePool?.ReturnDrone(_ownedDrone);
        _ownedDrone = null;
    }

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();

        if (Data == null) return;
        Manager.Drone?.ApplyDroneBuff(Data.atkBuffMultiplier, Data.speedBuffMultiplier, Data.buffDuration);
    }
}
