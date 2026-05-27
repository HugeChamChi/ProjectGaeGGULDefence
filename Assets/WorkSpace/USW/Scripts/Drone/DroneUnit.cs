using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 영구 잔존 드론.
/// 배치 시 오너 유닛 근처에 작은 오프셋으로 위치를 고정하고 DroneHoverAnimation이 부유 연출을 담당한다.
/// 족장 집결 시 DroneManager.ExecuteRallyAsync가 MoveToAsync/ReturnToHomeAsync를 직접 호출한다.
/// </summary>
public class DroneUnit : MonoBehaviour
{
    public float   Atk            { get; private set; }
    public float   AttackInterval { get; private set; }
    public Vector3 HomePosition   => (_ownerTransform != null ? _ownerTransform.position : _homePositionFallback)
                                     + (Vector3)_spawnOffset;

    private Transform _ownerTransform;
    private Vector3   _homePositionFallback;
    private Vector2   _spawnOffset;

    [Header("유닛 근처 오프셋 범위 (px)")]
    [SerializeField] private float _offsetRangeX =  12f;
    [SerializeField] private float _offsetRangeY =  10f;

    private DroneHoverAnimation      _hoverAnim;
    private CancellationTokenSource  _attackCts;

    // ── 초기화 ──────────────────────────────────────────────────────

    private void Awake()
    {
        _hoverAnim = GetComponent<DroneHoverAnimation>();
    }

    /// <summary>DronePool.GetDrone() 에서 호출 — 스탯 주입 후 공격 루프 시작</summary>
    /// <param name="fixedOffset">null 이면 랜덤 오프셋, 값 지정 시 해당 위치에 고정 (드론 간 겹침 방지용)</param>
    public void Initialize(float atk, float attackInterval, Transform ownerTransform = null, Vector2? fixedOffset = null)
    {
        Atk                   = atk;
        AttackInterval        = attackInterval;
        _ownerTransform       = ownerTransform;
        _homePositionFallback = transform.position;

        _spawnOffset = fixedOffset ?? new Vector2(
            Random.Range(-_offsetRangeX, _offsetRangeX),
            Random.Range(0f, _offsetRangeY)
        );

        StopAll();
        transform.position = HomePosition;

        Manager.Drone?.RegisterDrone(this);

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
        var bossArea = Manager.Boss?.CurrentBoss?.GetComponent<BossAreaTarget>();
        if (bossArea == null || Manager.Projectile == null) return;
        Manager.Projectile.Launch(transform.position, bossArea.GetRandomWorldPosition());
    }

    // ── 풀 반환 시 정리 ─────────────────────────────────────────────

    private void OnDisable()
    {
        StopAll();
        Manager.Drone?.UnregisterDrone(this);
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
            float speedMult = Manager.Drone?.DroneSpeedMultiplier ?? 1f;
            int   delayMs   = Mathf.RoundToInt(AttackInterval / speedMult * 1000f);

            if (await UniTask.Delay(delayMs, cancellationToken: token).SuppressCancellationThrow())
                return;

            var boss = Manager.Boss?.CurrentBoss;
            if (boss == null || boss.IsDead) continue;

            LaunchProjectile();
            float dmg = Atk * (Manager.Drone?.DroneAtkMultiplier ?? 1f);
            boss.TakeDamage(Mathf.RoundToInt(dmg));
        }
    }

    private void LaunchProjectile()
    {
        var bossArea = Manager.Boss?.CurrentBoss?.GetComponent<BossAreaTarget>();
        if (bossArea == null || Manager.Projectile == null) return;
        Manager.Projectile.Launch(transform.position, bossArea.GetRandomWorldPosition());
    }
}
