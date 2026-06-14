using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>
/// 드론을 소환하는 유닛들의 공통 부모 클래스.
/// 드론 소환, 위치 계산(V자 대형), 회수(풀링) 로직을 통합 관리합니다.
/// </summary>
public abstract class DroneSpawnerBase : UnitBase
{
    [Header("드론 배치 설정")]
    [SerializeField] protected DroneUnit dronePrefab;
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

    // 하위 클래스에서 추가 검증이 필요하면 재정의
    protected virtual bool HasValidData() => unitData != null && dronePrefab != null;

    protected override bool CanBasicAttack => false;

    protected override void OnUnitPlaced()
    {
        if (!HasValidData()) return;
        
        for (int i = 0; i < unitData.maxDroneCount; i++)
        {
            SpawnOneDrone();
        }
    }

    protected void SpawnOneDrone()
    {
        if (!HasValidData()) return;
        if (_ownedDrones.Count >= unitData.maxDroneCount) return;

        var slots  = SlotOffsets;
        var offset = _ownedDrones.Count < slots.Length ? slots[_ownedDrones.Count] : slots[0];
        
        var droneObj = RM.Instantiate(dronePrefab.gameObject, transform.position, Quaternion.identity, transform, true);
        if (droneObj != null)
        {
            var drone = droneObj.GetComponent<DroneUnit>();
            if (drone != null)
            {
                drone.Initialize(this, offset);
                
                if (IsFirstPlacement)
                {
                    _audioManager?.PlaySFX("05.Drone_Summon");
                }
                _ownedDrones.Add(drone);
            }
        }
    }

    protected override void OnUnitRemoved()
    {
        foreach (var drone in _ownedDrones)
        {
            if (drone != null)
                RM.Destroy(drone.gameObject);
        }
        _ownedDrones.Clear();
    }
}
