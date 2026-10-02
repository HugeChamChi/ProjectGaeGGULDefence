using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 처치 공지 실험실(FxLab_BossClearNotice) 드라이버 (사용자 요청 2026-10-02).
/// 타이머 보너스 실험실(TimerBonusLab)과 같은 무대·같은 시계 위에서, 처치 직후 화면 가운데에
/// 공지를 빠르게 띄우고 빠르게 지운다 — 타이머 연출과 어울리는지 함께 본다.
/// 시안: A 밴드 슬라이스(어두운 띠) / B 골드 배너(노란 띠 + 둘째 줄) / C 젤리 필(쫀득한 알약) / D 크로스 스트라이크(교차 띠 + 히트스톱).
/// 타이머 실험실의 LateUpdate 뒤에 돈다 (같은 프레임의 시계·히트스톱을 그대로 쓴다). 실험실 전용.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class BossClearNoticeLab : MonoBehaviour, IFxLabPlayable
{
    private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ButtonSelected = new Color(0.95f, 0.7f, 0.2f, 0.85f);

    [SerializeField] private TimerBonusLab _timerLab;
    [SerializeField] private BossClearNoticeSettings _settings;
    [SerializeField] private TMP_FontAsset _font;
    [Tooltip("공지 루트 (화면 가운데 기준). 플래시보다 위, 버튼보다 아래")]
    [SerializeField] private RectTransform _root;
    [Tooltip("공지 시안 버튼 (A~D 순서)")]
    [SerializeField] private Image[] _variantButtons;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private int _variant;

    private IBossClearNotice[] _variants;
    private TimerBonusSprites _sprites;
    private TimerBonusParticles _particles;
    private RectTransform _layer;
    private float _lastKill = float.NegativeInfinity;
    private float _lastBonus = float.NaN;
    private string _subText;
    private bool _ready;

    /// <inheritdoc />
    public bool UseUnscaledTime
    {
        get => _timerLab != null && _timerLab.UseUnscaledTime;
        set { if (_timerLab != null) _timerLab.UseUnscaledTime = value; }
    }

    // ── 시안이 쓰는 문맥 ─────────────────────────────────────

    internal BossClearNoticeSettings Settings => _settings;
    internal TimerBonusLab TimerLab => _timerLab;
    internal TimerBonusSprites Sprites => _sprites;
    internal string Title => _settings.Title;
    internal string SubText => _subText;
    /// <summary>이번 프레임 진행 초 (히트스톱·정지 중 0).</summary>
    internal float Step { get; private set; }

    private void Start()
    {
        if (_timerLab == null || _settings == null || _font == null || _root == null)
        {
            Debug.LogError("[BossClearNoticeLab] 타이머 실험실/설정/폰트/루트 미연결", this);
            enabled = false;
            return;
        }
        _sprites = new TimerBonusSprites();
        _layer = TimerBonusLab.NewRect("Notices", _root, Vector2.zero);
        _variants = new IBossClearNotice[]
        {
            new NoticeBandSlice(),
            new NoticeGoldBanner(),
            new NoticeJellyPill(),
            new NoticeCrossStrike(),
        };
        foreach (var v in _variants) v.Setup(this);
        var particleLayer = TimerBonusLab.NewRect("NoticeParticles", _root, Vector2.zero);
        _particles = new TimerBonusParticles(particleLayer, _sprites.SoftDot, _sprites.Ring);
        _variant = Mathf.Clamp(_variant, 0, _variants.Length - 1);
        for (int i = 0; i < _variants.Length; i++) _variants[i].SetVisible(i == _variant);
        _ready = true;
        Refresh();
    }

    private void OnDestroy() => _sprites?.Dispose();

    private void LateUpdate()
    {
        if (!_ready || !_timerLab.IsReady) return;
        float kill = _timerLab.KillTime;
        if (kill < _lastKill) _particles.Clear();   // 반복 재시작
        Step = kill > _lastKill && !float.IsNegativeInfinity(_lastKill) ? kill - _lastKill : 0f;
        _lastKill = kill;
        UpdateSubText();

        _root.anchoredPosition = new Vector2(0f, _settings.NoticeY);
        _root.localScale = Vector3.one * _settings.Scale;
        _variants[_variant].Render(kill - _settings.DelayAfterKill, _settings.VisibleSeconds, _settings.ExitSpeedScale);
        _particles.Tick(Step);
    }

    // ── 버튼 ──────────────────────────────────────────────────

    /// <summary>캡처용: 타이머 실험실을 처음부터 (공지는 같은 시계를 따라간다).</summary>
    public void Play() => _timerLab.Play();

    /// <summary>공지 시안 선택 (0=A 밴드 슬라이스, 1=B 골드 배너, 2=C 젤리 필, 3=D 크로스 스트라이크). 처음부터 다시 재생.</summary>
    public void SelectNotice(int index)
    {
        if (!_ready) { _variant = index; return; }
        _variants[_variant].SetVisible(false);
        _variant = Mathf.Clamp(index, 0, _variants.Length - 1);
        _variants[_variant].SetVisible(true);
        _particles.Clear();
        _timerLab.Replay();
        Refresh();
    }

    // ── 시안이 부르는 도우미 (좌표·크기는 공지 루트 기준 캔버스 단위) ──

    internal TextMeshProUGUI CreateText(string name, float size, Color color)
    {
        var t = TimerBonusLab.NewText(name, _layer, _font, size, color);
        TimerBonusLab.Hide(t);
        return t;
    }

    internal Image CreateImage(string name, Color color, Sprite sprite = null, RectTransform parent = null)
        => TimerBonusLab.CreateImage(name, parent != null ? parent : _layer, color, sprite);

    /// <summary>이번 반복에서 처음이면 true (타이머 실험실과 같은 반복 단위로 초기화).</summary>
    internal bool Once(int key) => _timerLab.Once(key);

    internal void Emit(TimerBonusParticles.Particle p) => _particles.Emit(p);

    /// <summary>한 점에서 퍼지는 파티클 (캔버스 단위·초, angle 라디안 위 = +π/2, NaN이면 사방).</summary>
    internal void Burst(Vector2 at, int count, float minSpeed, float maxSpeed, Color[] colors,
        TimerBonusParticles.Kind kind = TimerBonusParticles.Kind.Spark, float gravity = 0f, float drag = 0.05f,
        float size0 = 3f, float size1 = 6f, float life0 = 0.25f, float life1 = 0.5f, float angle = float.NaN, float spread = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = float.IsNaN(angle) ? TimerBonusLab.Rnd(0f, Mathf.PI * 2f) : angle + TimerBonusLab.Rnd(-spread, spread);
            float speed = TimerBonusLab.Rnd(minSpeed, maxSpeed);
            _particles.Emit(new TimerBonusParticles.Particle
            {
                Kind = kind,
                Position = at,
                Velocity = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed,
                Gravity = gravity,
                Drag = drag,
                Size = TimerBonusLab.Rnd(size0, size1),
                MaxLife = TimerBonusLab.Rnd(life0, life1),
                Color = colors[i % colors.Length],
            });
        }
    }

    // ── 내부 ──────────────────────────────────────────────────

    private void UpdateSubText()
    {
        float bonus = _timerLab.Bonus;
        if (bonus == _lastBonus) return;
        _lastBonus = bonus;
        _subText = string.Format(CultureInfo.InvariantCulture, _settings.SubFormat, bonus.ToString("0", CultureInfo.InvariantCulture));
    }

    private void Refresh()
    {
        if (_variantButtons != null)
            for (int i = 0; i < _variantButtons.Length; i++)
                if (_variantButtons[i] != null) _variantButtons[i].color = i == _variant ? ButtonSelected : ButtonIdle;
        if (_label != null) _label.text = "공지: " + _variants[_variant].Name;
    }
}
