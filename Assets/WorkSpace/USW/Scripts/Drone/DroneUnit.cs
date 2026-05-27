using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 영구 잔존 드론.
/// 평소에는 HomePosition 주변을 원형 궤도로 맴돌며 보스를 자동 공격한다.
/// 족장 집결 시 DroneManager.ExecuteRallyAsync가 MoveToAsync/ReturnToHomeAsync를 직접 호출한다.
/// </summary>
public class DroneUnit : MonoBehaviour
{
    public float   Atk            { get; private set; }
    public float   AttackInterval { get; private set; }
    public Vector3 HomePosition   { get; private set; }

    [Header("궤도 반경 (px)")]
    [SerializeField] private float _orbitRadiusMin = 25f;
    [SerializeField] private float _orbitRadiusMax = 45f;

    [Header("궤도 속도 (rad/s)")]
    [SerializeField] private float _orbitSpeedMin = 0.8f;
    [SerializeField] private float _orbitSpeedMax = 1.4f;

    private CancellationTokenSource _attackCts;
    private CancellationTokenSource _orbitCts;

    // ── 초기화 ──────────────────────────────────────────────────────

    /// <summary>DronePool.GetDrone() 에서 호출 — 스탯 주입 후 공격·궤도 루프 시작</summary>
    public void Initialize(float atk, float attackInterval)
    {
        Atk            = atk;
        AttackInterval = attackInterval;
        HomePosition   = transform.position;

        StopAll();
        Manager.Drone?.RegisterDrone(this);

        _attackCts = new CancellationTokenSource();
        AttackLoopAsync(_attackCts.Token).Forget();

        StartOrbit();
    }

    // ── 궤도 제어 (DroneManager.ExecuteRallyAsync 에서도 호출) ──────

    public void StartOrbit()
    {
        StopOrbit();
        _orbitCts = new CancellationTokenSource();
        OrbitLoopAsync(_orbitCts.Token).Forget();
    }

    public void StopOrbit()
    {
        _orbitCts?.Cancel();
        _orbitCts?.Dispose();
        _orbitCts = null;
    }

    // ── 이동 (집결/귀환) ────────────────────────────────────────────

    /// <summary>SmoothStep 보간으로 target 까지 이동. 궤도는 자동 중단.</summary>
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

    // ── 궤도 루프 ───────────────────────────────────────────────────

    private async UniTaskVoid OrbitLoopAsync(CancellationToken token)
    {
        float phase  = Random.Range(0f, Mathf.PI * 2f);
        float speed  = Random.Range(_orbitSpeedMin, _orbitSpeedMax);
        float radius = Random.Range(_orbitRadiusMin, _orbitRadiusMax);

        while (!token.IsCancellationRequested)
        {
            phase += speed * Time.deltaTime;
            transform.position = HomePosition + new Vector3(
                Mathf.Cos(phase) * radius,
                Mathf.Sin(phase) * radius * 0.5f,
                0f
            );

            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                return;
        }
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
