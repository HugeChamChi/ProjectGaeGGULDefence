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

    protected virtual Vector2[] SlotOffsets
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
    private DroneSelectionState _subscribedSelections;
    private bool _dronesPlaced;

    /// <summary>실제 소유 등급이 에픽/전설인 소환자의 전투 드론 상한. 임시 등급 상승은 해금하지 않는다.</summary>
    public int CombatDroneCapacity => unitData == null ? 0 : unitData.maxDroneCount.Get(currentTier)
        + ((OriginalTier == Tier.Epic || OriginalTier == Tier.Legend)
            ? Mathf.Max(0, DroneSelections?.Get(DroneSelectionKind.ExtraCombatDrone)?.Count ?? 0) : 0);

    /// <summary>현재 소유한 전투 드론 수.</summary>
    public int OwnedDroneCount => _ownedDrones.Count;

    /// <inheritdoc />
    public override float GetDisplayAttackDamage()
    {
        float total = 0f;
        foreach (var drone in _ownedDrones)
            if (drone != null && drone.isActiveAndEnabled && drone.Owner == this)
                total += drone.NonCriticalAttackDamage;
        return total;
    }

    /// <inheritdoc />
    public override float GetDisplayAttackInterval()
    {
        foreach (var drone in _ownedDrones)
            if (drone != null && drone.isActiveAndEnabled && drone.Owner == this)
                return drone.EffectiveAttackInterval;
        return base.GetDisplayAttackInterval();
    }

    // 하위 클래스에서 추가 검증이 필요하면 재정의
    protected virtual bool HasValidData() => unitData != null && dronePrefab != null;

    public override bool CanBasicAttack => false;

    protected override void OnUnitPlaced()
    {
        _dronesPlaced = true;
        UnsubscribeSelections();
        _subscribedSelections = DroneSelections;
        if (_subscribedSelections != null) _subscribedSelections.OnChanged += RefreshCombatDrones;
        RefreshCombatDrones();
    }

    /// <inheritdoc />
    protected override void OnCombatTierChanged()
    {
        if (_dronesPlaced) RefreshCombatDrones();
    }

    /// <summary>선택지 상한까지 보충/회수한다. 본체 스킬은 실행하지 않는다.</summary>
    public void RefreshCombatDrones()
    {
        if (currentCell == null || !HasValidData()) return;
        for (int i = _ownedDrones.Count - 1; i >= CombatDroneCapacity; i--)
        {
            var drone = _ownedDrones[i];
            _ownedDrones.RemoveAt(i);
            if (drone != null) ReleaseCombatDrone(drone);
        }
        int missing = CombatDroneCapacity - _ownedDrones.Count;
        for (int i = 0; i < missing; i++)
        {
            SpawnOneDrone();
        }
    }

    protected void SpawnOneDrone()
    {
        if (!HasValidData()) return;
        if (_ownedDrones.Count >= CombatDroneCapacity) return;

        var slots  = SlotOffsets;
        var offset = _ownedDrones.Count < slots.Length ? slots[_ownedDrones.Count] : slots[0];
        
        var drone = CreateCombatDrone();
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

    /// <summary>
    /// 리소스 풀을 통해 소유 전투 드론을 생성한다. 오너를 부모로 잡지 않는다 — 오너에 Anim_Breathing 같은
    /// 스케일 트윈이 붙으면 자식인 드론까지 같이 늘었다 줄었다 하며 대형이 흔들리는 문제가 있었다.
    /// 위치는 DroneUnit.HomePosition이 오너 좌표를 직접 참조해 계산하므로 부모 연결 없이도 정확하다.
    /// </summary>
    protected virtual DroneUnit CreateCombatDrone()
    {
        var obj = RM.Instantiate(dronePrefab.gameObject, transform.position, Quaternion.identity, null, true);
        return obj != null ? obj.GetComponent<DroneUnit>() : null;
    }

    /// <summary>소유 전투 드론을 리소스 풀로 반환한다.</summary>
    protected virtual void ReleaseCombatDrone(DroneUnit drone) => RM.Destroy(drone.gameObject);

    /// <summary>소유 드론 전체에 스킬 발동 액션 스프라이트를 동시 재생한다(감망 버프/델탕 디버프/베탕 자폭드론 발사 시 호출).</summary>
    protected void FlashOwnedDrones()
    {
        foreach (var drone in _ownedDrones)
            drone?.PlayActionFlash();
    }

    protected override void OnUnitRemoved()
    {
        _dronesPlaced = false;
        UnsubscribeSelections();
        foreach (var drone in _ownedDrones)
        {
            if (drone != null)
                ReleaseCombatDrone(drone);
        }
        _ownedDrones.Clear();
    }

    private void OnDisable() => UnsubscribeSelections();

    private void UnsubscribeSelections()
    {
        if (_subscribedSelections != null) _subscribedSelections.OnChanged -= RefreshCombatDrones;
        _subscribedSelections = null;
    }
}
