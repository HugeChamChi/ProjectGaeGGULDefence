using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>
/// 드론을 소환하는 유닛들의 공통 부모 클래스.
/// 드론 소환, 위치 계산(V자 대형), 회수(풀링) 로직을 통합 관리합니다.
/// </summary>
public abstract class DroneSpawnerBase : UnitBase
{
    [Inject] protected DronePool _dronePoolManager;

    [Header("드론 배치 설정")]
    [SerializeField] protected float _spreadX      = 35f;
    [SerializeField] protected float _spreadXUpper = 50f;
    [SerializeField] protected float _spreadY      = 20f;

    protected Vector2[] SlotOffsets => new[]
    {
        new Vector2(-_spreadX,      -_spreadY), // 0: 좌하단
        new Vector2( _spreadX,      -_spreadY), // 1: 우하단
        new Vector2(-_spreadXUpper,  _spreadY), // 2: 좌중단
        new Vector2( _spreadXUpper,  _spreadY), // 3: 우중단
    };

    protected readonly List<DroneUnit> _ownedDrones = new();

    // 하위 클래스에서 제공해야 하는 데이터
    protected abstract bool HasValidData();
    protected abstract float GetDroneAtk();
    protected abstract float GetDroneAttackInterval();

    protected override void OnUnitPlaced()
    {
        if (unitData == null || _dronePoolManager == null || !HasValidData()) return;
        
        for (int i = 0; i < unitData.maxDroneCount; i++)
        {
            SpawnOneDrone();
        }
    }

    protected void SpawnOneDrone()
    {
        if (unitData == null || _dronePoolManager == null || !HasValidData()) return;
        if (_ownedDrones.Count >= unitData.maxDroneCount) return;

        var slots  = SlotOffsets;
        var offset = _ownedDrones.Count < slots.Length ? slots[_ownedDrones.Count] : slots[0];
        var drone  = _dronePoolManager.GetDrone(GetDroneAtk(), GetDroneAttackInterval(), transform.position, transform, offset);
        
        if (drone != null)
        {
            if (IsFirstPlacement)
            {
                _audioManager?.PlaySFX("05.Drone_Summon");
            }
            _ownedDrones.Add(drone);
        }
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
