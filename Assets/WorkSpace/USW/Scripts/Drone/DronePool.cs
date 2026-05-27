using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 드론 오브젝트 풀 — InGameSingleton.
/// 일반 드론(50)과 자폭 드론(20) 두 풀을 관리한다.
/// DroneProducer는 Instantiate 대신 이 클래스를 통해 드론을 가져온다.
/// </summary>
public class DronePool : InGameSingleton<DronePool>
{
    [Header("프리팹")]
    [SerializeField] private DroneUnit         _dronePrefab;
    [SerializeField] private SelfDestructDrone _selfDestructPrefab;

    [Header("풀 크기")]
    [SerializeField] private int _dronePoolSize        = 50;
    [SerializeField] private int _selfDestructPoolSize = 20;

    private ObjectPool<DroneUnit>         _dronePool;
    private ObjectPool<SelfDestructDrone> _selfDestructPool;

    protected override void Awake()
    {
        base.Awake();

        _dronePool = new ObjectPool<DroneUnit>(
            createFunc:      () => Instantiate(_dronePrefab, transform),
            actionOnGet:     d => d.gameObject.SetActive(true),
            actionOnRelease: d => d.gameObject.SetActive(false),
            actionOnDestroy: d => Destroy(d.gameObject),
            collectionCheck: false,
            defaultCapacity: _dronePoolSize,
            maxSize:         _dronePoolSize
        );

        _selfDestructPool = new ObjectPool<SelfDestructDrone>(
            createFunc:      () => Instantiate(_selfDestructPrefab, transform),
            actionOnGet:     b => b.gameObject.SetActive(true),
            actionOnRelease: b => b.gameObject.SetActive(false),
            actionOnDestroy: b => Destroy(b.gameObject),
            collectionCheck: false,
            defaultCapacity: _selfDestructPoolSize,
            maxSize:         _selfDestructPoolSize
        );
    }

    // ── 일반 드론 ────────────────────────────────────────────────────

    /// <summary>풀에서 드론을 꺼내 초기화 후 반환. parent 미지정 시 DronePool 하위에 배치.</summary>
    public DroneUnit GetDrone(float atk, float attackInterval, Vector3 position, Transform parent = null)
    {
        var drone = _dronePool.Get();
        drone.transform.SetParent(parent != null ? parent : transform, worldPositionStays: false);
        drone.transform.position = position;
        drone.Initialize(atk, attackInterval);
        return drone;
    }

    public void ReturnDrone(DroneUnit drone) => _dronePool.Release(drone);

    // ── 자폭 드론 ────────────────────────────────────────────────────

    /// <summary>풀에서 자폭 드론을 꺼내 초기화. 자폭 후 스스로 ReturnSelfDestruct를 호출한다.</summary>
    public SelfDestructDrone GetSelfDestruct(float damage, Vector3 position)
    {
        var bomb = _selfDestructPool.Get();
        bomb.transform.SetParent(transform, worldPositionStays: false);
        bomb.transform.position = position;
        bomb.Initialize(damage);
        return bomb;
    }

    public void ReturnSelfDestruct(SelfDestructDrone bomb) => _selfDestructPool.Release(bomb);
}
