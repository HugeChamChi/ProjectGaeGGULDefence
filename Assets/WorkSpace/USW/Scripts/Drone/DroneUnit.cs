using System.Threading;
using VContainer;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 영구 잔존 드론.
/// SO 설정의 H 궤도로 오너를 돌며, 궤도를 끄면 기존 고정 슬롯/호버를 사용한다.
/// 족장 집결 시 DroneManager.ExecuteRallyAsync가 MoveToAsync/ReturnToHomeAsync를 직접 호출한다.
/// </summary>
public class DroneUnit : MonoBehaviour
{
    [Inject] private DroneManager _droneManager;
    [Inject] private BossManager _bossManager;
    [Inject] private ProjectilePool _projectileManager;
    [Inject] private AudioManager _audioManager;
    [Inject] private EndlessRunService _run;
    [Inject] private GameManager _gameManager;

    public float   Atk            => _owner != null ? _owner.GetAttackDamage() : 0f;
    public float   AttackInterval => _owner != null ? _owner.GetCurrentAttackInterval()
        / (_owner is Drone_Betan ? 1f + (_owner.DroneEffects?.DroneAttackSpeedBonus ?? 0f) : 1f) : 1f;
    /// <summary>선택지 대상 판별에 사용하는 소유 유닛.</summary>
    public UnitBase Owner => _owner;
    /// <summary>본체 보정과 군단 버프를 반영한 드론 1기의 비치명타 공격력.</summary>
    public int NonCriticalAttackDamage => _owner != null
        ? Mathf.RoundToInt(_owner.GetNonCriticalAttackDamage() * (_droneManager?.DroneAtkMultiplier ?? 1f)) : 0;
    /// <summary>전용 선택지와 군단 속도 버프까지 반영한 실제 공격간격.</summary>
    public float EffectiveAttackInterval => AttackInterval / (_droneManager?.DroneSpeedMultiplier ?? 1f);
    /// <summary>오너 위치와 현재 위상을 반영한 귀환/드래그 기준점.</summary>
    public Vector3 HomePosition => (_owner != null ? _owner.transform.position : _homePositionFallback)
        + (UsesOrbit ? OrbitOffset : (Vector3)_spawnOffset);
    /// <summary>대형 드론들의 위상 시계를 동기화할 전투/선택 정지 시간.</summary>
    public float OrbitSeconds => _orbitSeconds;
    /// <summary>풀 재사용 이전의 집결 이동을 구분하는 배치 수명.</summary>
    public int PlacementLifetime => _attackLifetime;

    private UnitBase _owner;
    private Vector3   _homePositionFallback;

    private Vector2   _spawnOffset;
    private DroneFormationSettings _formation;
    private float _orbitSeconds;
    private int _orbitSlot, _orbitCount = 1;
    private bool _orbitRunning, _usingOrbit;
    private float _facingSign = 1f;
    private float _punchFactor = 1f;
    private Quaternion _authoredRotation;
    private CancellationTokenSource _moveCts;
    private bool UsesOrbit => _formation != null && _formation.Orbit != null
        && _formation.Orbit.Enabled && _formation.Orbit.PeriodSeconds > 0f;
    private float OrbitAngle => (_orbitSeconds / _formation.Orbit.PeriodSeconds
        + _orbitSlot / (float)_orbitCount) * (Mathf.PI * 2f);
    private Vector3 OrbitOffset => new Vector3(
        Mathf.Cos(OrbitAngle) * (_formation.OwnerHalfWidth + _formation.Orbit.HorizontalPadding),
        _formation.OwnerHeadHeight * _formation.Orbit.CenterHeightRatio
            + Mathf.Sin(OrbitAngle) * _formation.Orbit.VerticalRadius, 0f);

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

    [Header("공격 프레임")]
    [Tooltip("투사체를 쏘는 순간 잠깐 바뀌는 스프라이트 (날개 펼침). 비워두면 스프라이트 전환 없음.")]
    [SerializeField] private Sprite _attackSprite;
    [Tooltip("공격 스프라이트를 유지하는 시간(초).")]
    [SerializeField, Min(0f)] private float _attackSpriteHoldSeconds = 0.18f;
    [Tooltip("발사 순간 스케일 펀치 세기 (0이면 끔).")]
    [SerializeField, Min(0f)] private float _attackPunchScale = 0.18f;
    [Tooltip("스케일 펀치 시간(초).")]
    [SerializeField, Min(0.01f)] private float _attackPunchDuration = 0.2f;

    [Header("정렬")]
    [Tooltip("고정 슬롯 모드의 Y기반 정렬 차이. 궤도 모드는 SO의 앞/뒤 차이를 사용합니다.")]
    [SerializeField] private int _sortingOrderOffset = 1;

    private SpriteRenderer           _spriteRenderer;
    private Sprite                   _normalSprite;
    private DroneHoverAnimation      _hoverAnim;
    private CancellationTokenSource  _attackCts;
    private CancellationTokenSource  _flashCts;
    private Animator                 _animator;
    private SpineActorVisual        _spineVisual;
    /// <summary>Animated firing origin for normal shots, hacking and recorded attacks.</summary>
    public Vector3 MuzzlePosition => _spineVisual != null ? _spineVisual.MuzzlePosition : transform.position;
    private int _attackLifetime;
    private Vector3 _authoredScale = Vector3.one;
    private Vector3 _baseScale = Vector3.one;
    private Tween _punchTween;

    // ── 초기화 ──────────────────────────────────────────────────────

    private void Awake()
    {
        _hoverAnim = GetComponent<DroneHoverAnimation>();
        _spineVisual = GetComponent<SpineActorVisual>();
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _normalSprite = _spriteRenderer.sprite;
        _authoredScale = _baseScale = transform.localScale;
        _authoredRotation = transform.rotation;
    }

    private AnimatorUpdateMode _updateModeBeforePause;
    private bool _inPauseIdle;

    private bool _attackHeld;

    /// <summary>선택 화면이 열린 동안 새 공격을 보류한다 (이미 날아가는 투사체는 그대로). 풀리면 기다리던 사격부터 한다.</summary>
    public void SetAttackHold(bool on) => _attackHeld = on;

    /// <summary>레벨업 등 일시정지 중 부유/애니메이션을 unscaled로 유지한다 (시각 전용).</summary>
    public void SetPauseIdle(bool on)
    {
        if (on == _inPauseIdle) return;
        _inPauseIdle = on;
        if (_hoverAnim != null) _hoverAnim.SetForceUnscaled(on);
        _spineVisual?.SetPauseIdle(on);
        if (_animator == null) return;
        if (on) { _updateModeBeforePause = _animator.updateMode; _animator.updateMode = AnimatorUpdateMode.UnscaledTime; }
        else _animator.updateMode = _updateModeBeforePause;
    }

    /// <summary>DroneSpawnerBase.SpawnOneDrone() 에서 호출 — 스탯 주입 후 공격 루프 시작</summary>
    /// <param name="owner">스탯과 홈 위치를 제공하는 배치 유닛.</param>
    /// <param name="slotOffset">궤도가 꺼졌을 때 사용할 오너 기준 기존 슬롯.</param>
    /// <param name="formation">저작된 오너 치수와 공통 궤도 설정.</param>
    /// <param name="slotIndex">실제 소유 드론 목록의 순서.</param>
    /// <param name="slotCount">실제 대형의 드론 수.</param>
    /// <param name="orbitSeconds">기존 드론과 공유할 위상 시계.</param>
    public void Initialize(UnitBase owner, Vector2 slotOffset, DroneFormationSettings formation = null,
        int slotIndex = 0, int slotCount = 1, float orbitSeconds = 0f)
    {
        StopAll();
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        _owner                = owner;
        _homePositionFallback = transform.position;
        _spawnOffset          = slotOffset;
        _formation = formation;
        _usingOrbit = UsesOrbit;
        ConfigureOrbitSlot(slotIndex, slotCount, orbitSeconds);
        _ownerDrag            = owner != null ? owner.GetComponent<DragHandler>() : null;
        _followingOwnerDrag   = false;

        transform.rotation = _authoredRotation;
        ApplyFacingByCell();
        if (UsesOrbit && _formation.Orbit.FaceMotion)
            _facingSign = Mathf.Sin(OrbitAngle) < 0f ? -1f : 1f;
        ApplyVisualScale();
        transform.position = HomePosition;

        _droneManager?.RegisterDrone(this);

        _attackCts = new CancellationTokenSource();
        AttackLoopAsync(_attackCts.Token).Forget();

        StartOrbit();
    }

    /// <summary>추가/회수 후 실제 드론 수로 균등 배치하며 기존 위상 시계를 보존한다.</summary>
    public void ConfigureOrbitSlot(int index, int count, float orbitSeconds)
    {
        _orbitSlot = Mathf.Max(0, index); _orbitCount = Mathf.Max(1, count);
        _orbitSeconds = orbitSeconds;
    }

    // Match UnitBase's cell-facing rule on the whole rig so Spine and its muzzle mirror together.
    private void ApplyFacingByCell()
    {
        _baseScale = _authoredScale;
        if (UsesOrbit)
            _baseScale = Vector3.Scale(_baseScale, new Vector3(_formation.Orbit.DroneScale, _formation.Orbit.DroneScale, 1f));
        if (_owner?.currentCell != null && _owner._gridManager != null)
        {
            int halfColumn = _owner._gridManager.Columns / 2;
            bool faceLeft = _owner.currentCell.GridPosition.x >= halfColumn;
            float x = Mathf.Abs(_baseScale.x);
            if (x <= 0f) x = 1f;
            _baseScale.x = faceLeft ? x : -x;
        }
        _facingSign = Mathf.Sign(_baseScale.x);
        ApplyVisualScale();
    }

    private void ApplyVisualScale()
    {
        var scale = _baseScale;
        if (UsesOrbit && _formation.Orbit.FaceMotion) scale.x = Mathf.Abs(scale.x) * _facingSign;
        transform.localScale = scale * _punchFactor;
    }

    // ── 궤도/정렬 (위쪽 반은 뒤, 아래쪽 반은 앞) ──────────────────────

    private void LateUpdate()
    {
        if (_owner != null) UpdateOrbit();
        if (_spriteRenderer != null && _owner != null)
        {
            int offset = UsesOrbit ? (Mathf.Sin(OrbitAngle) > 0f
                ? _formation.Orbit.BackSortingOffset : _formation.Orbit.FrontSortingOffset) : _sortingOrderOffset;
            _spriteRenderer.sortingOrder = Mathf.RoundToInt(-_owner.transform.position.y * 100f) + offset;
        }
        FollowOwnerDrag();
    }

    private void UpdateOrbit()
    {
        if (_usingOrbit != UsesOrbit)
        {
            bool resume = _orbitRunning;
            StopOrbit(); _usingOrbit = UsesOrbit;
            ApplyFacingByCell(); transform.rotation = _authoredRotation;
            if (!_moving && !_followingOwnerDrag) transform.position = HomePosition;
            if (resume) StartOrbit();
        }
        if (!UsesOrbit) return;
        if (_hoverAnim != null && _hoverAnim.enabled) _hoverAnim.enabled = false;
        float delta = _inPauseIdle ? Time.unscaledDeltaTime : Time.deltaTime;
        if (_orbitRunning && !_moving && !_followingOwnerDrag && _ownerDrag?.IsDragging != true)
        {
            _orbitSeconds = Mathf.Repeat(_orbitSeconds + delta, _formation.Orbit.PeriodSeconds);
            transform.position = HomePosition;
            if (_formation.Orbit.FaceMotion)
            {
                float targetSign = Mathf.Sin(OrbitAngle) < 0f ? -1f : 1f;
                _facingSign = _formation.Orbit.TurnSeconds > 0f
                    ? Mathf.MoveTowards(_facingSign, targetSign, 2f * delta / _formation.Orbit.TurnSeconds)
                    : targetSign;
            }
        }
        if (!_formation.Orbit.FaceMotion) ApplyFacingByCell();
        else ApplyVisualScale();
    }

    /// <summary>
    /// 오너를 드래그하는 동안 위상/호버를 멈추고 현재 홈 오프셋으로 부드럽게 따라간다.
    /// 드래그가 끝나면 홈 위치에 붙은 뒤 궤도/호버를 재개한다.
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

    // ── 궤도/호버 제어 (DroneManager.ExecuteRallyAsync에서도 호출) ──

    /// <summary>현재 홈에서 SO 궤도를 재개한다. 궤도 비활성 시 기존 호버를 사용한다.</summary>
    public void StartOrbit()
    {
        if (!isActiveAndEnabled) return;
        _orbitRunning = true;
        _usingOrbit = UsesOrbit;
        if (UsesOrbit)
        {
            if (_hoverAnim != null) _hoverAnim.enabled = false;
            if (!_moving && !_followingOwnerDrag) transform.position = HomePosition;
            return;
        }
        if (_hoverAnim == null) return;
        _hoverAnim.enabled = false;
        _hoverAnim.enabled = true; // OnEnable → basePos 현재 위치로 리셋
        if (_inPauseIdle) _hoverAnim.SetForceUnscaled(true);
    }

    /// <summary>집결·드래그·반환 중 다른 위치 작성자가 움직일 수 있도록 위상과 호버를 멈춘다.</summary>
    public void StopOrbit()
    {
        _orbitRunning = false;
        if (_hoverAnim != null) _hoverAnim.enabled = false;
    }

    /// <summary>취소된 집결에서 현재 오너의 홈으로 복원하고 외형 궤도를 재개한다.</summary>
    public void ResumeHomeOrbit()
    {
        if (!isActiveAndEnabled) return;
        _moveCts?.Cancel();
        _moving = false; _followingOwnerDrag = false;
        transform.position = HomePosition;
        StartOrbit();
    }

    // ── 이동 (집결/귀환) ────────────────────────────────────────────

    /// <summary>SmoothStep으로 이동하며 궤도/호버를 중단한다. 외부 취소와 개체 생명주기를 함께 따른다.</summary>
    public async UniTask MoveToAsync(Vector3 target, float duration, CancellationToken token)
    {
        StopOrbit();
        _followingOwnerDrag = false;
        _moveCts?.Cancel();
        var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        _moveCts = source;
        _moving = true;
        try { await MoveToCoreAsync(target, duration, source.Token); }
        finally
        {
            if (_moveCts == source) { _moveCts = null; _moving = false; }
            source.Dispose();
        }
    }

    private async UniTask MoveToCoreAsync(Vector3 target, float duration, CancellationToken token)
    {
        if (token.IsCancellationRequested || !isActiveAndEnabled) return;
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

        if (!token.IsCancellationRequested && isActiveAndEnabled) transform.position = target;
    }

    /// <summary>집결 동안 멈춘 위상과 현재 오너 위치로 계산한 홈에 귀환한다.</summary>
    public UniTask ReturnToHomeAsync(float duration, CancellationToken token)
        => MoveToAsync(HomePosition, duration, token);

    // ── 집결 사격 (DroneManager 에서 일제 호출) ─────────────────────

    public void FireRallyShot()
    {
        if (_attackHeld) return;
        var bossArea = _bossManager?.CurrentBoss?.GetComponent<BossAreaTarget>();
        if (bossArea == null) return;
        ShootProjectile(bossArea.GetRandomWorldPosition());
    }

    // ── 스킬 발동 액션 스프라이트 (DroneSpawnerBase.FlashOwnedDrones 에서 호출) ──

    /// <summary>오너의 스킬 발동 순간 잠깐 액션 스프라이트로 바뀌었다가 자동으로 복귀한다.</summary>
    public void PlayActionFlash()
    {
        if (_spineVisual != null) _spineVisual.PlayAttack();
        else FlashSprite(_actionSprite, _actionSpriteHoldSeconds);
    }

    /// <summary>발사 순간 공격 프레임(날개 펼침)으로 바꾸고 스케일 펀치를 준다.</summary>
    private void PlayAttackFrame()
    {
        if (_spineVisual != null) { _spineVisual.PlayAttack(); return; }
        FlashSprite(_attackSprite, _attackSpriteHoldSeconds);

        if (_attackPunchScale <= 0f) return;
        _punchTween?.Kill();
        _punchFactor = 1f; ApplyVisualScale();
        _punchTween = DOTween.Punch(() => Vector3.one * _punchFactor,
            value => { _punchFactor = value.x; ApplyVisualScale(); },
            Vector3.one * _attackPunchScale, _attackPunchDuration, 1, 0.5f)
            .SetLink(gameObject);
    }

    private void FlashSprite(Sprite sprite, float holdSeconds)
    {
        if (_spriteRenderer == null || sprite == null) return;

        _flashCts?.Cancel();
        _flashCts?.Dispose();
        _flashCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        FlashSpriteAsync(sprite, holdSeconds, _flashCts.Token).Forget();
    }

    private async UniTaskVoid FlashSpriteAsync(Sprite sprite, float holdSeconds, CancellationToken token)
    {
        _spriteRenderer.sprite = sprite;

        if (await UniTask.Delay(Mathf.RoundToInt(holdSeconds * 1000f), cancellationToken: token).SuppressCancellationThrow())
            return;

        _spriteRenderer.sprite = _normalSprite;
    }

    /// <summary>데미지 없이 연출만 발사한다 (테스트 씬 프리뷰용).</summary>
    public void FireVisualShot(Vector3 targetPos) => ShootProjectile(targetPos);

    // ── 풀 반환 시 정리 ─────────────────────────────────────────────

    private void OnDisable()
    {
        StopAll();
        _droneManager?.UnregisterDrone(this);
    }

    private void OnDestroy() => StopAll();

    private void StopAll()
    {
        SetAttackHold(false);
        SetPauseIdle(false);
        _spineVisual?.ResetAnimation();
        _attackLifetime++;
        _attackCts?.Cancel();
        _attackCts?.Dispose();
        _attackCts = null;

        _flashCts?.Cancel();
        _flashCts?.Dispose();
        _flashCts = null;
        if (_spriteRenderer != null) _spriteRenderer.sprite = _normalSprite;
        _punchTween?.Kill();
        _punchTween = null;
        _punchFactor = 1f; ApplyVisualScale();
        var move = _moveCts; _moveCts = null;
        move?.Cancel(); // 이동 작업이 finally에서 Dispose한다.
        _moving = false; _followingOwnerDrag = false;
        StopOrbit();
    }

    // ── 공격 루프 ───────────────────────────────────────────────────

    private async UniTaskVoid AttackLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_gameManager?.IsFinished == true)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
                continue;
            }
            int delayMs;
            try { delayMs = RunStatMath.ToDelayMilliseconds(EffectiveAttackInterval); }
            catch (System.OverflowException error) { _run.Fail(error.Message); return; }

            if (await UniTask.Delay(delayMs, cancellationToken: token).SuppressCancellationThrow())
                return;
            while (_attackHeld)
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;

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
        if (_attackHeld || _gameManager?.IsFinished == true) return;
        var kind = critical ? BossDamageKind.Critical : BossDamageKind.Normal;
        var boss = _bossManager?.CurrentBoss;
        var bossArea = boss?.GetComponent<BossAreaTarget>();
        if (bossArea == null || boss.IsDead || _owner == null || _owner.IsStunned || _owner.currentCell == null) return;
        var cell = _owner.currentCell.Model;
        if (cell.IsSealed || cell.IsAttackDisabled || cell.TotemAttackDisabled) return;
        var owner = _owner;
        int lifetime = _attackLifetime;
        Vector3 origin = MuzzlePosition;
        var replay = owner.Combat?.BeginDroneBasicAttack(boss, transform,
            () => this != null && _attackLifetime == lifetime && _owner == owner && owner.currentCell != null);
        var source = owner.unitData;
        var recorded = replay?.IsEnabled == true ? new RecordedDamage(damage, kind, source) : null;
        bool originalHit = false;
        bool shadowArrivedEarly = false;
        
        Vector3 targetPos = bossArea.GetRandomWorldPosition();
        System.Action applyHit = () =>
        {
            if (boss != null && !boss.IsDead)
            {
                boss.ApplyProjectileImpact(() =>
                {
                    if (recorded != null) recorded.Apply(boss, targetPos);
                    else boss.TakeDamage(damage, targetPos, kind, source);
                });
            }
        };
        ShootProjectile(targetPos, () =>
        {
            applyHit();
            originalHit = true;
            if (shadowArrivedEarly && replay?.CanReplay == true) applyHit();
        });
        replay?.RecordShot(hit => ShootProjectile(targetPos, hit, origin), () =>
        {
            if (originalHit) applyHit();
            else shadowArrivedEarly = true;
        });
        owner.InvokeOnAttack();
    }

    private async UniTaskVoid ShootAfterHoldAsync(Vector3 target, System.Action callback, Vector3? origin, int lifetime)
    {
        if (await UniTask.WaitUntil(() => !_attackHeld, cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow()) return;
        if (_attackLifetime != lifetime || !isActiveAndEnabled) return;
        ShootProjectile(target, callback, origin);
    }

    private void ShootProjectile(Vector3 targetPos, System.Action onHitCallback = null, Vector3? recordedOrigin = null)
    {
        if (_attackHeld)
        {
            ShootAfterHoldAsync(targetPos, onHitCallback, recordedOrigin, _attackLifetime).Forget();
            return;
        }
        Vector3 origin = recordedOrigin ?? MuzzlePosition;
        PlayAttackFrame();

        if (_projectilePrefab != null)
        {
            var p = RM.Instantiate(_projectilePrefab, origin, Quaternion.identity, true);
            if (p != null)
            {
                if (!(p is DroneLaserProjectile beam) || !beam.IsSustainedBeam)
                    _audioManager?.PlayCue(GameAudioCue.DroneLaser);
                // 부모를 설정하지 않거나 null로 두어 WorldSpace 좌표계를 온전히 사용
                p.transform.SetParent(null);

                if (p is DroneLaserProjectile laser) laser.BindEmitter(recordedOrigin.HasValue ? null : _spineVisual);

                p.Launch(origin, targetPos, proj =>
                {
                    onHitCallback?.Invoke();
                    RM.Destroy(proj.gameObject);
                }, null, _owner);
            }
        }
        else if (_projectileManager != null)
        {
            _audioManager?.PlayCue(GameAudioCue.DroneLaser);
            _projectileManager.Launch(origin, targetPos, onHitCallback, _owner);
        }
    }
}
