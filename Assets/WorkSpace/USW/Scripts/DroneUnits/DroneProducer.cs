using System.Collections.Generic;
using VContainer;
using UnityEngine;

/// <summary>
/// 드론 생산자.
/// 스킬마다 영구 드론 1마리 추가 (최대 maxDroneCount). 자폭 드론은 캡 없이 매 스킬 소환.
/// 유닛 제거 시 소유 드론 전체를 풀로 반환한다.
/// </summary>
public class DroneProducer : UnitBase
{
    [Inject] private DronePool _dronePoolManager;

    [SerializeField] private DroneProducerData[] _dataByTier; // 0=Normal 1=Rare 2=Epic 3=Legend

    private DroneProducerData Data =>
        unitData != null && _dataByTier != null && (int)unitData.unitTier < _dataByTier.Length
            ? _dataByTier[(int)unitData.unitTier] : null;

    [SerializeField] private float _spreadX      = 35f;
    [SerializeField] private float _spreadXUpper = 50f; // 위 슬롯 X — 아래보다 넓어서 V자
    [SerializeField] private float _spreadY      = 20f;

    // 소환 순서: 좌하단 → 우하단 → 좌중단 → 우중단
    private Vector2[] SlotOffsets => new[]
    {
        new Vector2(-_spreadX,      -_spreadY), // 0: 좌하단
        new Vector2( _spreadX,      -_spreadY), // 1: 우하단
        new Vector2(-_spreadXUpper,  _spreadY), // 2: 좌중단
        new Vector2( _spreadXUpper,  _spreadY), // 3: 우중단
    };

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

        if (Data == null || _dronePoolManager == null) return;
        for (int i = 0; i < Data.selfDestructCount; i++)
            _dronePoolManager.GetSelfDestruct(Data.selfDestructDamage, transform.position);
    }

    private void SpawnOneDrone()
    {
        if (Data == null || _dronePoolManager == null) return;
        if (_ownedDrones.Count >= Data.maxDroneCount) return;

        var slots  = SlotOffsets;
        var offset = _ownedDrones.Count < slots.Length ? slots[_ownedDrones.Count] : slots[0];
        var drone  = _dronePoolManager.GetDrone(Data.droneAtk, Data.droneAttackInterval, transform.position, transform, offset);
        if (drone != null) _ownedDrones.Add(drone);
    }

    protected override void OnUnitRemoved()
    {
        foreach (var drone in _ownedDrones)
        {
            if (drone != null)
                _dronePoolManager?.ReturnDrone(drone);
        }
        _ownedDrones.Clear();
    }
}
