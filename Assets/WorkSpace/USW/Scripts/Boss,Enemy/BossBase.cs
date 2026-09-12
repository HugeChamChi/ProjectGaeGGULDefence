using UnityEngine;
using VContainer;
using System;
using DG.Tweening;
using Cysharp.Threading.Tasks;

/// <summary>
/// 모든 보스의 기반 추상 클래스
///
/// 변경 사항:
///   - Patterns 프로퍼티 추가: 프리팹 Inspector에서 패턴 SO를 직접 설정
///     BossPatternController가 이 배열을 등록하여 사용
///
/// 책임:
///   - HP 보유 및 데미지 처리
///   - 이벤트 발행 (OnHpChanged, OnDamaged, OnDeath)
///   - 패턴 실행 인터페이스 제공
///   - 패턴 데이터 보유 (프리팹 단위로 설정)
///
/// 보스 종류 추가 방법:
///   BossNormal, BossShaman, BossDragon 등 이 클래스를 상속
///   ExecutePattern() 오버라이드로 패턴별 동작 구현
/// </summary>
public abstract class BossBase : MonoBehaviour
{
    private DebuffCatalog _debuffCatalog;
    private GameManager _gameManager;
    private TimerController _combatTimer;
    private double _combatTimeOrigin;
    private double _defenseScale;
    private double _defense;
    private double _debuffTime;
    private bool _deathStarted;
    /// <summary>대상 소유 디버프 상태. 드론 매니저와 독립이다.</summary>
    public DebuffController Debuffs { get; private set; }
    /// <summary>명시적 보스 스폰 경로에서 루트 정의/씬 전투 상태를 전달한다.</summary>
    public void ConfigureDebuffs(DebuffCatalog catalog, DebuffSettings settings, GameManager gameManager, double defense, TimerController timer)
    {
        if (_combatTimer != null) _combatTimer.OnCombatTimeAdvanced -= OnCombatTimeAdvanced;
        _combatTimer = timer;
        if (_combatTimer != null) _combatTimer.OnCombatTimeAdvanced += OnCombatTimeAdvanced;
        _debuffCatalog = catalog; _gameManager = gameManager;
        _defenseScale = settings.DefenseScale; _defense = defense;
        _ = DamageCalculator.Calculate(0, defense, _defenseScale, 1, 1, 0);
    }
    private bool CombatAllowsDamage => _gameManager == null || _gameManager.CurrentState == GameManager.GameState.Playing;
    private void OnCombatTimeAdvanced(double elapsed) => AdvanceDebuffs();
    private void Update() => AdvanceDebuffs();
    private void AdvanceDebuffs()
    {
        if (_gameManager != null && (_gameManager.CurrentState == GameManager.GameState.Win || _gameManager.CurrentState == GameManager.GameState.Lose))
        { Debuffs?.Clear(); return; }
        if (IsDead || !CombatAllowsDamage) return;
        if (_combatTimer != null) _debuffTime = Math.Max(_debuffTime, _combatTimer.ElapsedCombatTime - _combatTimeOrigin);
        Debuffs?.Advance(_debuffTime);
    }
    /// <summary>스킬/아머 부여. 화염구는 ApplyDebuffImpact로 피해 전 스냅샷을 전달한다.</summary>
    public bool TryApplyDebuff(DebuffBinding binding, int sourceId)
    {
        AdvanceDebuffs();
        if (IsDead || Invincible || !CombatAllowsDamage || Debuffs == null || _debuffCatalog == null) return false;
        if (!_debuffCatalog.TryGet(binding.DebuffId, out var definition))
        { Debug.LogError($"Unknown debuff_id: {binding.DebuffId}"); return false; }
        return Debuffs.Apply(definition, new DebuffApplyContext(sourceId, binding.StacksPerApply, _health.CurrentUnits));
    }
    /// <summary>실제 적중 대상에서 기존 틱 → 스냅샷 → 즉발 피해 → 생존 시 디버프 순서를 보장한다.</summary>
    public void ApplyDebuffImpact(DebuffBinding binding, int sourceId, decimal impactDamage, Vector3? hitPos = null)
    {
        AdvanceDebuffs();
        if (IsDead || Invincible || !CombatAllowsDamage || _debuffCatalog == null || Debuffs == null) return;
        if (!_debuffCatalog.TryGet(binding.DebuffId, out var definition))
        { Debug.LogError($"Unknown debuff_id: {binding.DebuffId}"); return; }
        long snapshot = _health.CurrentUnits;
        TakeDamage(impactDamage, hitPos);
        if (!IsDead) Debuffs.Apply(definition, new DebuffApplyContext(sourceId, binding.StacksPerApply, snapshot));
    }

    // ── 패턴 데이터 (프리팹 Inspector에서 설정) ────────────────────
    [Header("보스 패턴")]
    [Tooltip("이 보스가 사용할 패턴 SO 목록 — 프리팹에 직접 설정")]
    [SerializeField] private BossPatternData[] _patterns;

    [Header("애니메이션")]
    [SerializeField] protected Animator _animator;

    public BossPatternData[] Patterns => _patterns;

    // ── 이벤트 ─────────────────────────────────────────────────────
    /// <summary>(현재HP, 최대HP) — UIManager가 구독하여 HP바 갱신</summary>
    public event Action<decimal, decimal> OnHpChanged;

    /// <summary>데미지량, 타격위치 — ExpManager가 구독하여 경험치 추가 및 데미지 플로터 띄움</summary>
    public event Action<decimal, Vector3?> OnDamaged;

    /// <summary>사망 — WaveManager가 구독하여 다음 웨이브 처리</summary>
    public event Action           OnDeath;

    /// <summary>보스 처치 시 전역 알림 — TotemBossKillStack 등에서 구독</summary>
    public static event Action OnAnyBossDied;

    // ── 상태 ────────────────────────────────────────────────────────
    private readonly CombatHealth _health = new CombatHealth();
    /// <summary>정확한 최대 HP. 내부 저장은 fixed-point long.</summary>
    public decimal MaxHp => _health.Max;
    /// <summary>정확한 현재 HP.</summary>
    public decimal CurrentHp => _health.Current;
    /// <summary>고정소수 정수 0에서 사망.</summary>
    public bool IsDead => _health.IsDead;

    /// <summary>true면 TakeDamage가 무시된다(체력 무한). 스킬 테스트 씬처럼 보스가 죽지 않아야
    /// 하는 특수 상황에서만 코드로 켠다 — 기본값 false로 일반 게임플레이엔 영향 없다.</summary>
    public bool Invincible { get; set; } = false;

    // ── 트윈 관련 ──────────────────────────────────────────────────
    private Vector3 _originalScale;
    private Tween _hitTween;

    // ── 초기화 ──────────────────────────────────────────────────────
    /// <summary>BossManager.SpawnBosses() 내부에서 WaveData의 hp 주입</summary>
    public void Init(decimal hp)
    {
        _health.Reset(hp);
        Debuffs?.Clear();
        Debuffs = new DebuffController(units => ApplyFinalDamage(units, null), () => !IsDead);
        _debuffTime = 0; _combatTimeOrigin = _combatTimer != null ? _combatTimer.ElapsedCombatTime : 0; _deathStarted = false;

        if (_originalScale == Vector3.zero)
        {
            _originalScale = transform.localScale;
        }

        if (_animator != null)
        {
            _animator.Play("Idle");
        }
    }

    // ── 데미지 처리 ─────────────────────────────────────────────────
    /// <summary>최종 경계에서만 4자리로 반올림하는 일반 피해 경로.</summary>
    public void TakeDamage(decimal amount, Vector3? hitPos = null)
    {
        AdvanceDebuffs();
        if (IsDead || Invincible || !CombatAllowsDamage || amount <= 0) return;
        if (_defenseScale <= 0) throw new InvalidOperationException("Boss debuff settings were not configured.");
        long units = DamageCalculator.Calculate(amount, _defense, _defenseScale,
            Debuffs?.ArmorFactor ?? 1, Debuffs?.DamageTakenMultiplier ?? 1, _health.CurrentUnits);
        ApplyFinalDamage(units, hitPos);
    }

    private void ApplyFinalDamage(long units, Vector3? hitPos)
    {
        if (IsDead || Invincible || !CombatAllowsDamage || units <= 0) return;
        long actual = _health.ApplyDamage(units);
        bool died = IsDead;
        if (died) { Debuffs?.Clear(); _deathStarted = true; }

        if (!IsDead)
        {
            PlayHitAnimation();
        }

        OnHpChanged?.Invoke(CurrentHp, MaxHp);
        OnDamaged?.Invoke((decimal)actual / CombatHealth.Scale, hitPos);

        if (died && _deathStarted)
        {
            HandleDeathAsync().Forget();
        }
    }

    private async UniTaskVoid HandleDeathAsync()
    {
        Debug.Log($"[BossBase] HandleDeathAsync called for {gameObject.name}");
        if (_animator != null)
        {
            Debug.Log($"[BossBase] Triggering 'Death' animation on {_animator.name}");
            _animator.ResetTrigger("Hit"); // 찌꺼기 트리거 초기화
            _animator.SetTrigger("Death");
            
            // 애니메이션 재생을 위해 1초 대기 후 파괴 이벤트 호출
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: this.GetCancellationTokenOnDestroy())
                         .SuppressCancellationThrow();
            if (cancelled) return;
            Debug.Log($"[BossBase] Finished 1-second death wait for {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[BossBase] _animator is null on {gameObject.name}!");
        }

        Debug.Log($"[BossBase] Invoking OnDeath and OnAnyBossDied for {gameObject.name}");
        OnDeath?.Invoke();
        OnAnyBossDied?.Invoke();
    }

    private void PlayHitAnimation()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("Hit");
        }
        else
        {
            _hitTween?.Kill();
            transform.localScale = _originalScale;
            _hitTween = transform.DOPunchScale(_originalScale * 0.2f, 0.15f, 0, 0f).SetLink(gameObject);
        }
    }

    // ── 패턴 실행 (자식 구현) ───────────────────────────────────────
    /// <summary>BossPatternController가 타이밍에 맞춰 호출</summary>
    public abstract void ExecutePattern(BossPatternData patternData);

    // ── 정리 ────────────────────────────────────────────────────────
    /// <summary>보스 교체/웨이브 종료 시 이벤트 구독 해제용</summary>
    public void ClearListeners()
    {
        if (_combatTimer != null) _combatTimer.OnCombatTimeAdvanced -= OnCombatTimeAdvanced;
        Debuffs?.Clear();
        OnHpChanged = null;
        OnDamaged   = null;
        OnDeath     = null;
    }

    private void OnDestroy()
    {
        if (_combatTimer != null) _combatTimer.OnCombatTimeAdvanced -= OnCombatTimeAdvanced;
        Debuffs?.Clear();
    }
}
