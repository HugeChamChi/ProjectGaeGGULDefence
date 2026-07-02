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

    [Inject] protected DroneManager _droneManager;

    protected Vector2[] SlotOffsets
    {
        get
        {
            float sxL = _droneManager?.droneSpreadXLower ?? 0.25f;
            float syL = _droneManager?.droneSpreadYLower ?? 0.2f;
            float sxU = _droneManager?.droneSpreadXUpper ?? 0.8f;
            float syU = _droneManager?.droneSpreadYUpper ?? 0.7f;

            return new[]
            {
                new Vector2(-sxL,  syL), // 0: 좌하단 (안쪽 아래)
                new Vector2( sxL,  syL), // 1: 우하단 (안쪽 아래)
                new Vector2(-sxU,  syU), // 2: 좌상단 (바깥쪽 위)
                new Vector2( sxU,  syU), // 3: 우상단 (바깥쪽 위)
            };
        }
    }

    protected readonly List<DroneUnit> _ownedDrones = new();

    // 하위 클래스에서 추가 검증이 필요하면 재정의
    protected virtual bool HasValidData() => unitData != null && dronePrefab != null;

    public override bool CanBasicAttack => false;

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
