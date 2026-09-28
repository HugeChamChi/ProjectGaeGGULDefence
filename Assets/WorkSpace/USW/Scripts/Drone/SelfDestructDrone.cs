using System.Threading;
using VContainer;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 자폭 드론 — 분노 차징(팝인 + 붉은 점멸 + 떨림) → 보스를 향해 곡선 가속 돌진 → 폭발 피해 후 풀로 반환된다.
/// 생성 시점/개수는 Drone_Betan.SpawnSelfDestructDrones(스킬, 선택지)가 결정하고, 이 클래스는 비행 연출과 피해만 담당한다.
/// </summary>
public class SelfDestructDrone : MonoBehaviour
{
    [Inject] private BossManager _bossManager;
    [Inject] private DroneManager _droneManager;

    [Header("분노 차징")]
    [Tooltip("생성 후 돌진 전까지 달아오르는 시간(초).")]
    [SerializeField, Min(0f)] private float _chargeDuration = 0.35f;
    [Tooltip("여러 기가 동시에 생성될 때 출발을 흩뜨리는 추가 랜덤 지연 최대값(초).")]
    [SerializeField, Min(0f)] private float _launchJitter = 0.15f;
    [Tooltip("생성 팝인 시간(초).")]
    [SerializeField, Min(0.01f)] private float _popInDuration = 0.2f;
    [Tooltip("차징 중 붉게 점멸하는 색.")]
    [SerializeField] private Color _angryTint = new Color(1f, 0.45f, 0.35f);
    [Tooltip("점멸 한 번(밝음→붉음) 시간(초).")]
    [SerializeField, Min(0.01f)] private float _tintPulseSeconds = 0.08f;
    [Tooltip("차징 중 부들부들 떠는 회전 세기(도).")]
    [SerializeField, Min(0f)] private float _chargeShakeAngle = 20f;
    [Tooltip("차징 시작 위치에서 살짝 뒤로 물러나는 거리(월드 단위, 보스 반대 방향).")]
    [SerializeField, Min(0f)] private float _windUpDistance = 0.25f;

    [Header("돌진")]
    [SerializeField, Min(0.05f)] private float _flyDuration = 0.4f;
    [Tooltip("곡선 경로의 옆으로 휘는 최대 거리(월드 단위). 드론마다 랜덤 방향.")]
    [SerializeField, Min(0f)] private float _arcSideOffset = 0.8f;
    [Tooltip("가속 곡선 지수 (1 = 등속, 클수록 끝에서 급가속).")]
    [SerializeField, Min(1f)] private float _accelPower = 2f;
    [Tooltip("돌진 중 진행 방향으로 기울이는 최대 각도(도).")]
    [SerializeField, Range(0f, 90f)] private float _dashTilt = 25f;

    [Header("폭발")]
    [Tooltip("착탄 지점에 생성할 폭발 이펙트 (비우면 이펙트 없음).")]
    [SerializeField] private GameObject _explosionPrefab;
    [SerializeField, Min(0.1f)] private float _explosionLifetime = 1.5f;
    [Tooltip("폭발 효과음 (비우면 재생 안 함).")]
    [SerializeField] private string _explosionSfx;

    [Inject] private AudioManager _audioManager;

    private CancellationTokenSource _cts;
    private SpriteRenderer _spriteRenderer;
    private Color _baseColor = Color.white;
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;
    private TrailRendererReset _trailReset;
    private Sequence _chargeSequence;
    private Tween _tintTween;
    private Vector3? _previewTarget;
    private GameObject _previewExplosion;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _baseColor = _spriteRenderer.color;
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;
        _trailReset = GetComponent<TrailRendererReset>();
    }

    /// <summary>Drone_Betan.SpawnSelfDestructDrones() 에서 호출 — 차징 후 보스를 향해 돌진한다.</summary>
    public void Initialize(float damage)
    {
        StopSequence();
        _cts = new CancellationTokenSource();
        _previewTarget = null;
        _previewExplosion = null;
        SequenceAsync(damage, _cts.Token).Forget();
    }

    /// <summary>데미지 없이 지정 위치로 연출만 재생한다 (테스트 씬 프리뷰용).</summary>
    /// <param name="explosionOverride">지정 시 이번 발사에만 이 폭발 이펙트를 쓴다 (연기 유무 비교용).</param>
    public void InitializePreview(Vector3 target, GameObject explosionOverride = null)
    {
        StopSequence();
        _cts = new CancellationTokenSource();
        target.z = 0f;
        _previewTarget = target;
        _previewExplosion = explosionOverride;
        SequenceAsync(0f, _cts.Token).Forget();
    }

    // ── 차징 → 돌진 → 폭발 ──────────────────────────────────────────

    private async UniTaskVoid SequenceAsync(float damage, CancellationToken token)
    {
        if (!TryGetTarget(out Vector3 target))
        {
            ReturnToPool();
            return;
        }

        SetTrailEmitting(false);
        if (await ChargeAsync(target, token)) return;

        // 차징 중 보스가 바뀌었거나 죽었으면 새 착탄 지점을 다시 고른다.
        if (!TryGetTarget(out target))
        {
            ReturnToPool();
            return;
        }

        SetTrailEmitting(true);
        if (await DashAsync(target, token)) return;

        Explode(damage, target);
        ReturnToPool();
    }

    private bool TryGetTarget(out Vector3 target)
    {
        if (_previewTarget.HasValue)
        {
            target = _previewTarget.Value;
            return true;
        }

        target = default;
        var boss = _bossManager?.CurrentBoss;
        if (boss == null || boss.IsDead) return false;

        var bossArea = boss.GetComponent<BossAreaTarget>();
        target = bossArea != null ? bossArea.GetRandomWorldPosition() : boss.transform.position;
        target.z = 0f;
        return true;
    }

    /// <summary>팝인 + 붉은 점멸 + 떨림 + 뒤로 살짝 물러나기. 취소되면 true.</summary>
    private async UniTask<bool> ChargeAsync(Vector3 target, CancellationToken token)
    {
        Vector3 start = transform.position;
        Vector3 away = (start - target).normalized;
        float duration = _chargeDuration + Random.Range(0f, _launchJitter);

        transform.localScale = Vector3.zero;
        transform.localRotation = _baseRotation;
        _chargeSequence = DOTween.Sequence()
            .Append(transform.DOScale(_baseScale, _popInDuration).SetEase(Ease.OutBack))
            .Join(transform.DOMove(start + away * _windUpDistance, duration).SetEase(Ease.OutQuad))
            .Insert(_popInDuration, transform.DOShakeRotation(Mathf.Max(0.01f, duration - _popInDuration),
                new Vector3(0f, 0f, _chargeShakeAngle), 30, 90f, false))
            .SetLink(gameObject);

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _baseColor;
            _tintTween = _spriteRenderer.DOColor(_angryTint, _tintPulseSeconds)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                return true;
            elapsed += Time.deltaTime;
        }

        _chargeSequence?.Kill();
        _chargeSequence = null;
        if (_spriteRenderer != null)
        {
            _tintTween?.Kill();
            _tintTween = null;
            _spriteRenderer.color = _angryTint;
        }
        transform.localScale = _baseScale;
        return false;
    }

    /// <summary>옆으로 휘는 2차 베지어 곡선으로 가속 돌진. 취소되면 true.</summary>
    private async UniTask<bool> DashAsync(Vector3 target, CancellationToken token)
    {
        Vector3 start = transform.position;
        Vector3 dir = target - start;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f).normalized * (Random.Range(-1f, 1f) * _arcSideOffset);
        Vector3 control = start + dir * 0.4f + side;

        float elapsed = 0f;
        Vector3 prev = start;
        while (elapsed < _flyDuration)
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                return true;

            elapsed += Time.deltaTime;
            float t = Mathf.Pow(Mathf.Clamp01(elapsed / _flyDuration), _accelPower);
            float u = 1f - t;
            Vector3 pos = u * u * start + 2f * u * t * control + t * t * target;
            transform.position = pos;

            Vector3 vel = pos - prev;
            if (vel.sqrMagnitude > 0.000001f)
            {
                float tilt = Mathf.Clamp(-vel.x / vel.magnitude, -1f, 1f) * _dashTilt;
                transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, tilt);
            }
            prev = pos;
        }

        transform.position = target;
        return false;
    }

    private void Explode(float damage, Vector3 target)
    {
        var boss = _previewTarget.HasValue ? null : _bossManager?.CurrentBoss;
        if (boss != null && !boss.IsDead)
            boss.TakeDamage(Mathf.RoundToInt(damage), target);

        var explosionPrefab = _previewExplosion != null ? _previewExplosion : _explosionPrefab;
        if (explosionPrefab != null)
        {
            var fx = RM.Instantiate(explosionPrefab, target, Quaternion.identity, true);
            if (fx != null) RM.Destroy(fx, _explosionLifetime);
        }
        if (!string.IsNullOrEmpty(_explosionSfx))
            _audioManager?.PlaySFX(_explosionSfx);

        if (!_previewTarget.HasValue) _droneManager?.NotifySelfDestructExplosion();
    }

    private void SetTrailEmitting(bool on)
    {
        if (!on) _trailReset?.Clear();
        foreach (var trail in GetComponentsInChildren<TrailRenderer>(true))
            trail.emitting = on;
    }

    private void ReturnToPool() => RM.Destroy(this.gameObject);

    // ── 풀 반환 시 정리 ─────────────────────────────────────────────

    private void OnDisable() => StopSequence();
    private void OnDestroy() => StopSequence();

    private void StopSequence()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _chargeSequence?.Kill();
        _chargeSequence = null;
        _tintTween?.Kill();
        _tintTween = null;
        if (_spriteRenderer != null) _spriteRenderer.color = _baseColor;
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
    }
}
