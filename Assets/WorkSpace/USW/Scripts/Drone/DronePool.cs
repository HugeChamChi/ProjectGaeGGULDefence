using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 드론 오브젝트 풀 — InGameSingleton.
/// 일반 드론(50)과 자폭 드론(20) 두 풀을 관리한다.
/// DroneProducer는 Instantiate 대신 이 클래스를 통해 드론을 가져온다.
/// </summary>
public class DronePool : MonoBehaviour
{
    [Header("프리팹")]
    [SerializeField] private GameObject _dronePrefab;
    [SerializeField] private GameObject _selfDestructPrefab;

    [Header("Canvas 컨테이너 (드론 프리팹이 UI Image인 경우 Canvas 하위 Transform 연결)")]
    [SerializeField] private Transform _droneContainer;

    [Header("풀 크기")]
    [SerializeField] private int _dronePoolSize        = 50;
    [SerializeField] private int _selfDestructPoolSize = 20;

    private ObjectPool<DroneUnit>         _dronePool;
    private ObjectPool<SelfDestructDrone> _selfDestructPool;

    protected void Awake()
    {
        if (_dronePrefab == null || _dronePrefab.GetComponent<DroneUnit>() == null)
        {
            Debug.LogError("DronePool: _dronePrefab 미연결 또는 DroneUnit 컴포넌트 없음 — Inspector에서 DronePrefab.prefab을 연결하세요.");
            return;
        }

        var droneParent = _droneContainer != null ? _droneContainer : transform;

        _dronePool = new ObjectPool<DroneUnit>(
            createFunc:      () => Instantiate(_dronePrefab, droneParent).GetComponent<DroneUnit>(),
            actionOnGet:     d => d.gameObject.SetActive(true),
            actionOnRelease: d => d.gameObject.SetActive(false),
            actionOnDestroy: d => Destroy(d.gameObject),
            collectionCheck: false,
            defaultCapacity: _dronePoolSize,
            maxSize:         _dronePoolSize
        );

        if (_selfDestructPrefab == null || _selfDestructPrefab.GetComponent<SelfDestructDrone>() == null)
        {
            Debug.LogWarning("DronePool: _selfDestructPrefab 미연결 또는 SelfDestructDrone 컴포넌트 없음 — 자폭 드론 비활성화.");
            return;
        }

        _selfDestructPool = new ObjectPool<SelfDestructDrone>(
            createFunc:      () => Instantiate(_selfDestructPrefab, droneParent).GetComponent<SelfDestructDrone>(),
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
    /// <param name="ownerTransform">드론이 맴돌 기준 유닛 Transform. null이면 생성 위치 고정.</param>
    public DroneUnit GetDrone(float atk, float attackInterval, Vector3 position, Transform ownerTransform = null, Vector2? fixedOffset = null)
    {
        if (_dronePool == null) return null;
        var drone = _dronePool.Get();
        drone.transform.SetParent(_droneContainer != null ? _droneContainer : transform, worldPositionStays: false);
        drone.transform.position = position;
        drone.Initialize(atk, attackInterval, ownerTransform, fixedOffset);
        return drone;
    }

    public void ReturnDrone(DroneUnit drone) => _dronePool?.Release(drone);

    // ── 자폭 드론 ────────────────────────────────────────────────────

    /// <summary>풀에서 자폭 드론을 꺼내 초기화. 자폭 후 스스로 ReturnSelfDestruct를 호출한다.</summary>
    public SelfDestructDrone GetSelfDestruct(float damage, Vector3 position)
    {
        if (_selfDestructPool == null) return null;
        var bomb = _selfDestructPool.Get();
        bomb.transform.SetParent(_droneContainer != null ? _droneContainer : transform, worldPositionStays: false);
        bomb.transform.position = position;
        bomb.Initialize(damage);
        return bomb;
    }

    public void ReturnSelfDestruct(SelfDestructDrone bomb) => _selfDestructPool?.Release(bomb);
}
