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
    public float   AttackInterval => _owner != null ? _owner.GetCurrentAttackInterval()
        / (_owner is Drone_Betan ? 1f + (_owner.DroneSelections?.Get(DroneSelectionKind.BetanAttackSpeed)?.Value ?? 0f) : 1f) : 1f;
    /// <summary>선택지 대상 판별에 사용하는 소유 유닛.</summary>
    public UnitBase Owner => _owner;
    /// <summary>본체 보정과 군단 버프를 반영한 드론 1기의 비치명타 공격력.</summary>
    public int NonCriticalAttackDamage => _owner != null
        ? Mathf.RoundToInt(_owner.GetNonCriticalAttackDamage() * (_droneManager?.DroneAtkMultiplier ?? 1f)) : 0;
    /// <summary>전용 선택지와 군단 속도 버프까지 반영한 실제 공격간격.</summary>
    public float EffectiveAttackInterval => AttackInterval / (_droneManager?.DroneSpeedMultiplier ?? 1f);
    public Vector3 HomePosition   => (_owner != null ? _owner.transform.position : _homePositionFallback)
                                     + (Vector3)_spawnOffset;

    private UnitBase _owner;
    private Vector3   _homePositionFallback;

    private Vector2   _spawnOffset;

    [Header("오너 드래그 추적")]
    [Tooltip("오너 유닛을 드래그하는 동안 드론이 따라붙는 민첩도 (클수록 바짝 붙음).")]
    [SerializeField, Min(0.1f)] private float _dragFollowSharpness = 18f;
    private DragHandler _ownerDrag;
    private bool _followingOwnerDrag;
    private bool _moving; // 집결/귀환 이동 중에는 추적하지 않는다

    [Header("투사체 프리팹 (지정 시 RM 풀링 사용, 비우면 기본 Pool 사용)")]
    [SerializeField] private Projectile _projectilePrefab;

    [Header("스킬 발동 액션 스프라이트")]
    [Tooltip("오너의 스킬이 발동될 때(감망 버프 / 델탕 디버프 / 베탕 자폭드론 발사) 잠깐 바뀌는 스프라이트. 비워두면 기능 꺼짐.")]
    [SerializeField] private Sprite _actionSprite;
    [Tooltip("액션 스프라이트를 유지하는 시간(초). 이후 자동으로 평소 스프라이트로 복귀.")]
    [SerializeField] private float  _actionSpriteHoldSeconds = 0.15f;

    [Header("정렬")]
    [Tooltip("오너 유닛의 DragHandler와 동일한 Y기반 정렬 공식을 따라가되, 항상 이 값만큼 앞에 그립니다.")]
    [SerializeField] private int _sortingOrderOffset = 1;

    private SpriteRenderer           _spriteRenderer;
    private Sprite                   _normalSprite;
    private DroneHoverAnimation      _hoverAnim;
    private CancellationTokenSource  _attackCts;
    private CancellationTokenSource  _flashCts;
    private Animator                 _animator;
    private int _attackLifetime;

    // ── 초기화 ──────────────────────────────────────────────────────

    private void Awake()
    {
        _hoverAnim = GetComponent<DroneHoverAnimation>();
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _normalSprite = _spriteRenderer.sprite;
    }

    private AnimatorUpdateMode _updateModeBeforePause;
    private bool _inPauseIdle;

    /// <summary>레벨업 등 일시정지 중 부유/애니메이션을 unscaled로 유지한다 (시각 전용).</summary>
    public void SetPauseIdle(bool on)
    {
        if (on == _inPauseIdle) return;
        _inPauseIdle = on;
        if (_hoverAnim != null) _hoverAnim.SetForceUnscaled(on);
        if (_animator == null) return;
        if (on) { _updateModeBeforePause = _animator.updateMode; _animator.updateMode = AnimatorUpdateMode.UnscaledTime; }
        else _animator.updateMode = _updateModeBeforePause;
    }

    /// <summary>DroneSpawnerBase.SpawnOneDrone() 에서 호출 — 스탯 주입 후 공격 루프 시작</summary>
    /// <param name="slotOffset">오너 기준 고정 대형 슬롯 오프셋 (드론 간 겹침 방지용)</param>
    public void Initialize(UnitBase owner, Vector2 slotOffset)
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        _owner                = owner;
        _homePositionFallback = transform.position;
        _spawnOffset          = slotOffset;
        _ownerDrag            = owner != null ? owner.GetComponent<DragHandler>() : null;
        _followingOwnerDrag   = false;

        StopAll();
        transform.position = HomePosition;

        _droneManager?.RegisterDrone(this);

        _attackCts = new CancellationTokenSource();
        AttackLoopAsync(_attackCts.Token).Forget();

        StartOrbit();
    }

    // ── 렌더 정렬 (오너보다 항상 앞) ──────────────────────────────────

    private void LateUpdate()
    {
        if (_spriteRenderer != null && _owner != null)
            _spriteRenderer.sortingOrder = Mathf.RoundToInt(-_owner.transform.position.y * 100f) + _sortingOrderOffset;
        FollowOwnerDrag();
    }

    /// <summary>
    /// 오너를 드래그하는 동안 호버를 멈추고 오너 쪽 슬롯으로 부드럽게 따라간다.
    /// 드래그가 끝나면 홈 위치에 붙은 뒤 호버를 재개한다 (배치가 바뀌면 오너가 드론을 새로 소환한다).
    /// </summary>
    private void FollowOwnerDrag()
    {
        if (_moving || _owner == null || _ownerDrag == null) return;
        bool dragging = _ownerDrag.IsDragging;
        if (!dragging && !_followingOwnerDrag) return;

        if (dragging && !_followingOwnerDrag)
        {
            _followingOwnerDrag = true;
            StopOrbit();
        }

        Vector3 home = HomePosition;
        float t = 1f - Mathf.Exp(-_dragFollowSharpness * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, home, t);

        if (!dragging && (transform.position - home).sqrMagnitude < 0.0004f)
        {
            transform.position = home;
            _followingOwnerDrag = false;
            StartOrbit();
        }
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
        _followingOwnerDrag = false;
        _moving = true;
        try { await MoveToCoreAsync(target, duration, token); }
        finally { _moving = false; }
    }

    private async UniTask MoveToCoreAsync(Vector3 target, float duration, CancellationToken token)
    {
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

    // ── 스킬 발동 액션 스프라이트 (DroneSpawnerBase.FlashOwnedDrones 에서 호출) ──

    /// <summary>오너의 스킬 발동 순간 잠깐 액션 스프라이트로 바뀌었다가 자동으로 복귀한다.</summary>
    public void PlayActionFlash()
    {
        if (_spriteRenderer == null || _actionSprite == null) return;

        _flashCts?.Cancel();
        _flashCts?.Dispose();
        _flashCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        FlashSpriteAsync(_flashCts.Token).Forget();
    }

    private async UniTaskVoid FlashSpriteAsync(CancellationToken token)
    {
        _spriteRenderer.sprite = _actionSprite;

        if (await UniTask.Delay(Mathf.RoundToInt(_actionSpriteHoldSeconds * 1000f), cancellationToken: token).SuppressCancellationThrow())
            return;

        _spriteRenderer.sprite = _normalSprite;
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
        _attackLifetime++;
        _attackCts?.Cancel();
        _attackCts?.Dispose();
        _attackCts = null;

        _flashCts?.Cancel();
        _flashCts?.Dispose();
        _flashCts = null;
        if (_spriteRenderer != null) _spriteRenderer.sprite = _normalSprite;
        StopOrbit();
    }

    // ── 공격 루프 ───────────────────────────────────────────────────

    private async UniTaskVoid AttackLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            int delayMs = Mathf.RoundToInt(EffectiveAttackInterval * 1000f);

            if (await UniTask.Delay(delayMs, cancellationToken: token).SuppressCancellationThrow())
                return;

            var boss = _bossManager?.CurrentBoss;
            if (boss == null || boss.IsDead) continue;
            bool critical = false;
            int ownerDamage = _owner != null ? _owner.GetAttackDamage(out critical) : 0;
            float dmg = ownerDamage * (_droneManager?.DroneAtkMultiplier ?? 1f);
            LaunchProjectile(Mathf.RoundToInt(dmg), critical);
        }
    }

    private void LaunchProjectile(int damage, bool critical = false)
    {
        var kind = critical ? BossDamageKind.Critical : BossDamageKind.Normal;
        var boss = _bossManager?.CurrentBoss;
        var bossArea = boss?.GetComponent<BossAreaTarget>();
        if (bossArea == null || boss.IsDead || _owner == null || _owner.IsStunned || _owner.currentCell == null) return;
        var cell = _owner.currentCell.Model;
        if (cell.IsSealed || cell.IsAttackDisabled || cell.TotemAttackDisabled) return;
        var owner = _owner;
        int lifetime = _attackLifetime;
        Vector3 origin = transform.position;
        var replay = owner.Combat?.BeginDroneBasicAttack(boss, transform,
            () => this != null && _attackLifetime == lifetime && _owner == owner && owner.currentCell != null);
        var recorded = replay?.IsEnabled == true ? new RecordedDamage(damage, kind) : null;
        bool originalHit = false;
        bool shadowArrivedEarly = false;
        
        Vector3 targetPos = bossArea.GetRandomWorldPosition();
        System.Action applyHit = () =>
        {
            if (boss != null && !boss.IsDead)
            {
                if (recorded != null) recorded.Apply(boss, targetPos);
                else boss.TakeDamage(damage, targetPos, kind);
            }
        };
        ShootProjectile(targetPos, () =>
        {
            applyHit();
            originalHit = true;
            if (shadowArrivedEarly && replay?.CanReplay == true) applyHit();
        }, origin);
        replay?.RecordShot(hit => ShootProjectile(targetPos, hit, origin), () =>
        {
            if (originalHit) applyHit();
            else shadowArrivedEarly = true;
        });
        owner.InvokeOnAttack();
    }

    private void ShootProjectile(Vector3 targetPos, System.Action onHitCallback = null, Vector3? recordedOrigin = null)
    {
        Vector3 origin = recordedOrigin ?? transform.position;
        _audioManager?.PlaySFX("05.Drone_Attack");

        if (_projectilePrefab != null)
        {
            var p = RM.Instantiate(_projectilePrefab, origin, Quaternion.identity, true);
            if (p != null)
            {
                // 부모를 설정하지 않거나 null로 두어 WorldSpace 좌표계를 온전히 사용
                p.transform.SetParent(null);

                p.Launch(origin, targetPos, proj =>
                {
                    onHitCallback?.Invoke();
                    RM.Destroy(proj.gameObject);
                }, null, _owner);
            }
        }
        else if (_projectileManager != null)
        {
            _projectileManager.Launch(origin, targetPos, onHitCallback, _owner);
        }
    }
}
