using System.Threading;
using VContainer;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 영구 잔존 드론.
/// 배치 시 오너 유닛 근처에 작은 오프셋으로 위치를 고정하고 DroneHoverAnimation이 부유 연출을 담당한다.
/// 족장 집결 시 DroneManager.ExecuteRallyAsync가 MoveToAsync/ReturnToHomeAsync를 직접 호출한다.
/// </summary>
public class DroneUnit : MonoBehaviour
{
    [Inject] private DroneManager _droneManager;
    [Inject] private BossManager _bossManager;
    [Inject] private ProjectilePool _projectileManager;
    [Inject] private AudioManager _audioManager;

    public float   Atk            => _owner != null ? _owner.GetAttackDamage() : 0f;
    public float   AttackInterval => _owner != null ? _owner.GetCurrentAttackInterval() : 1f;
    public Vector3 HomePosition   => (_owner != null ? _owner.transform.position : _homePositionFallback)
                                     + (Vector3)_spawnOffset;

    private UnitBase _owner;
    private Vector3   _homePositionFallback;

    private Vector2   _spawnOffset;

    [Header("Drone Offset")]
    [SerializeField] private float _offsetRangeX =  0.12f;
    [SerializeField] private float _offsetRangeY =  0.10f;

    [Header("투사체 프리팹 (지정 시 RM 풀링 사용, 비우면 기본 Pool 사용)")]
    [SerializeField] private Projectile _projectilePrefab;

    private DroneHoverAnimation      _hoverAnim;
    private CancellationTokenSource  _attackCts;
    private Animator                 _animator;

    // ── 초기화 ──────────────────────────────────────────────────────

    private void Awake()
    {
        _hoverAnim = GetComponent<DroneHoverAnimation>();
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    /// <summary>DronePool.GetDrone() 에서 호출 — 스탯 주입 후 공격 루프 시작</summary>
    /// <param name="fixedOffset">null 이면 랜덤 오프셋, 값 지정 시 해당 위치에 고정 (드론 간 겹침 방지용)</param>
    public void Initialize(UnitBase owner, Vector2? fixedOffset = null)
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        _owner                = owner;
        _homePositionFallback = transform.position;

        _spawnOffset = fixedOffset ?? new Vector2(
            Random.Range(-_offsetRangeX, _offsetRangeX),
            Random.Range(0f, _offsetRangeY)
        );

        StopAll();
        transform.position = HomePosition;

        _droneManager?.RegisterDrone(this);

        _attackCts = new CancellationTokenSource();
        AttackLoopAsync(_attackCts.Token).Forget();

        StartOrbit();
    }

    // ── 호버 제어 (DroneManager.ExecuteRallyAsync 에서도 호출) ──────

    public void StartOrbit()
    {
        if (_hoverAnim == null) return;
        _hoverAnim.enabled = false;
        _hoverAnim.enabled = true; // OnEnable → basePos 현재 위치로 리셋
    }

    public void StopOrbit()
    {
        if (_hoverAnim != null) _hoverAnim.enabled = false;
    }

    // ── 이동 (집결/귀환) ────────────────────────────────────────────

    /// <summary>SmoothStep 보간으로 target 까지 이동. 호버는 자동 중단.</summary>
    public async UniTask MoveToAsync(Vector3 target, float duration, CancellationToken token)
    {
        StopOrbit();

        Vector3 start   = transform.position;
        float   elapsed = 0f;

        while (elapsed < duration)
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                return;

            elapsed           += Time.deltaTime;
            float t            = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(start, target, t);
        }

        transform.position = target;
    }

    public UniTask ReturnToHomeAsync(float duration, CancellationToken token)
        => MoveToAsync(HomePosition, duration, token);

    // ── 집결 사격 (DroneManager 에서 일제 호출) ─────────────────────

    public void FireRallyShot()
    {
        var bossArea = _bossManager?.CurrentBoss?.GetComponent<BossAreaTarget>();
        if (bossArea == null) return;
        ShootProjectile(bossArea.GetRandomWorldPosition());
    }

    // ── 풀 반환 시 정리 ─────────────────────────────────────────────

    private void OnDisable()
    {
        StopAll();
        _droneManager?.UnregisterDrone(this);
    }

    private void OnDestroy() => StopAll();

    private void StopAll()
    {
        _attackCts?.Cancel();
        _attackCts?.Dispose();
        _attackCts = null;
        StopOrbit();
    }

    // ── 공격 루프 ───────────────────────────────────────────────────

    private async UniTaskVoid AttackLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            float speedMult = _droneManager?.DroneSpeedMultiplier ?? 1f;
            int   delayMs   = Mathf.RoundToInt(AttackInterval / speedMult * 1000f);

            if (await UniTask.Delay(delayMs, cancellationToken: token).SuppressCancellationThrow())
                return;

            var boss = _bossManager?.CurrentBoss;
            if (boss == null || boss.IsDead) continue;
            float dmg = Atk * (_droneManager?.DroneAtkMultiplier ?? 1f);
            LaunchProjectile(Mathf.RoundToInt(dmg));
        }
    }

    private void LaunchProjectile(int damage)
    {
        var boss = _bossManager?.CurrentBoss;
        var bossArea = boss?.GetComponent<BossAreaTarget>();
        if (bossArea == null) return;
        
        Vector3 targetPos = bossArea.GetRandomWorldPosition();
        ShootProjectile(targetPos, () => 
        {
            if (boss != null && !boss.IsDead)
            {
                boss.TakeDamage(damage, targetPos);
            }
        });
    }

    private void ShootProjectile(Vector3 targetPos, System.Action onHitCallback = null)
    {
        _audioManager?.PlaySFX("05.Drone_Attack");

        if (_projectilePrefab != null)
        {
            var p = RM.Instantiate(_projectilePrefab, transform.position, Quaternion.identity, true);
            if (p != null)
            {
                // 부모를 설정하지 않거나 null로 두어 WorldSpace 좌표계를 온전히 사용
                p.transform.SetParent(null);

                p.Launch(transform.position, targetPos, proj => 
                {
                    onHitCallback?.Invoke();
                    RM.Destroy(proj.gameObject);
                });
            }
        }
        else if (_projectileManager != null)
        {
            _projectileManager.Launch(transform.position, targetPos, onHitCallback);
        }
    }
}
