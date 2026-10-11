using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TimerBonusEase;

/// <summary>
/// 보스 처치 시간 보너스 연출 실험실(FxLab_TimerBonus) 드라이버 (사용자 요청 2026-10-02).
/// HTML 시안 4종(합체·슬롯 릴·질주·스탬프)과 E 해킹(글리치, 2026-10-09)을 같은 시계로 반복 재생한다:
/// 처치 전 긴장(드론 레이저·빨간 박동·미세 떨림, 보스 피격 반응 없음) → 처치 순간 히트스톱·플래시·줌 → 시안별 보너스 연출 → 카운트다운.
/// 히트스톱은 실험실 시계만 멈춘다. 인게임에 붙일 때는 Time.timeScale 대신 TimeScaleService.Request/Release.
/// 게임 코드와 무관한 실험실 전용. FxLabCapture 캡처 대상 (Play() = 현재 시안 1배속 처음부터).
/// 인게임 바인딩(_runtimeTimer 연결 시): A 합체(보스 처치 보너스, Present)와 E 해킹 + J 중첩(디시그망 시간 회복, PresentHack)만 쓴다.
/// 해킹 중에는 HUD 타이머를 숨기고 같은 글꼴·재질의 숫자 사본(정수 3자리)을 그 자리에 그린다. 실제 시간은 건드리지 않는다.
/// </summary>
public sealed class TimerBonusLab : MonoBehaviour, IFxLabPlayable
{
    private const int KeyKill = 1;
    private const int KeyBossBurst = 2;
    private const int KeyBeatBase = 2000;
    private const float MaxStep = 1f / 20f;
    private const float FrameRef = 60f;               // HTML 시안의 '프레임당 감쇠'를 초 단위로 바꾸는 기준
    private const float BossPopSeconds = 0.11f;
    private const float BossShrinkSeconds = 0.15f;
    private const float BossRespawnSeconds = 0.38f;
    private const float HpBarFraction = 0.3f;         // 처치 직전 남은 체력 표시
    private const float PreShakeStart = 0.65f;
    private const float PreShakeRamp = 0.35f;
    private const float PulseDecay = 0.86f;
    private const float FlashDecay = 0.8f;
    private const float ZoomDecay = 0.86f;
    private const float DangerHoldDecay = 0.9f;
    private const float DangerFadeDecay = 0.82f;
    private const float GlowGoldAlpha = 0.55f;
    private const float GlowFlashAlpha = 0.3f;
    private const float FlashWhiten = 0.7f;
    private const int RuntimeIntDigits = 3;           // 인게임 타이머는 100초를 넘을 수 있다 (90초 시작 + 보스 처치 보너스)
    private const int RuntimeFusionConcept = 0;
    private const int RuntimeHackConcept = 1;
    private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.85f);
    private static readonly Color Pink = new Color(1f, 0.84f, 0.87f);

    [SerializeField] private TimerBonusLabSettings _settings;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Sprite _bossSprite;
    [Tooltip("연출 영역 (가운데 기준 좌표)")]
    [SerializeField] private RectTransform _stage;
    [Tooltip("화면 전체 플래시 (버튼보다 아래 형제)")]
    [SerializeField] private RectTransform _overlay;
    [SerializeField] private TextMeshProUGUI _status;
    [Tooltip("시안 선택 버튼 (A~D 순서)")]
    [SerializeField] private Image[] _conceptButtons;
    [SerializeField] private Vector2 _timerPosition = new Vector2(0f, 560f);
    [Tooltip("보스 발밑 위치")]
    [SerializeField] private Vector2 _bossPosition = new Vector2(0f, -330f);
    [SerializeField] private float _bossSize = 460f;
    [SerializeField] private float[] _speeds = { 1f, 0.5f, 0.25f };
    [Header("처치 전 드론 공격 (실제 유닛 프리팹·레이저, 피격 이펙트 없음)")]
    [Tooltip("드론 프리팹·레이저 연결 (빌더가 채움). 자리·크기·간격은 설정 에셋에서 조정")]
    [SerializeField] private TimerBonusDroneSquad.Entry[] _droneSquad;
    [SerializeField] private int _concept;
    [Tooltip("해킹 중첩 시안(G~J) 발동 시점 묶음 번호 — '발동 타이밍' 버튼이 순환")]
    [SerializeField] private int _triggerSetIndex;
    [SerializeField] private bool _loop = true;
    [Header("Live game binding")]
    [SerializeField] private TMP_Text _runtimeTimer;
    private TimerController _liveClock;
    private float _liveBonus;
    private bool _liveBonusPending;
    private Vector2 _liveBossCenter;
    private Vector2 _timerBasePosition;
    private Vector3 _timerBaseScale;
    private Color _timerBaseColor;
    private Vector2 _liveTimerSize;
    private float _liveFontSize;
    private float _liveScaleRatio = 1f;
    private bool _hackActive;
    private TimerBonusHackStack _liveHack;
    private readonly List<float> _liveTriggers = new List<float>();
    private readonly List<float> _liveTriggerBonus = new List<float>();
    private readonly List<Vector2> _liveTriggerOrigin = new List<Vector2>();
    private float[] _liveTriggerArray = System.Array.Empty<float>();
    private float _liveAdded;

    /// <summary>True only while a real time addition is being presented.</summary>
    public bool IsPresenting => _runtimeTimer != null && _running && _ready;
    internal bool IsRuntime => _runtimeTimer != null;

    /// <summary>Plays A against the authoritative timer. Does not add time or spawn lab actors.</summary>
    public void Present(TimerController clock, float added, Vector2 bossCenter, bool bonusPending = false)
    {
        if (!_ready) Start();
        if (!_ready) return;
        EndHack();
        _liveClock = clock;
        _liveBonus = added;
        _liveBonusPending = bonusPending;
        _liveBossCenter = bossCenter;
        UpdateBonusText();
        Restart();
        _t = _settings.PreKillSeconds;
        _shakeRoot.gameObject.SetActive(true);
        _flash.gameObject.SetActive(true);
    }

    /// <summary>True when the A chip has reached the timer, using the actual animation clock including hit stops.</summary>
    public bool HasReachedImpact => _ready && KillTime >= _settings.Fusion.FlySeconds;
    /// <summary>True when the A timer counter has finished rising.</summary>
    public bool IsBonusCountComplete => _ready && KillTime >= _settings.Fusion.FlySeconds + _settings.Fusion.CountSeconds;
    /// <summary>Called immediately after the authoritative countdown receives the pending bonus.</summary>
    public void NotifyBonusApplied() => _liveBonusPending = false;

    /// <summary>True while the E hack (Disigman time recovery) is on screen.</summary>
    public bool IsPresentingHack => IsPresenting && _hackActive;

    /// <summary>
    /// E 해킹 + J 중첩 — 이미 실제 타이머에 붙은 해킹 시간을 표시만 한다 (시간은 호출 전에 AddTime으로 반영돼 있어야 한다).
    /// 해킹이 화면에 있으면 발동을 더하고(합산/이어 붙임), A 합체가 진행 중이면 표시하지 않는다.
    /// originOnStage는 신호가 출발할 연출 영역 좌표(그 유닛 위치).
    /// </summary>
    public void PresentHack(TimerController clock, float added, Vector2 originOnStage)
    {
        if (!IsRuntime || added <= 0f || clock == null) return;
        if (!_ready) Start();
        if (!_ready || _liveHack == null) return;
        if (IsPresenting && !_hackActive) return;   // 보스 처치 연출이 타이머를 쓰는 중

        if (!IsPresentingHack)
        {
            _liveClock = clock;
            _liveBonus = added;
            _liveBonusPending = false;
            _liveTriggers.Clear();
            _liveTriggerBonus.Clear();
            _liveTriggerOrigin.Clear();
            _liveAdded = 0f;
            _liveScaleRatio = _stage.lossyScale.x > 1e-6f ? _runtimeTimer.rectTransform.lossyScale.x / _stage.lossyScale.x : 1f;
            SwitchConcept(RuntimeHackConcept);
            _hackActive = true;
            UpdateBonusText();
            Restart();
            _t = _settings.PreKillSeconds;
            _shakeRoot.gameObject.SetActive(true);
            _flash.gameObject.SetActive(true);
            _timerRoot.gameObject.SetActive(true);
        }
        _liveTriggers.Add(Mathf.Max(0f, KillTime));
        _liveTriggerBonus.Add(added);
        _liveTriggerOrigin.Add(originOnStage);
        _liveTriggerArray = _liveTriggers.ToArray();
        _liveAdded += added;
    }

    /// <summary>Releases timer appearance on completion, scene exit or a stopped run.</summary>
    public void CancelPresentation()
    {
        if (!IsRuntime || !_ready) return;
        EndHack();
        _running = false;
        _particles.Clear();
        if (_shakeRoot != null) _shakeRoot.gameObject.SetActive(false);
        if (_flash != null) _flash.gameObject.SetActive(false);
        if (_runtimeTimer != null)
        {
            _runtimeTimer.rectTransform.anchoredPosition = _timerBasePosition;
            _runtimeTimer.rectTransform.localScale = _timerBaseScale;
            _runtimeTimer.color = _timerBaseColor;
            if (_liveClock != null) _runtimeTimer.text = _liveClock.RemainingTime.ToString("F2", CultureInfo.InvariantCulture);
        }
    }

    private ITimerBonusConcept[] _concepts;
    private TimerBonusSprites _sprites;
    private TimerBonusParticles _particles;
    private TimerBonusDroneSquad _drones;
    private TimerBonusDigits _main;
    private TimerBonusDigits _ghostCyan;
    private TimerBonusDigits _ghostHot;
    private RectTransform _shakeRoot;
    private RectTransform _timerRoot;
    private RectTransform _bossRoot;
    private RectTransform _hpFill;
    private RectTransform _fx;
    private Image _bossImage;
    private Image _bossGlow;
    private Image _hpBack;
    private Image _hpFillImage;
    private Image _timerGlow;
    private Image _flash;
    private readonly HashSet<int> _fired = new HashSet<int>();

    private float _t;
    private bool _running = true;
    private bool _ready;
    private bool _useUnscaledTime = true;
    private int _speedIndex;
    private int _bonusIndex;
    private string _bonusText;
    private string _bonusShortText;
    // 화면 공통 상태 (감쇠)
    private float _hold;
    private float _pulse;
    private float _zoom;
    private float _whiteFlash;
    private float _danger;
    // 이번 프레임에 시안이 정하는 값 (매 프레임 초기화)
    private Vector2 _shake;
    private Vector2 _timerOffset;
    private Vector2 _timerScale = Vector2.one;
    private float _timerSkew;
    private float _gold;
    private float _timerFlash;
    private float _ghostOffset;
    private float _ghostAlpha;
    private float _timerAlpha = 1f;
    private float _hack;

    /// <inheritdoc />
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

    // ── 시안이 쓰는 문맥 ─────────────────────────────────────

    internal TimerBonusLabSettings Settings => _settings;
    internal float Px => _settings.PxScale;
    internal float Bonus => IsRuntime ? _liveBonus : _settings.Bonuses.Length > 0 ? _settings.Bonuses[_bonusIndex] : 0f;
    /// <summary>중첩 해킹 i번째 발동의 보너스 (인게임은 발동마다 실제 회복량, 실험실은 공통 보너스).</summary>
    internal float StackTriggerBonus(int i) => IsRuntime && i < _liveTriggerBonus.Count ? _liveTriggerBonus[i] : Bonus;
    /// <summary>인게임: i번째 발동 유닛의 연출 영역 좌표.</summary>
    internal bool TryGetTriggerOrigin(int i, out Vector2 origin)
    {
        bool ok = i >= 0 && i < _liveTriggerOrigin.Count;
        origin = ok ? _liveTriggerOrigin[i] : Vector2.zero;
        return ok;
    }
    /// <summary>"+15.00"</summary>
    internal string BonusText => _bonusText;
    /// <summary>"+15"</summary>
    internal string BonusShortText => _bonusShortText;
    /// <summary>지금 남은 시간 (보너스 제외).</summary>
    internal double Now => !IsRuntime ? _settings.BaseRemaining - _t
        : _liveClock == null ? 0d
        : _hackActive ? _liveClock.RemainingTime - _liveAdded   // 해킹으로 붙은 시간은 모두 빼고 (해킹 렌더러가 단계별로 더해 보인다)
        : _liveClock.RemainingTime - (_liveBonusPending ? 0f : _liveBonus);
    internal Vector2 TimerPosition => IsRuntime ? (Vector2)_stage.InverseTransformPoint(_runtimeTimer.transform.position) : _timerPosition;
    internal Vector2 TimerSize => IsRuntime && !_hackActive ? _liveTimerSize : _main.Size;
    /// <summary>타이머 숫자 글자 크기 (인게임은 HUD 타이머 크기).</summary>
    internal float TimerFontSize => IsRuntime ? _liveFontSize : _settings.TimerFontSize;
    /// <summary>타이머 숫자 정수 자릿수 (인게임 3, 실험실 2).</summary>
    internal int TimerIntDigits => IsRuntime ? RuntimeIntDigits : 2;
    /// <summary>숫자 사본이 글꼴 재질·스타일을 따라 할 원본 (인게임 HUD 타이머, 실험실은 없음).</summary>
    internal TMP_Text TimerStyleSource => IsRuntime ? _runtimeTimer : null;
    /// <summary>평소 타이머 색.</summary>
    internal Color TimerBaseColor => IsRuntime ? _timerBaseColor : _settings.TimerColor;
    internal Vector2 BossCenter => IsRuntime ? _liveBossCenter : _bossPosition + new Vector2(0f, _bossSize * 0.5f);
    internal Vector2 StageSize => _stage.rect.size;
    internal TimerBonusDigits MainDigits => _main;
    /// <summary>타이머 묶음 루트 — 여기 붙인 자식은 타이머 이동·박동·크기를 같이 따른다 (본체보다 위에 그려짐).</summary>
    internal RectTransform TimerRoot => _timerRoot;
    internal TMP_FontAsset Font => IsRuntime && _runtimeTimer.font != null ? _runtimeTimer.font : _font;
    internal RectTransform FxLayer => _fx;
    internal float Pulse { get => _pulse; set => _pulse = value; }
    internal float Zoom { get => _zoom; set => _zoom = value; }
    /// <summary>처치 순간 기준 실험실 시계 (처치 전 음수). 히트스톱·슬로모션이 반영된다 — 공지 실험실이 같은 박자로 맞춘다.</summary>
    internal float KillTime => _t - _settings.PreKillSeconds;
    internal bool IsReady => _ready;

    /// <summary>해킹 중첩 시안(G~J)의 현재 발동 시점 묶음 (처치 기준 초).</summary>
    internal float[] StackTriggerTimes
    {
        get
        {
            if (IsRuntime) return _liveTriggerArray;
            var sets = _settings.Hack.StackTriggerSets;
            if (sets == null || sets.Length == 0) return System.Array.Empty<float>();
            return sets[Mathf.Clamp(_triggerSetIndex, 0, sets.Length - 1)].Times ?? System.Array.Empty<float>();
        }
    }

    private float LoopSeconds => _settings.PreKillSeconds + _settings.AnimSeconds + _settings.PostSeconds;
    private float Speed => _speeds.Length > 0 ? _speeds[Mathf.Clamp(_speedIndex, 0, _speeds.Length - 1)] : 1f;

    /// <summary>처치 시점 기준 a초의 남은 시간 (보너스 제외).</summary>
    internal double RemainingAt(float a) => IsRuntime ? Now - (a - KillTime) : _settings.BaseRemaining - (_settings.PreKillSeconds + a);

    // ── 생명주기 ──────────────────────────────────────────────

    private void Start()
    {
        if (_ready) return;
        if (_settings == null || _font == null || _stage == null || _overlay == null)
        {
            Debug.LogError("[TimerBonusLab] 설정/폰트/영역 미연결", this);
            enabled = false;
            return;
        }
        _bonusIndex = Mathf.Clamp(_settings.DefaultBonusIndex, 0, Mathf.Max(0, _settings.Bonuses.Length - 1));
        UpdateBonusText();
        BuildView();
        var stackHack = new TimerBonusHack();   // G~J가 같이 쓰는 해킹 렌더러 (인게임은 J만)
        if (IsRuntime) _liveHack = new TimerBonusHackStack(stackHack, TimerBonusHackStack.Mode.MergeMini);
        _concepts = IsRuntime ? new ITimerBonusConcept[] { new TimerBonusFusion(), _liveHack } : new ITimerBonusConcept[]
        {
            new TimerBonusFusion(),
            new TimerBonusSlotReel(),
            new TimerBonusDash(),
            new TimerBonusStamp(),
            new TimerBonusHack(),
            new TimerBonusHack(countUp: true),
            new TimerBonusHackStack(stackHack, TimerBonusHackStack.Mode.Restart),
            new TimerBonusHackStack(stackHack, TimerBonusHackStack.Mode.Queue),
            new TimerBonusHackStack(stackHack, TimerBonusHackStack.Mode.Merge),
            new TimerBonusHackStack(stackHack, TimerBonusHackStack.Mode.MergeMini),
        };
        foreach (var c in _concepts) c.Setup(this);
        _concept = Mathf.Clamp(_concept, 0, _concepts.Length - 1);
        for (int i = 0; i < _concepts.Length; i++) _concepts[i].SetVisible(i == _concept);
        _ready = true;
        Restart();
        Refresh();
        if (IsRuntime) CancelPresentation();
    }

    private void OnDestroy()
    {
        CancelPresentation();
        _drones?.Dispose();
        _sprites?.Dispose();
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        if (IsRuntime && !_running) return;
        float dt = Mathf.Min(_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, MaxStep) * Speed;
        // 히트스톱: 시계·파티클이 같이 멈추고, 플래시·줌만 계속 줄어든다.
        bool frozen = _running && _hold > 0f;
        if (frozen) _hold -= dt;
        if (_running && !frozen)
        {
            _t += dt;
            if (_t >= LoopSeconds && !_hackActive)
            {
                if (_loop) Restart();
                else { _t = LoopSeconds; _running = false; }
            }
        }
        float step = _running && !frozen ? dt : 0f;
        float fadeStep = _running ? dt : 0f;
        _pulse *= Decay(PulseDecay, step);

        float a = _t - _settings.PreKillSeconds;
        BeginFrame();
        if (!IsRuntime)
        {
            RenderTension(a, step);
            RenderBoss(a);
            _drones.Render(_t, a < 0f);
        }
        _concepts[_concept].Render(a, step);
        _particles.Tick(step);

        _whiteFlash *= Decay(FlashDecay, fadeStep);
        _zoom *= Decay(ZoomDecay, fadeStep);
        ApplyFrame();
        if (IsRuntime && (_hackActive ? a >= _liveHack.VisibleEnd : a >= _settings.AnimSeconds)) CancelPresentation();
        if (Time.frameCount % 6 == 0) UpdateStatus();
    }

    // ── 버튼 ──────────────────────────────────────────────────

    /// <summary>캡처용: 현재 시안을 1배속으로 처음부터.</summary>
    public void Play()
    {
        _speedIndex = 0;
        Restart();
    }

    /// <summary>처음부터 다시.</summary>
    public void Replay() => Restart();

    /// <summary>시안 선택 (0=A 합체, 1=B 슬롯 릴, 2=C 질주, 3=D 스탬프, 4=E 해킹, 5=F 해킹 상승, 6~9=G~J 해킹 중첩 재시작·대기열·합산·합산+미니).</summary>
    public void SelectConcept(int index)
    {
        if (!_ready) { _concept = index; return; }
        _concepts[_concept].SetVisible(false);
        _concept = Mathf.Clamp(index, 0, _concepts.Length - 1);
        _concepts[_concept].SetVisible(true);
        Restart();
        Refresh();
    }

    /// <summary>1배 → 0.5배 → 0.25배 슬로모션 순환.</summary>
    public void NextSpeed()
    {
        _speedIndex = (_speedIndex + 1) % Mathf.Max(1, _speeds.Length);
        UpdateStatus();
    }

    /// <summary>보너스 초 순환 (+5 / +10 / +15 / +30).</summary>
    public void NextBonus()
    {
        _bonusIndex = (_bonusIndex + 1) % Mathf.Max(1, _settings.Bonuses.Length);
        UpdateBonusText();
        Restart();
        UpdateStatus();
    }

    /// <summary>해킹 중첩 시안(G~J)의 발동 시점 묶음 순환 (몰아서 / 끝날 때쯤 / 연타 …).</summary>
    public void NextTriggerSet()
    {
        var sets = _settings.Hack.StackTriggerSets;
        _triggerSetIndex = (_triggerSetIndex + 1) % Mathf.Max(1, sets != null ? sets.Length : 1);
        Restart();
        UpdateStatus();
    }

    /// <summary>반복 재생 켜기/끄기.</summary>
    public void ToggleLoop()
    {
        _loop = !_loop;
        if (_loop && !_running) Restart();
        UpdateStatus();
    }

    // ── 시안이 부르는 도우미 ──────────────────────────────────

    /// <summary>타이머 세 겹(본체·잔상 2장)에 같은 값을 쓴다.</summary>
    internal void SetTimerValue(double seconds)
    {
        if (IsRuntime && !_hackActive) { _runtimeTimer.text = System.Math.Max(0d, seconds).ToString("F2", CultureInfo.InvariantCulture); return; }
        _main.SetValue(seconds);
        _ghostCyan.SetValue(seconds);
        _ghostHot.SetValue(seconds);
    }

    /// <summary>이번 프레임 타이머 변형. tx·ty는 px, skew는 도 (양수 = 윗부분 오른쪽).</summary>
    internal void TimerTransform(float tx, float ty, float sx, float sy, float skew)
    {
        _timerOffset = new Vector2(tx, ty) * Px;
        _timerScale = new Vector2(sx, sy);
        _timerSkew = skew;
    }

    /// <summary>자릿수 하나를 세 겹(본체·잔상 2장)에 같이 쓴다 (reel 0~3, digit 0~9).</summary>
    internal void SetTimerDigit(int reel, int digit)
    {
        _main.SetDigit(reel, digit);
        _ghostCyan.SetDigit(reel, digit);
        _ghostHot.SetDigit(reel, digit);
    }

    /// <summary>정수 3자리 숫자(인게임)에서 백의 자리 칸 숨김 — 본체·잔상 2장에 같이.</summary>
    internal void SetTimerLeadingHidden(bool hidden)
    {
        _main.SetLeadingHidden(hidden);
        _ghostCyan.SetLeadingHidden(hidden);
        _ghostHot.SetLeadingHidden(hidden);
    }

    /// <summary>타이머 본체 투명도 (0~1, 잔상은 SetGhosts로 따로). 시안이 본체를 직접 대신 그릴 때 0.</summary>
    internal void SetTimerAlpha(float alpha) => _timerAlpha = alpha;

    /// <summary>타이머 하늘색(해킹) 정도 (0~1).</summary>
    internal void SetHack(float h) => _hack = h;

    /// <summary>타이머 금색 정도 (0~1).</summary>
    internal void SetGold(float g) => _gold = g;

    /// <summary>타이머 하얗게 번쩍임 (0~1).</summary>
    internal void SetFlash(float f) => _timerFlash = f;

    /// <summary>RGB 잔상 — 좌우 간격(px)과 투명도.</summary>
    internal void SetGhosts(float offsetPx, float alpha)
    {
        _ghostOffset = offsetPx * Px;
        _ghostAlpha = alpha;
    }

    /// <summary>화면 흔들림 추가 (px).</summary>
    internal void AddShake(float x, float y) => _shake += new Vector2(x, y) * Px;

    /// <summary>충돌 순간 공통 타격감: 히트스톱 + 화면 플래시 + 줌 펀치.</summary>
    internal void Impact(float strength)
    {
        _hold = Mathf.Max(_hold, _settings.ImpactHitStop * strength);
        _whiteFlash = Mathf.Max(_whiteFlash, _settings.ImpactFlash * strength);
        _zoom = Mathf.Max(_zoom, _settings.ImpactZoom * strength);
    }

    /// <summary>히트스톱만 (연출 시계 정지 — 게임 시간은 건드리지 않음). 인게임 해킹 완료처럼 화면 플래시 없이 타이머만 터뜨릴 때.</summary>
    internal void HitStop(float strength) => _hold = Mathf.Max(_hold, _settings.ImpactHitStop * strength);

    /// <summary>이번 반복에서 처음이면 true (한 번만 터지는 이벤트).</summary>
    internal bool Once(int key) => _fired.Add(key);

    internal static float Rnd(float min, float max) => Random.Range(min, max);

    /// <summary>파티클 하나 (캔버스 단위 그대로).</summary>
    internal void Emit(TimerBonusParticles.Particle p) => _particles.Emit(p);

    /// <summary>
    /// 한 점에서 퍼지는 파티클 묶음. 속도·중력·크기는 px (× Px), angle은 라디안 (위 = +π/2), NaN이면 사방.
    /// </summary>
    internal void Burst(Vector2 at, int count, float minSpeed, float maxSpeed, Color[] colors,
        TimerBonusParticles.Kind kind = TimerBonusParticles.Kind.Spark, float gravity = 0f, float drag = 0.05f,
        float size0 = 1.5f, float size1 = 3f, float life0 = 0.3f, float life1 = 0.6f,
        float angle = float.NaN, float spread = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = float.IsNaN(angle) ? Rnd(0f, Mathf.PI * 2f) : angle + Rnd(-spread, spread);
            float speed = Rnd(minSpeed, maxSpeed) * Px;
            _particles.Emit(new TimerBonusParticles.Particle
            {
                Kind = kind,
                Position = at,
                Velocity = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed,
                Gravity = gravity * Px,
                Drag = drag,
                Size = Rnd(size0, size1) * Px,
                MaxLife = Rnd(life0, life1),
                Color = colors[i % colors.Length],
            });
        }
    }

    /// <summary>충격파 링 (반지름 px).</summary>
    internal void Ring(Vector2 at, float radiusFrom, float radiusTo, Color color, float life = 0.42f)
    {
        _particles.Emit(new TimerBonusParticles.Particle
        {
            Kind = TimerBonusParticles.Kind.Ring,
            Position = at,
            RadiusFrom = radiusFrom * Px,
            RadiusTo = radiusTo * Px,
            Color = color,
            MaxLife = life,
        });
    }

    /// <summary>연출 레이어에 글자 (크기 px).</summary>
    internal TextMeshProUGUI CreateText(string name, float sizePx, Color color)
    {
        var text = NewText(name, _fx, _font, sizePx * Px, color);
        Hide(text);
        return text;
    }

    /// <summary>parent 아래에 Image.</summary>
    internal static Image CreateImage(string name, RectTransform parent, Color color, Sprite sprite = null)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>위치·크기·회전(도, 반시계)·투명도를 한 번에.</summary>
    internal static void Place(Graphic g, Vector2 pos, Vector2 scale, float alpha, float rotation = 0f)
    {
        var rt = g.rectTransform;
        rt.anchoredPosition = pos;
        rt.localScale = new Vector3(scale.x, scale.y, 1f);
        rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
        g.canvasRenderer.SetAlpha(alpha);
    }

    internal static void Hide(Graphic g) => g.canvasRenderer.SetAlpha(0f);

    internal static void SetText(TMP_Text t, string s)
    {
        if (t.text != s) t.text = s;
    }

    internal static RectTransform NewRect(string name, RectTransform parent, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }

    internal static TextMeshProUGUI NewText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.sizeDelta = new Vector2(size * 5f, size * 1.3f);
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── 내부 ──────────────────────────────────────────────────

    private void BuildView()
    {
        _sprites = new TimerBonusSprites();
        _shakeRoot = NewRect("ShakeRoot", _stage, Vector2.zero);
        Stretch(_shakeRoot);

        if (IsRuntime)
        {
            _loop = false;
            _timerBasePosition = _runtimeTimer.rectTransform.anchoredPosition;
            _timerBaseScale = _runtimeTimer.rectTransform.localScale;
            _timerBaseColor = _runtimeTimer.color;
            _liveTimerSize = _runtimeTimer.rectTransform.rect.size;
            _liveFontSize = _runtimeTimer.fontSize;
            // 해킹용 숫자 사본 (HUD 타이머 자리에 겹쳐 그림): 잔상 2장 → 본체. 평소엔 꺼 둔다.
            _timerRoot = NewRect("HackTimer", _shakeRoot, Vector2.zero);
            _ghostCyan = new TimerBonusDigits(_timerRoot, "GhostCyan", Font, _liveFontSize, RuntimeIntDigits);
            _ghostHot = new TimerBonusDigits(_timerRoot, "GhostHot", Font, _liveFontSize, RuntimeIntDigits);
            _main = new TimerBonusDigits(_timerRoot, "Main", Font, _liveFontSize, RuntimeIntDigits);
            foreach (var d in new[] { _ghostCyan, _ghostHot, _main }) d.MatchStyle(_runtimeTimer);
            _timerRoot.sizeDelta = _main.Size;
            _timerRoot.gameObject.SetActive(false);
            _fx = NewRect("Fx", _shakeRoot, Vector2.zero);
            Stretch(_fx);
            var liveParticles = NewRect("Particles", _shakeRoot, Vector2.zero);
            Stretch(liveParticles);
            _particles = new TimerBonusParticles(liveParticles, _sprites.SoftDot, _sprites.Ring);
            _flash = CreateImage("Flash", _overlay, Color.clear);
            Stretch(_flash.rectTransform);
            return;
        }

        // 보스 대역 (발밑 기준)
        _bossRoot = NewRect("Boss", _shakeRoot, new Vector2(_bossSize, _bossSize));
        _bossRoot.pivot = new Vector2(0.5f, 0f);
        _bossRoot.anchoredPosition = _bossPosition;
        _bossImage = _bossRoot.gameObject.AddComponent<Image>();
        _bossImage.sprite = _bossSprite;
        _bossImage.preserveAspect = true;
        _bossImage.raycastTarget = false;
        _bossGlow = CreateImage("DeathFlash", _bossRoot, Color.clear, _sprites.Glow);
        _bossGlow.rectTransform.sizeDelta = Vector2.one * (_bossSize * 1.1f);

        _hpBack = CreateImage("HpBack", _shakeRoot, new Color(0f, 0f, 0f, 0.45f));
        _hpBack.rectTransform.sizeDelta = new Vector2(_bossSize * 0.7f, 14f);
        _hpBack.rectTransform.anchoredPosition = _bossPosition + new Vector2(0f, _bossSize + 24f);
        _hpFillImage = CreateImage("HpFill", _hpBack.rectTransform, _settings.HotColor);
        _hpFill = _hpFillImage.rectTransform;
        Stretch(_hpFill);
        _hpFill.pivot = new Vector2(0f, 0.5f);

        // 타이머: 글로우 → 잔상 2장 → 본체 순으로 (뒤에서 앞)
        _timerRoot = NewRect("Timer", _shakeRoot, Vector2.zero);
        _timerRoot.anchoredPosition = _timerPosition;
        _timerGlow = CreateImage("Glow", _timerRoot, Color.clear, _sprites.Glow);
        float fs = _settings.TimerFontSize;
        _ghostCyan = new TimerBonusDigits(_timerRoot, "GhostCyan", _font, fs);
        _ghostHot = new TimerBonusDigits(_timerRoot, "GhostHot", _font, fs);
        _main = new TimerBonusDigits(_timerRoot, "Main", _font, fs);
        _timerGlow.rectTransform.sizeDelta = Vector2.Scale(_main.Size, new Vector2(1.4f, 1.6f));
        _timerRoot.sizeDelta = _main.Size;

        var label = NewText("TimeLabel", _shakeRoot, _font, 11f * Px, new Color(1f, 0.965f, 0.9f, 0.5f));
        label.text = "TIME";
        label.characterSpacing = 26f;
        label.rectTransform.anchoredPosition = _timerPosition + new Vector2(0f, _main.Size.y * 0.5f + 8f * Px);

        var droneSlots = NewRect("DroneSlots", _shakeRoot, Vector2.zero);
        Stretch(droneSlots);
        _drones = new TimerBonusDroneSquad(_droneSquad, droneSlots, BossCenter, _settings);

        _fx = NewRect("Fx", _shakeRoot, Vector2.zero);
        Stretch(_fx);
        var particleLayer = NewRect("Particles", _shakeRoot, Vector2.zero);
        Stretch(particleLayer);
        _particles = new TimerBonusParticles(particleLayer, _sprites.SoftDot, _sprites.Ring);

        _flash = CreateImage("Flash", _overlay, Color.clear);
        Stretch(_flash.rectTransform);
    }

    private void Restart()
    {
        _t = 0f;
        _fired.Clear();
        _particles?.Clear();
        _hold = _pulse = _zoom = _whiteFlash = _danger = 0f;
        _running = true;
    }

    private void BeginFrame()
    {
        _shake = Vector2.zero;
        _timerOffset = Vector2.zero;
        _timerScale = Vector2.one;
        _timerSkew = 0f;
        _gold = 0f;
        _timerFlash = 0f;
        _ghostOffset = 0f;
        _ghostAlpha = 0f;
        _timerAlpha = 1f;
        _hack = 0f;
    }

    // 처치 전: 박동이 점점 빨라지며 타이머가 빨갛게, 화면 가장자리가 붉게 → 처치 순간 히트스톱으로 터뜨린다.
    private void RenderTension(float a, float step)
    {
        if (a < 0f)
        {
            var beats = _settings.HeartbeatTimes;
            int n = beats.Length;
            for (int i = 0; i < n; i++)
            {
                if (_t < beats[i] || !Once(KeyBeatBase + i)) continue;
                float k = n > 1 ? 0.5f + 0.5f * i / (n - 1f) : 1f;
                _pulse += 0.05f + 0.05f * k;
                _danger = 1f;
            }
            _danger = Mathf.Max(_settings.DangerBase * (_t / _settings.PreKillSeconds), _danger * Decay(DangerHoldDecay, step));
            float q = Mathf.Clamp01((_t - PreShakeStart) / PreShakeRamp);
            if (q > 0f) AddShake(Mathf.Sin(_t * 1700f) * 1.6f * q, -Mathf.Cos(_t * 2300f) * 1.2f * q);
            return;
        }
        if (Once(KeyKill))
        {
            _hold = _settings.KillHitStop;
            _whiteFlash = _settings.KillFlash;
            _zoom = _settings.KillZoom;
        }
        _danger *= Decay(DangerFadeDecay, step);
    }

    private void RenderBoss(float a)
    {
        float scale = 1f, alpha = 1f, flash = 0f, hp = 0f;
        // 처치 전: 체력만 줄어들고 보스 자체 피격 반응은 없다 (이펙트는 드론 레이저 쪽에만).
        if (a < 0f) hp = 1f - _t / _settings.PreKillSeconds;
        else
        {
            if (a < BossPopSeconds) { scale = 1f + 0.22f * OutCubic(a / BossPopSeconds); flash = 1f; }
            else if (a < BossPopSeconds + BossShrinkSeconds)
            {
                float q = (a - BossPopSeconds) / BossShrinkSeconds;
                scale = 1.22f * (1f - InCubic(q));
                flash = 1f - q;
            }
            else { scale = 0f; alpha = 0f; }

            float respawn = _t - (LoopSeconds - BossRespawnSeconds);
            if (respawn > 0f) { scale = OutBack(Mathf.Clamp01(respawn / BossRespawnSeconds)); alpha = 1f; flash = 0f; hp = 1f; }

            if (Once(KeyBossBurst))
            {
                Burst(BossCenter, 22, 200f, 560f, new[] { _settings.HotColor, Pink, Color.white }, gravity: 500f, drag: 0.04f);
                Ring(BossCenter, 20f, 95f, Pink, 0.38f);
            }
        }
        _bossRoot.anchoredPosition = _bossPosition;
        _bossRoot.localScale = Vector3.one * Mathf.Max(0f, scale);
        _bossImage.canvasRenderer.SetAlpha(alpha);
        _bossGlow.color = new Color(1f, 1f, 1f, flash * 0.85f * alpha);
        _hpBack.canvasRenderer.SetAlpha(alpha);
        _hpFillImage.canvasRenderer.SetAlpha(alpha);
        _hpFill.localScale = new Vector3(Mathf.Clamp01(hp) * HpBarFraction, 1f, 1f);
    }

    private void ApplyFrame()
    {
        if (IsRuntime && _hackActive)
        {
            // HUD 타이머는 숨기고 그 자리에 사본을 그린다. 흔들림은 타이머에만 (화면은 그대로).
            _runtimeTimer.canvasRenderer.SetAlpha(0f);
            float hp = (1f + _pulse) * _liveScaleRatio;
            _timerRoot.anchoredPosition = TimerPosition + _timerOffset + _shake;
            _timerRoot.localScale = new Vector3(_timerScale.x * hp, _timerScale.y * hp, 1f);
            var hc = Color.Lerp(_timerBaseColor, _settings.CyanColor, Mathf.Clamp01(_hack));
            hc = Color.Lerp(hc, Color.white, Mathf.Clamp01(_timerFlash) * FlashWhiten);
            hc.a *= Mathf.Clamp01(_timerAlpha);
            _main.SetColor(hc);
            _ghostCyan.SetColor(WithAlpha(_settings.CyanColor, _ghostAlpha));
            _ghostHot.SetColor(WithAlpha(_settings.HotColor, _ghostAlpha));
            _ghostCyan.Root.anchoredPosition = new Vector2(-_ghostOffset, 0f);
            _ghostHot.Root.anchoredPosition = new Vector2(_ghostOffset, 0f);
            _main.SetSkew(_timerSkew);
            _ghostCyan.SetSkew(_timerSkew);
            _ghostHot.SetSkew(_timerSkew);
            _flash.color = WithAlpha(Color.white, Mathf.Clamp01(_whiteFlash));
            return;
        }
        if (IsRuntime)
        {
            _runtimeTimer.rectTransform.anchoredPosition = _timerBasePosition + _timerOffset;
            _runtimeTimer.rectTransform.localScale = Vector3.Scale(_timerBaseScale, new Vector3(_timerScale.x, _timerScale.y, 1f));
            _runtimeTimer.color = Color.Lerp(Color.Lerp(_timerBaseColor, _settings.GoldColor, Mathf.Clamp01(_gold)), Color.white, Mathf.Clamp01(_timerFlash));
            _flash.color = WithAlpha(Color.white, Mathf.Clamp01(_whiteFlash));
            return;
        }
        _shakeRoot.anchoredPosition = _shake;
        _shakeRoot.localScale = Vector3.one * (1f + _zoom);

        float p = 1f + _pulse;
        _timerRoot.anchoredPosition = _timerPosition + _timerOffset;
        _timerRoot.localScale = new Vector3(_timerScale.x * p, _timerScale.y * p, 1f);

        var c = Color.Lerp(_settings.TimerColor, _settings.DangerColor, Mathf.Clamp01(_danger));
        c = Color.Lerp(c, _settings.CyanColor, Mathf.Clamp01(_hack));
        c = Color.Lerp(c, _settings.GoldColor, Mathf.Clamp01(_gold));
        c = Color.Lerp(c, Color.white, Mathf.Clamp01(_timerFlash) * FlashWhiten);
        c.a *= Mathf.Clamp01(_timerAlpha);
        _main.SetColor(c);
        _ghostCyan.SetColor(WithAlpha(_settings.CyanColor, _ghostAlpha));
        _ghostHot.SetColor(WithAlpha(_settings.HotColor, _ghostAlpha));
        _ghostCyan.Root.anchoredPosition = new Vector2(-_ghostOffset, 0f);
        _ghostHot.Root.anchoredPosition = new Vector2(_ghostOffset, 0f);
        _timerGlow.color = WithAlpha(_settings.GoldColor, Mathf.Clamp01(_gold) * GlowGoldAlpha + Mathf.Clamp01(_timerFlash) * GlowFlashAlpha);
        // 색·글자를 다 바꾼 뒤 마지막에 기울인다 (정점을 직접 미는 방식).
        _main.SetSkew(_timerSkew);
        _ghostCyan.SetSkew(_timerSkew);
        _ghostHot.SetSkew(_timerSkew);

        _flash.color = WithAlpha(Color.white, Mathf.Clamp01(_whiteFlash));
    }

    // 인게임: 그릴 시안 교체 (A 합체 ↔ J 해킹). 해킹 렌더러는 마지막에 켠 쪽만 끌 수 있으므로 끈 뒤 켠다.
    private void SwitchConcept(int index)
    {
        if (_concept == index) return;
        _concepts[_concept].SetVisible(false);
        _concept = Mathf.Clamp(index, 0, _concepts.Length - 1);
        _concepts[_concept].SetVisible(true);
    }

    // 인게임 해킹 정리: 사본을 끄고 HUD 타이머를 실제 값으로 되돌린다.
    private void EndHack()
    {
        if (!_hackActive) return;
        _hackActive = false;
        _liveTriggers.Clear();
        _liveTriggerBonus.Clear();
        _liveTriggerOrigin.Clear();
        _liveTriggerArray = System.Array.Empty<float>();
        _liveAdded = 0f;
        _hold = 0f;
        if (_timerRoot != null) _timerRoot.gameObject.SetActive(false);
        if (_runtimeTimer != null) _runtimeTimer.canvasRenderer.SetAlpha(1f);
        SwitchConcept(RuntimeFusionConcept);
    }

    private void UpdateBonusText()
    {
        float b = Bonus;
        _bonusText = "+" + b.ToString("0.00", CultureInfo.InvariantCulture);
        _bonusShortText = "+" + b.ToString("0", CultureInfo.InvariantCulture);
    }

    private void Refresh()
    {
        if (_conceptButtons != null)
            for (int i = 0; i < _conceptButtons.Length; i++)
                if (_conceptButtons[i] != null) _conceptButtons[i].color = i == _concept ? ButtonSelected : ButtonIdle;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_status == null || !_ready) return;
        float a = _t - _settings.PreKillSeconds;
        string phase = a < 0f ? "긴장" : a < _settings.AnimSeconds ? "연출" : "카운트다운";
        _status.text = $"{_concepts[_concept].Name}  /  보너스 {_bonusText}  /  {Speed:0.##}배속  /  반복 {(_loop ? "켬" : "끔")}\n"
            + $"처치 기준 {a:+0.00;-0.00}s ({phase})" + TriggerSetStatus();
    }

    // 중첩 시안일 때만 발동 묶음 이름과 시점을 붙인다.
    private string TriggerSetStatus()
    {
        var sets = _settings.Hack.StackTriggerSets;
        if (!(_concepts[_concept] is TimerBonusHackStack) || sets == null || sets.Length == 0) return "";
        var set = sets[Mathf.Clamp(_triggerSetIndex, 0, sets.Length - 1)];
        var times = set.Times ?? System.Array.Empty<float>();
        return $"   /   발동: {set.Name} ({string.Join(" / ", System.Array.ConvertAll(times, t => t.ToString("0.##", CultureInfo.InvariantCulture)))}s)";
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static float Decay(float perFrame, float seconds) => Mathf.Pow(perFrame, seconds * FrameRef);
}
