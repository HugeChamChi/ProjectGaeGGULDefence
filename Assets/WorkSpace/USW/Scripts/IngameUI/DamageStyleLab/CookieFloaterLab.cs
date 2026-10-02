using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 쿠키런 크럼블형 데미지 숫자 실험실(FxLab_CookieFloater) 드라이버 — 같은 타격 흐름을 위·아래 두 보스에 띄우고,
/// 각각 다른 후보(CookieFloaterLabSettings.Variants)를 골라 나란히 비교한다.
/// 후보: 기존 흩뿌리기 / 중앙 옅게·바깥 진하게 / 빠르게 위로 소멸 / 빠르게 대각선(바깥쪽 고정) / 중앙 옅게+빠르게 위로.
/// 상태 줄에 동시 표시 최대 수와 뜨는 범위 밖으로 나간 거리(이탈)를 보여 준다.
/// FxLabCapture 캡처 대상 (Play() = 실전 밀도, 1배속). 게임 코드와 무관한 실험실 전용.
/// </summary>
public sealed class CookieFloaterLab : MonoBehaviour, IFxLabPlayable
{
    /// <summary>타격 흐름 종류.</summary>
    public enum Pattern { None, Slow, Dense, Swarm, Single }

    private const int PatternSeed = 20261001;
    private const int TopViewSeed = 11;
    private const int BottomViewSeed = 11;
    private const float SingleHitDamage = 61234f;
    private const float MaxStep = 1f / 20f;
    private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.85f);

    [SerializeField] private CookieFloaterLabSettings _settings;
    [SerializeField] private RectTransform _container;
    [SerializeField] private SpriteRenderer _topBoss;
    [SerializeField] private SpriteRenderer _bottomBoss;
    [Tooltip("보스 대역을 맞출 캔버스 위 자리 (크기 = 보스 크기)")]
    [SerializeField] private RectTransform _topBossSlot;
    [SerializeField] private RectTransform _bottomBossSlot;
    [SerializeField] private TextMeshProUGUI _topLabel;
    [SerializeField] private TextMeshProUGUI _bottomLabel;
    [SerializeField] private TextMeshProUGUI _status;
    [Tooltip("위 후보 선택 버튼 (Variants 순서)")]
    [SerializeField] private Image[] _topButtons;
    [SerializeField] private Image[] _bottomButtons;
    [SerializeField] private int _topVariant;
    [SerializeField] private int _bottomVariant = 2;
    [SerializeField] private float[] _speeds = { 1f, 0.5f, 0.25f };

    [Header("타격 흐름")]
    [SerializeField] private float _slowInterval = 0.45f;
    [Tooltip("실전 밀도: 유닛 여럿이 동시에 때릴 때")]
    [SerializeField] private float _denseInterval = 0.07f;
    [Tooltip("폭주: 후반 유닛·드론이 가득 찼을 때")]
    [SerializeField] private float _swarmInterval = 0.025f;
    [Tooltip("때리는 유닛들의 기본 피해 (타격마다 하나를 고른다)")]
    [SerializeField] private float[] _unitDamages = { 5000f, 12000f, 30000f };
    [Tooltip("인게임 기본 치명 배율 (LevelUpManager.CritDamageMultiplier 기본 1.5)")]
    [SerializeField] private float _criticalMultiplier = 1.5f;
    [SerializeField] private float _burnDamage = 900f;
    [Range(0f, 1f)] [SerializeField] private float _critChance = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float _burnChance = 0.12f;
    [Tooltip("인게임 GameConfig.damageVariance와 같게 (0.1 = ±10%)")]
    [Range(0f, 0.5f)] [SerializeField] private float _damageSpread = 0.1f;

    private CookieFloaterVariantView _topView;
    private CookieFloaterVariantView _bottomView;
    private System.Random _rng = new System.Random(PatternSeed);
    private Pattern _pattern = Pattern.None;
    private float _clock;
    private float _nextHitAt;
    private int _speedIndex;
    private bool _showGuides = true;
    private bool _useUnscaledTime = true;

    /// <inheritdoc />
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

    private float Speed => _speeds.Length > 0 ? _speeds[Mathf.Clamp(_speedIndex, 0, _speeds.Length - 1)] : 1f;
    private int VariantCount => _settings != null && _settings.Variants != null ? _settings.Variants.Length : 0;

    private void Start()
    {
        if (_settings == null || _container == null || VariantCount == 0)
        {
            Debug.LogError("[CookieFloaterLab] 설정/컨테이너/후보 미연결", this);
            enabled = false;
            return;
        }

        _topView = new CookieFloaterVariantView(_settings, _container, "Top", TopViewSeed);
        _bottomView = new CookieFloaterVariantView(_settings, _container, "Bottom", BottomViewSeed);
        SyncBosses();
        _topView.SetAnchor(_topBoss);
        _bottomView.SetAnchor(_bottomBoss);
        _topView.SetVariant(Mathf.Clamp(_topVariant, 0, VariantCount - 1));
        _bottomView.SetVariant(Mathf.Clamp(_bottomVariant, 0, VariantCount - 1));
        Refresh();
        StartPattern(Pattern.Dense);
    }

    private void LateUpdate()
    {
        if (_topView == null) return;
        SyncBosses();
        // 멈칫한 프레임에 시계가 튀어 여러 타격이 한꺼번에 나오지 않게 한 걸음을 제한한다.
        float dt = Mathf.Min(_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, MaxStep) * Speed;
        _clock += dt;
        while (_pattern != Pattern.None && _clock >= _nextHitAt) EmitHit();
        _topView.Tick(_clock);
        _bottomView.Tick(_clock);
        if (Time.frameCount % 15 == 0) UpdateStatus();
    }

    // ── 버튼 ──────────────────────────────────────────────────

    /// <summary>캡처용: 실전 밀도를 1배속으로 처음부터.</summary>
    public void Play()
    {
        _speedIndex = 0;
        StartPattern(Pattern.Dense);
    }

    /// <summary>느린 타격 — 숫자 하나의 등장·이동·사라짐을 보기.</summary>
    public void PlaySlow() => StartPattern(Pattern.Slow);

    /// <summary>실전 밀도.</summary>
    public void PlayDense() => StartPattern(Pattern.Dense);

    /// <summary>폭주 — 숫자가 쏟아질 때 범위가 지켜지는지.</summary>
    public void PlaySwarm() => StartPattern(Pattern.Swarm);

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

    /// <summary>뜨는 범위·중앙 영역 표시선 켜기/끄기.</summary>
    public void ToggleGuides()
    {
        _showGuides = !_showGuides;
        if (_topView == null) return;
        _topView.ShowGuides = _bottomView.ShowGuides = _showGuides;
    }

    /// <summary>위 보스 후보 선택. 같은 타격 흐름을 처음부터 다시 재생한다.</summary>
    public void SelectTop(int index)
    {
        _topVariant = Mathf.Clamp(index, 0, Mathf.Max(0, VariantCount - 1));
        _topView?.SetVariant(_topVariant);
        Restart();
    }

    /// <summary>아래 보스 후보 선택.</summary>
    public void SelectBottom(int index)
    {
        _bottomVariant = Mathf.Clamp(index, 0, Mathf.Max(0, VariantCount - 1));
        _bottomView?.SetVariant(_bottomVariant);
        Restart();
    }

    private void Restart()
    {
        Refresh();
        StartPattern(_pattern == Pattern.None ? Pattern.Dense : _pattern);
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
        if (_pattern == Pattern.Single)
        {
            kind = BossDamageKind.Critical;
            damage = SingleHitDamage;
            _pattern = Pattern.None;
        }
        else
        {
            kind = Roll(_burnChance) ? BossDamageKind.Burn : Roll(_critChance) ? BossDamageKind.Critical : BossDamageKind.Normal;
            damage = RollDamage(kind);
            _nextHitAt += _pattern == Pattern.Slow ? _slowInterval : _pattern == Pattern.Swarm ? _swarmInterval : _denseInterval;
        }

        var amount = (decimal)Mathf.Round(damage);
        _topView.Add(amount, kind, hitTime);
        _bottomView.Add(amount, kind, hitTime);
    }

    private bool Roll(float chance) => _rng.NextDouble() < chance;

    private float RollDamage(BossDamageKind kind)
    {
        if (kind == BossDamageKind.Burn) return _burnDamage;
        float unit = _unitDamages.Length > 0 ? _unitDamages[_rng.Next(_unitDamages.Length)] : 1000f;
        float damage = unit * (1f + ((float)_rng.NextDouble() * 2f - 1f) * _damageSpread);
        return kind == BossDamageKind.Critical ? damage * _criticalMultiplier : damage;
    }

    // ── 보스 대역 · 표시 ──────────────────────────────────────

    // 화면 비율이 바뀌어도 대역이 캔버스 자리와 크기를 따라가게 한다.
    private static void FitBoss(SpriteRenderer boss, RectTransform slot)
    {
        if (boss == null || slot == null || boss.sprite == null) return;
        Vector3 p = slot.position;
        boss.transform.position = new Vector3(p.x, p.y, boss.transform.position.z);
        Vector2 worldSize = Vector2.Scale(slot.rect.size, slot.lossyScale);
        Vector2 spriteSize = boss.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
        float k = Mathf.Min(worldSize.x / spriteSize.x, worldSize.y / spriteSize.y);
        boss.transform.localScale = new Vector3(k, k, 1f);
    }

    private void SyncBosses()
    {
        FitBoss(_topBoss, _topBossSlot);
        FitBoss(_bottomBoss, _bottomBossSlot);
    }

    private void Refresh()
    {
        Highlight(_topButtons, _topVariant);
        Highlight(_bottomButtons, _bottomVariant);
        if (_topLabel != null) _topLabel.text = "위: " + (_topView?.Variant?.Name ?? "-");
        if (_bottomLabel != null) _bottomLabel.text = "아래: " + (_bottomView?.Variant?.Name ?? "-");
        UpdateStatus();
    }

    private static void Highlight(Image[] buttons, int selected)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null) buttons[i].color = i == selected ? ButtonSelected : ButtonIdle;
    }

    private void UpdateStatus()
    {
        if (_status == null || _topView == null) return;
        string pattern = _pattern switch
        {
            Pattern.Slow => "느린 타격",
            Pattern.Dense => "실전 밀도",
            Pattern.Swarm => "폭주",
            Pattern.Single => "한 방",
            _ => "멈춤",
        };
        _status.text = $"{pattern} / {Speed:0.##}배속 / 범위선 {(_showGuides ? "켬" : "끔")}\n"
            + $"<color=#9fd0ff>위</color>  {Describe(_topView)}\n"
            + $"<color=#ffd27f>아래</color>  {Describe(_bottomView)}";
    }

    private static string Describe(CookieFloaterVariantView view)
    {
        var v = view.Variant;
        if (v == null) return "-";
        string motion = v.Motion == CookieFloaterLabSettings.Motion.Diagonal ? $"대각선 {v.DiagonalAngle:0}°" : "위로";
        string zone = v.UseCenterZone ? $" / 중앙 {v.CenterShare * 100f:0}% 옅게({v.CenterAlpha:0.##})" : "";
        return $"{v.Name}: {motion} {v.Rise:0} / 수명 {v.Lifetime:0.##}s{zone} / 동시 최대 {view.PeakActive}개 / 이탈 위 {Mathf.Max(0f, view.MaxOverTop):0} 옆 {Mathf.Max(0f, view.MaxOverSide):0}";
    }
}
