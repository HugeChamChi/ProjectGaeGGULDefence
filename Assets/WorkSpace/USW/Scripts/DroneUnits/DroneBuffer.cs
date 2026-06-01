using UnityEngine;
using VContainer;

/// <summary>
/// 드론 버퍼 — 배치 시 드론 1마리 생성, 스킬 발동 시 모든 드론에 버프 적용.
/// 유닛 제거 시 소속 드론을 풀로 반환한다.
/// </summary>
public class DroneBuffer : UnitBase
{
    [Inject] private DronePool _dronePoolManager;
    [Inject] private DroneManager _droneManager;

    [SerializeField] private DroneBufferData[] _dataByTier; // 0=Normal 1=Rare 2=Epic 3=Legend

    private DroneBufferData Data =>
        unitData != null && _dataByTier != null && (int)unitData.unitTier < _dataByTier.Length
            ? _dataByTier[(int)unitData.unitTier] : null;

    private DroneUnit _ownedDrone;

    protected override void OnUnitPlaced()
    {
        if (Data == null || _dronePoolManager == null) return;
        _ownedDrone = _dronePoolManager.GetDrone(Data.droneAtk, Data.droneAttackInterval, transform.position, transform);
    }

    protected override void OnUnitRemoved()
    {
        if (_ownedDrone == null) return;
        _dronePoolManager?.ReturnDrone(_ownedDrone);
        _ownedDrone = null;
    }

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();

        if (Data == null) return;
        _droneManager?.ApplyDroneBuff(Data.atkBuffMultiplier, Data.speedBuffMultiplier, Data.buffDuration);
    }
}
