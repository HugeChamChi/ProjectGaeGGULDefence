using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드론 생산자.
/// 스킬마다 영구 드론 1마리 추가 (최대 maxDroneCount). 자폭 드론은 캡 없이 매 스킬 소환.
/// 유닛 제거 시 소유 드론 전체를 풀로 반환한다.
/// </summary>
public class DroneProducer : UnitBase
{
    [SerializeField] private DroneProducerData[] _dataByTier; // 0=Normal 1=Rare 2=Epic 3=Legend

    private DroneProducerData Data =>
        unitData != null && _dataByTier != null && (int)unitData.unitTier < _dataByTier.Length
            ? _dataByTier[(int)unitData.unitTier] : null;

    private readonly List<DroneUnit> _ownedDrones = new();

    protected override void OnUnitPlaced()
    {
        SpawnOneDrone();
    }

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();
        SpawnOneDrone();

        if (Data == null || Manager.DronePool == null) return;
        for (int i = 0; i < Data.selfDestructCount; i++)
            Manager.DronePool.GetSelfDestruct(Data.selfDestructDamage, transform.position);
    }

    private void SpawnOneDrone()
    {
        if (Data == null || Manager.DronePool == null) return;
        if (_ownedDrones.Count >= Data.maxDroneCount) return;

        var drone = Manager.DronePool.GetDrone(Data.droneAtk, Data.droneAttackInterval, transform.position, transform);
        if (drone != null) _ownedDrones.Add(drone);
    }

    protected override void OnUnitRemoved()
    {
        foreach (var drone in _ownedDrones)
        {
            if (drone != null)
                Manager.DronePool?.ReturnDrone(drone);
        }
        _ownedDrones.Clear();
    }
}
