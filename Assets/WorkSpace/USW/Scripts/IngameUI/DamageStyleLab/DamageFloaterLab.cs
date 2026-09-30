using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 플로터 연구실(FxLab_DamageFloater) 드라이버 — 같은 타격 흐름을 위(현재 인게임 설정)·아래(제안 설정) 두 보스에 동시에 띄워 비교한다.
/// 레퍼런스 design/데미지플로터예시.gif(모비노기) 분석값이 제안 에셋의 기본값이다.
///   현재 = BossDamageNumberSettings의 실행 중 복사본 (원본 에셋 보호)
///   제안 = DamageFloaterLabProposal 에셋 그대로 (플레이 중 인스펙터로 바꾸면 바로 반영·저장된다)
/// 보스 대역은 월드 SpriteRenderer라 인게임과 같은 경로(스프라이트 bounds → 화면)로 위치가 잡힌다.
/// FxLabCapture 캡처 대상 (Play() = 레퍼런스 박자, 1배속). 게임 코드와 무관한 실험실 전용.
/// </summary>
public sealed class DamageFloaterLab : MonoBehaviour, IFxLabPlayable
{
    /// <summary>타격 흐름 종류.</summary>
    public enum Pattern { None, Reference, Slow, Dense, Single }

    private const int PatternSeed = 20260930;
    private const float SingleHitDamage = 61234f;
    private const float MaxStep = 1f / 20f;

    [SerializeField] private DamageStyleLabSettings _currentSettings;
    [SerializeField] private DamageStyleLabSettings _proposalSettings;
    [SerializeField] private RectTransform _container;
    [SerializeField] private SpriteRenderer _topBoss;
    [SerializeField] private SpriteRenderer _bottomBoss;
    [Tooltip("보스 대역을 맞출 캔버스 위 자리 (크기 = 보스 크기)")]
    [SerializeField] private RectTransform _topBossSlot;
    [SerializeField] private RectTransform _bottomBossSlot;
    [SerializeField] private TextMeshProUGUI _status;
    [SerializeField] private float[] _speeds = { 1f, 0.5f, 0.25f };

    [Header("타격 흐름")]
    [Tooltip("레퍼런스 박자: GIF처럼 큰 숫자가 0.1~0.35초 간격, 치명타 위주")]
    [SerializeField] private Vector2 _referenceInterval = new Vector2(0.1f, 0.35f);
    [SerializeField] private float _slowInterval = 0.45f;
    [Tooltip("실전 밀도: 유닛 여럿이 동시에 때릴 때 (기존 샘플 0.07초)")]
    [SerializeField] private float _denseInterval = 0.07f;
    [Tooltip("때리는 유닛들의 기본 피해 (타격마다 하나를 고른다 — 등급·버프가 다른 여러 유닛)")]
    [SerializeField] private float[] _unitDamages = { 5000f, 12000f, 30000f };
    [Tooltip("인게임 기본 치명 배율 (LevelUpManager.CritDamageMultiplier 기본 1.5)")]
    [SerializeField] private float _criticalMultiplier = 1.5f;
    [SerializeField] private float _burnDamage = 900f;
    [Range(0f, 1f)] [SerializeField] private float _referenceCritChance = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float _critChance = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float _burnChance = 0.12f;
    [Tooltip("인게임 GameConfig.damageVariance와 같게 (0.1 = ±10%)")]
    [Range(0f, 0.5f)] [SerializeField] private float _damageSpread = 0.1f;
    [Tooltip("인게임 GameConfig.critDamageVariance와 같게")]
    [Range(0f, 0.5f)] [SerializeField] private float _critSpread = 0.1f;

    private DamageStyleLabSettings _currentCopy;
    private MobinogiDamageView _topView;
    private MobinogiDamageView _bottomView;
    private System.Random _rng = new System.Random(PatternSeed);
    private Pattern _pattern = Pattern.None;
    private float _clock;
    private float _nextHitAt;
    private int _speedIndex;
    private bool _useUnscaledTime = true;

    /// <inheritdoc />
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

    private float Speed => _speeds.Length > 0 ? _speeds[Mathf.Clamp(_speedIndex, 0, _speeds.Length - 1)] : 1f;

    private void Start()
    {
        if (_currentSettings == null || _proposalSettings == null || _container == null)
        {
            Debug.LogError("[DamageFloaterLab] 설정/컨테이너 미연결", this);
            enabled = false;
            return;
        }

        _currentCopy = Instantiate(_currentSettings);
        _currentCopy.name = _currentSettings.name + " (실행 중 복사본)";
        _topView = CreateView(_currentCopy, "Current");
        _bottomView = CreateView(_proposalSettings, "Proposal");
        SyncBosses();
        _topView.SetAnchor(_topBoss != null ? _topBoss.transform : null);
        _bottomView.SetAnchor(_bottomBoss != null ? _bottomBoss.transform : null);
        UpdateStatus();
    }

    private void OnDestroy()
    {
        if (_currentCopy != null) Destroy(_currentCopy);
    }

    private MobinogiDamageView CreateView(DamageStyleLabSettings settings, string name)
    {
        var root = DamageStyleLabUtil.CreateRoot(_container, name);
        var view = new MobinogiDamageView(settings, root);
        if (settings.Fonts != null && settings.Fonts.Length > 0) view.SetFont(settings.Fonts[0].Font, settings.Fonts[0].Material);
        return view;
    }

    private void LateUpdate()
    {
        if (_topView == null) return;
        SyncBosses();
        // 멈칫한 프레임(첫 프레임·에디터 끊김)에 시계가 튀어 여러 타격이 한꺼번에 나오지 않게 한 걸음을 제한한다.
        float dt = Mathf.Min(_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, MaxStep) * Speed;
        _clock += dt;
        while (_pattern != Pattern.None && _clock >= _nextHitAt) EmitHit();
        _topView.Tick(_clock, dt);
        _bottomView.Tick(_clock, dt);
        if (Time.frameCount % 15 == 0) UpdateStatus();
    }

    // ── 버튼 ──────────────────────────────────────────────────

    /// <summary>캡처용: 레퍼런스 박자를 1배속으로 처음부터.</summary>
    public void Play()
    {
        _speedIndex = 0;
        StartPattern(Pattern.Reference);
    }

    /// <summary>레퍼런스 박자 (GIF 비슷한 간격·치명타 위주).</summary>
    public void PlayReference() => StartPattern(Pattern.Reference);

    /// <summary>느린 타격 — 한 줄의 등장·유지·사라짐을 하나씩 보기.</summary>
    public void PlaySlow() => StartPattern(Pattern.Slow);

    /// <summary>실전 밀도 — 연타가 쏟아질 때 쌓임.</summary>
    public void PlayDense() => StartPattern(Pattern.Dense);

    /// <summary>치명타 한 방.</summary>
    public void PlaySingle() => StartPattern(Pattern.Single);

    /// <summary>타격 멈춤 (떠 있는 숫자는 끝까지 재생).</summary>
    public void Stop()
    {
        _pattern = Pattern.None;
        UpdateStatus();
    }

    /// <summary>1배 → 0.5배 → 0.25배 슬로모션 순환.</summary>
    public void NextSpeed()
    {
        _speedIndex = (_speedIndex + 1) % Mathf.Max(1, _speeds.Length);
        UpdateStatus();
    }

    private void StartPattern(Pattern pattern)
    {
        if (_topView == null) return;
        _rng = new System.Random(PatternSeed);
        _topView.Clear();
        _bottomView.Clear();
        _pattern = pattern;
        _nextHitAt = _clock;
        UpdateStatus();
    }

    // ── 타격 생성 (위·아래에 같은 값) ─────────────────────────

    private void EmitHit()
    {
        float hitTime = _nextHitAt;
        BossDamageKind kind;
        float damage;
        switch (_pattern)
        {
            case Pattern.Single:
                kind = BossDamageKind.Critical;
                damage = SingleHitDamage;
                _pattern = Pattern.None;
                break;
            case Pattern.Reference:
                kind = Roll(_burnChance) ? BossDamageKind.Burn : Roll(_referenceCritChance) ? BossDamageKind.Critical : BossDamageKind.Normal;
                damage = RollDamage(kind);
                _nextHitAt += Mathf.Lerp(_referenceInterval.x, _referenceInterval.y, (float)_rng.NextDouble());
                break;
            default:
                kind = Roll(_burnChance) ? BossDamageKind.Burn : Roll(_critChance) ? BossDamageKind.Critical : BossDamageKind.Normal;
                damage = RollDamage(kind);
                _nextHitAt += _pattern == Pattern.Slow ? _slowInterval : _denseInterval;
                break;
        }

        var amount = (decimal)Mathf.Round(damage);
        _topView.Add(amount, kind, hitTime);
        _bottomView.Add(amount, kind, hitTime);
    }

    private bool Roll(float chance) => _rng.NextDouble() < chance;

    // 인게임 UnitStatsModifier와 같은 모양: 기본 피해 × (1 ± 편차), 치명타면 × 치명 배율 × (1 ± 치명 편차).
    private float RollDamage(BossDamageKind kind)
    {
        if (kind == BossDamageKind.Burn) return _burnDamage;
        float unit = _unitDamages.Length > 0 ? _unitDamages[_rng.Next(_unitDamages.Length)] : 1000f;
        float damage = unit * Spread(_damageSpread);
        return kind == BossDamageKind.Critical ? damage * _criticalMultiplier * Spread(_critSpread) : damage;
    }

    private float Spread(float variance) => 1f + ((float)_rng.NextDouble() * 2f - 1f) * variance;

    // ── 보스 대역 · 상태 표시 ─────────────────────────────────

    // 화면 비율이 바뀌어도 대역이 캔버스 자리와 크기를 따라가게 한다.
    private static void FitBoss(SpriteRenderer boss, RectTransform slot)
    {
        if (boss == null || slot == null || boss.sprite == null) return;
        Vector3 p = slot.position;
        boss.transform.position = new Vector3(p.x, p.y, boss.transform.position.z);
        Vector2 worldSize = Vector2.Scale(slot.rect.size, slot.lossyScale);
        Vector2 spriteSize = boss.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
        float k = Mathf.Min(worldSize.x / spriteSize.x, worldSize.y / spriteSize.y); // 비율 유지
        boss.transform.localScale = new Vector3(k, k, 1f);
    }

    private void SyncBosses()
    {
        FitBoss(_topBoss, _topBossSlot);
        FitBoss(_bottomBoss, _bottomBossSlot);
    }

    private void UpdateStatus()
    {
        if (_status == null) return;
        string pattern = _pattern switch
        {
            Pattern.Reference => "레퍼런스 박자",
            Pattern.Slow => "느린 타격",
            Pattern.Dense => "실전 밀도",
            Pattern.Single => "한 방",
            _ => "멈춤",
        };
        _status.text = $"{pattern} / {Speed:0.##}배속\n"
            + $"<color=#9fd0ff>위 현재</color>  {Describe(_currentCopy)}\n"
            + $"<color=#ffd27f>아래 제안</color>  {Describe(_proposalSettings)}";
    }

    private static string Describe(DamageStyleLabSettings s)
    {
        if (s == null) return "-";
        float fade = s.MobiLifetime * (1f - s.MobiFadeStart);
        string entry = s.MobiEntry == DamageStyleLabSettings.MobiEntryStyle.Slam
            ? $"Slam {s.MobiSlamStartScale:0.##}배>1 {s.MobiSlamSeconds:0.##}s"
            : $"Pop 0>{1f + s.MobiPopOvershoot:0.##}배";
        string size = s.MobiRelativeSizePower > 0f ? $" / 피해 비례 크기 {s.MobiRelativeSizeRange.x:0.##}~{s.MobiRelativeSizeRange.y:0.##}배" : "";
        return $"수명 {s.MobiLifetime:0.##}s (유지 {s.MobiLifetime - fade:0.##} + 사라짐 {fade:0.##}) / 최대 {s.MobiMaxLines}줄 / {entry} / 흔들림 {s.MobiCriticalShake:0.#}{size}";
    }
}
