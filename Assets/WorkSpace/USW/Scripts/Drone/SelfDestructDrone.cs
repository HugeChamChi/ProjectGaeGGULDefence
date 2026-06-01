using System.Threading;
using VContainer;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 자폭 드론 — 보스 방향으로 비행 후 폭발 피해를 주고 풀로 반환된다.
/// 오브젝트 풀로 관리되므로 Destroy 대신 DronePool.ReturnSelfDestruct를 호출한다.
/// </summary>
public class SelfDestructDrone : MonoBehaviour
{
    [Inject] private BossManager _bossManager;
    [Inject] private DronePool _dronePoolManager;

    [SerializeField] private float _flyDuration = 0.4f;

    private CancellationTokenSource _cts;

    /// <summary>DronePool.GetSelfDestruct() 에서 호출 — 즉시 보스를 향해 비행 시작</summary>
    public void Initialize(float damage)
    {
        StopSequence();
        _cts = new CancellationTokenSource();
        FlyAndExplodeAsync(damage, _cts.Token).Forget();
    }

    // ── 비행 + 폭발 ─────────────────────────────────────────────────

    private async UniTaskVoid FlyAndExplodeAsync(float damage, CancellationToken token)
    {
        var boss = _bossManager?.CurrentBoss;
        if (boss == null || boss.IsDead)
        {
            ReturnToPool();
            return;
        }

        var bossArea = boss.GetComponent<BossAreaTarget>();
        Vector3 target = bossArea != null
            ? bossArea.GetRandomWorldPosition()
            : boss.transform.position;

        // 보스 방향으로 비행
        Vector3 start   = transform.position;
        float   elapsed = 0f;

        while (elapsed < _flyDuration)
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                return;

            elapsed            += Time.deltaTime;
            transform.position  = Vector3.Lerp(start, target, elapsed / _flyDuration);
        }

        // 도착 — 폭발
        boss = _bossManager?.CurrentBoss;
        if (boss != null && !boss.IsDead)
            boss.TakeDamage(Mathf.RoundToInt(damage));

        ReturnToPool();
    }

    private void ReturnToPool() => _dronePoolManager?.ReturnSelfDestruct(this);

    // ── 풀 반환 시 정리 ─────────────────────────────────────────────

    private void OnDisable() => StopSequence();
    private void OnDestroy() => StopSequence();

    private void StopSequence()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
