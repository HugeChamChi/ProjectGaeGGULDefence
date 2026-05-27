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

    [SerializeField] private float _droneSpreadRadius = 35f;

    private readonly List<DroneUnit> _ownedDrones = new();

    protected override void OnUnitPlaced()
    {
        if (Data == null) return;
        for (int i = 0; i < Data.maxDroneCount; i++)
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

        var offset = CalcSlotOffset(_ownedDrones.Count, Data.maxDroneCount);
        var drone  = Manager.DronePool.GetDrone(Data.droneAtk, Data.droneAttackInterval, transform.position, transform, offset);
        if (drone != null) _ownedDrones.Add(drone);
    }

    /// <summary>총 total 개 슬롯을 원형으로 균등 배치. index 번째 슬롯 오프셋 반환.</summary>
    private Vector2 CalcSlotOffset(int index, int total)
    {
        // 3개: 90° 시작 → 꼭짓점이 위인 정삼각형
        // 4개: 90° 시작 → 다이아몬드, 반지름 1.4배로 더 벌림
        float startDeg = 90f;
        float radius   = total >= 4 ? _droneSpreadRadius * 1.4f : _droneSpreadRadius;
        float angle    = Mathf.Deg2Rad * (startDeg + 360f / Mathf.Max(total, 1) * index);

        return new Vector2(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius * 0.6f
        );
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
