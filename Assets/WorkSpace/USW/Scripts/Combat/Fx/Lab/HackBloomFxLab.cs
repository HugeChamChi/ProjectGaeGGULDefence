using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FxLab_HackBloom 드라이버 — 델탕은 그리드에 가만히 있고 델탕 드론이 쉬지 않고 해킹 탄을 쏴 악어 보스에 빛점 표식(스택)을 쌓는다.
/// 감망은 자기 주기마다 순간이동 신호를 보내 스택을 일정량(기본 20)만 소모한다: 50/50 → 30/50, 그 사이 드론이 다시 채운다.
/// 소모분만큼의 표식이 하나씩 연쇄로 터지고(표식마다 숫자), 마지막에 큰 숫자 하나로 마무리한다.
/// 확정(10-07): 기폭 A 연쇄 · 표식 빛점 · 최대 50스택 · 스택 비율 색상 단계 · 디버프 줄은 HP바 바로 아래 · 스택 UI는 해골 초상화 링.
/// 남은 비교: 링 시안 4종 · 감망 동작.
/// 보스 몸 표식은 최대 12개로 스택 비율만큼 보이고, 정확한 수는 숫자·게이지로 표현한다.
/// 실험실 전용: 속도는 x1 고정, Time.timeScale은 히트스톱에만 쓰고 비활성화 시 1로 되돌린다. 전투 데이터는 건드리지 않는다.
/// </summary>
public sealed class HackBloomFxLab : MonoBehaviour, IFxLabPlayable
{
    private const int NumberSeed = 23;
    private const float DepletionPreviewPause = 2f;
    private static readonly string[] GaugeLabels = { "없음", "분할 링", "스캔 링", "오픈 링" };

    [Header("참조")]
    [SerializeField] private HackStackMarks _marks;
    [SerializeField] private GammanTeleportFxLab _gamman;
    [SerializeField] private GammanTeleportTake[] _takes;
    [Tooltip("델탕 드론 (그리드 위 델탕 옆에서 떠 있다가 쏜다)")]
    [SerializeField] private SpriteRenderer _deltanDrone;
    [SerializeField] private Sprite _droneIdle;
    [SerializeField] private Sprite _droneFire;
    [Tooltip("타격 때 살짝 움찔하는 보스 그림 (Spine 오브젝트)")]
    [SerializeField] private Transform _bossVisual;
    [SerializeField] private Transform _boss;
    [Tooltip("인게임과 같은 자리에 둔 보스 HP바 (HUD 배치 기준)")]
    [SerializeField] private RectTransform _hpBar;
    [Tooltip("IngameScene에서 복제한 보스 HP바 — 시작 때 BeginBoss로 인게임처럼 채운다")]
    [SerializeField] private UI_BossHpBar _hpBarView;
    [Tooltip("실험실 HP바 표시값 (현재 HP, 최대 HP, 줄 수) — 예시값")]
    [SerializeField] private Vector3 _labBossHp = new Vector3(93100f, 100000f, 182f);
    [Header("디버프 줄 대역 (design/hp,디버프예씨.png)")]
    [SerializeField] private Sprite _debuffTile;
    [Tooltip("예시 디버프 아이콘 (방어 약화 / 화상 / 받피증)")]
    [SerializeField] private Sprite[] _debuffIcons;
    [SerializeField] private string[] _debuffCounts = { "23", "", "" };
    [SerializeField] private Camera _camera;
    [SerializeField] private RectTransform _uiRoot;
    [Tooltip("HUD보다 뒤에 그리는 레이어 (링 발광이 초상화를 덮지 않게)")]
    [SerializeField] private RectTransform _uiBackRoot;
    [SerializeField] private Image _flash;
    [SerializeField] private CookieFloaterLabSettings _floaterSettings;

    [Header("구성")]
    [Tooltip("독립 링 시안 (0 없음, 1 분할 / 2 스캔 / 3 오픈). HP바 우측 하단 배치.")]
    [SerializeField, Range(0, 3)] private int _gaugeDesign = 2;
    [Tooltip("최대 스택")]
    [SerializeField, Min(1)] private int _maxStacks = 50;
    [Tooltip("다시 보기 때 처음 쌓여 있는 스택")]
    [SerializeField, Min(0)] private int _startStacks = 0;
    [Tooltip("감망 스킬 한 번에 소모하는 스택")]
    [SerializeField, Min(1)] private int _consumePerCast = 20;
    [Tooltip("감망 신호 주기 (초) — 신호가 닿는 순간 사이 간격")]
    [SerializeField] private float _gammanCooldown = 3.6f;
    [SerializeField] private int _take;
    [Tooltip("드론 기준 해킹 탄이 나가는 위치")]
    [SerializeField] private Vector3 _droneMuzzle = new Vector3(0f, .2f, -.1f);

    [Header("해킹 (초)")]
    [SerializeField] private float _lead = .8f;
    [SerializeField] private float _shotInterval = .13f;
    [SerializeField] private float _boltFlight = .09f;
    [Tooltip("다시 보기 후 처음 최대치가 된 뒤 감망 신호가 닿기까지")]
    [SerializeField] private float _contactAfterFull = .3f;
    [Tooltip("감망 동작 앞쪽 대기(READY)를 건너뛰는 시간 — 신호를 빨리 보내기 위함")]
    [SerializeField] private float _gammanSkip = .55f;
    [Tooltip("드론 위아래 떠 있는 폭·속도")]
    [SerializeField] private Vector2 _droneHover = new Vector2(.06f, 3f);

    [Header("기폭 (초)")]
    [SerializeField] private float _igniteTime = .1f;
    [SerializeField] private float _chainGap = .045f;
    [Tooltip("마지막 표식이 터진 뒤 마무리 숫자까지")]
    [SerializeField] private float _finishDelay = .05f;
    [SerializeField] private float _contactHitStop = .06f;
    [Tooltip("히트스톱 동안의 시간 배율")]
    [SerializeField, Range(0f, 1f)] private float _hitStopScale = .03f;
    [Tooltip("캡처·검토용 전체 시간 배율 (화면 버튼 없음, 기본 1)")]
    [SerializeField, Range(.05f, 1f)] private float _reviewTimeScale = 1f;

    [Header("화면·보스 반응")]
    [SerializeField] private float _popShake = .05f;
    [SerializeField] private float _finishShake = .11f;
    [Tooltip("흔들림이 사라지는 시간")]
    [SerializeField] private float _shakeDecay = .3f;
    [SerializeField, Range(0f, 1f)] private float _finishFlash = .22f;
    [SerializeField] private float _flashFade = .2f;
    [Tooltip("표식 명중·파열 때 보스가 움찔하는 크기 (배율)")]
    [SerializeField] private float _hitPunch = .025f;
    [SerializeField] private float _finishPunch = .05f;

    [Header("피해 숫자 (예시값 — 실제 밸런스 아님)")]
    [SerializeField] private int _chipDamage = 1840;
    [SerializeField, Range(0f, .5f)] private float _damageVariance = .15f;
    [SerializeField] private int _finishDamageBase = 12000;
    [SerializeField] private int _finishDamagePerStack = 2400;

    private readonly List<(float At, Action Run)> _events = new List<(float, Action)>();
    private readonly List<Action> _due = new List<Action>();
    private readonly Vector3[] _corners = new Vector3[4];
    private HackStackGauge[] _gauges;
    private HackDebuffRowMock _debuffRow;
    private CookieFloaterVariantView _numbers;
    private System.Random _rng;
    private Vector3 _cameraHome, _droneHome, _bossScale;
    private float _clock, _hitStopUntil, _shake, _flashAlpha, _punch, _fireUntil;
    private bool _waitingContact, _wasFiring;
    private int _loop, _stackCount, _pendingConsume, _consumed;
    private string _phase = "";

    /// <inheritdoc/>
    public bool UseUnscaledTime { get; set; }
    /// <summary>현재 단계 이름 (캡처·확인용).</summary>
    public string Phase => _phase;
    /// <summary>현재 스택 (소모 연출 중인 양 제외).</summary>
    public int Stacks => _stackCount;

    private void Awake()
    {
        _cameraHome = _camera.transform.position;
        _droneHome = _deltanDrone.transform.position;
        _bossScale = _bossVisual != null ? _bossVisual.localScale : Vector3.one;
        _numbers = new CookieFloaterVariantView(_floaterSettings, _uiRoot, "HackBloomNumbers", NumberSeed) { ShowGuides = false };
        _numbers.SetVariant(0);
        _numbers.SetBoss(_boss);
        var font = _floaterSettings.Font; var material = _floaterSettings.FontMaterial;
        _gauges = new HackStackGauge[]
        {
            new HackGaugeOrbit(_uiRoot, font, material, HackGaugeOrbit.Look.Segmented),
            new HackGaugeOrbit(_uiRoot, font, material, HackGaugeOrbit.Look.Smooth),
            new HackGaugeOrbit(_uiRoot, font, material, HackGaugeOrbit.Look.OpenArc),
        };
        SelectGauge(_gaugeDesign);
        if (_debuffIcons != null && _debuffIcons.Length > 0)
            _debuffRow = new HackDebuffRowMock(_uiRoot, font, material, _debuffTile, _debuffIcons, _debuffCounts);
        _marks.BoltHit += OnBoltHit;
    }

    private void Start()
    {
        // 프리팹 기본 상태(채움 91%)로 두면 HP바 오른쪽 끝이 튀어나와 보이므로 인게임처럼 보스 HP를 넣는다.
        _hpBarView?.BeginBoss((decimal)_labBossHp.x, (decimal)_labBossHp.y, (int)_labBossHp.z);
        Play();
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        if (_camera != null) _camera.transform.position = _cameraHome;
    }

    private void OnDestroy()
    {
        if (_marks != null) _marks.BoltHit -= OnBoltHit;
    }

    /// <inheritdoc/>
    public void Play() => BeginLoop();

    /// <summary>스택 UI 시안을 바꾼다 (0 없음).</summary>
    public void SelectGauge(int design)
    {
        _gaugeDesign = Mathf.Clamp(design, 0, _gauges.Length);
        for (int i = 0; i < _gauges.Length; i++) _gauges[i].Active = i == _gaugeDesign - 1;
    }

    /// <summary>실험실 전용: 전량 소진과 HUD 퇴장을 보여준 뒤 0스택부터 다시 재생한다.</summary>
    public void PreviewDepletion()
    {
        _events.Clear(); _due.Clear();
        _waitingContact = false; _wasFiring = false;
        _gamman.Stop(); _marks.Clear();
        int remaining = _stackCount + _pendingConsume;
        _stackCount = _pendingConsume = _consumed = 0;
        _phase = "소진 연출 → 재시작";
        foreach (var gauge in _gauges) { gauge.Consume(remaining); gauge.Set(0); }
        Schedule(_clock + DepletionPreviewPause, BeginLoop);
    }

    private void BeginLoop()
    {
        _events.Clear();
        _clock = 0f; _hitStopUntil = 0f; _shake = 0f; _flashAlpha = 0f; _punch = 0f; _fireUntil = 0f;
        _waitingContact = false; _wasFiring = false; _pendingConsume = 0; _consumed = 0;
        _rng = new System.Random(1234 + _loop);
        _marks.Clear(); _numbers.Clear(); _gamman.Stop();
        _phase = "해킹";
        _stackCount = Mathf.Clamp(_startStacks, 0, _maxStacks);
        foreach (var gauge in _gauges) gauge.Reset(_stackCount, _maxStacks);
        int preMarks = MarksFor(_stackCount);
        Schedule(.02f, () => { for (int i = 0; i < preMarks; i++) _marks.AttachInstant(i); });
        Schedule(_lead, DroneShot);
        // 첫 신호는 처음 최대치가 된 직후에 닿게 한다.
        float firstContact = _lead + (_maxStacks - _stackCount) * _shotInterval + _boltFlight + _contactAfterFull;
        ScheduleGamman(firstContact);
    }

    private void Update()
    {
        Time.timeScale = _reviewTimeScale * (Time.unscaledTime < _hitStopUntil ? _hitStopScale : 1f);
        _clock += Time.deltaTime;
        if (_waitingContact)
        {
            bool firing = _gamman.IsFiring;
            if (firing && !_wasFiring) { _waitingContact = false; Detonate(); }
            _wasFiring = firing;
        }
        RunDue();
    }

    private void LateUpdate()
    {
        float now = Time.time;
        _numbers.Tick(now);
        if (_hpBar != null)
        {
            Rect bar = HpBarRect();
            if (_gaugeDesign > 0)
            {
                var gauge = _gauges[_gaugeDesign - 1];
                gauge.UseHud = true;
                gauge.HudBar = bar;
                gauge.Tick(now, Vector2.zero);
            }
            _debuffRow?.Tick(bar);
        }
        _shake *= Mathf.Exp(-Time.deltaTime * 3f / Mathf.Max(.01f, _shakeDecay));
        Vector2 offset = _shake > .001f ? UnityEngine.Random.insideUnitCircle * _shake : Vector2.zero;
        _camera.transform.position = _cameraHome + (Vector3)offset;
        _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, Time.deltaTime * _finishFlash / Mathf.Max(.01f, _flashFade));
        var color = _flash.color; color.a = _flashAlpha; _flash.color = color;

        // 델탕 드론: 제자리에서 떠 있고, 쏠 때만 발사 프레임 + 뒤로 살짝 반동.
        bool firing = _clock < _fireUntil;
        _deltanDrone.sprite = firing ? _droneFire : _droneIdle;
        float recoil = firing ? -.05f : 0f;
        _deltanDrone.transform.position = _droneHome + new Vector3(0f, Mathf.Sin(now * _droneHover.y) * _droneHover.x + recoil, 0f);

        // 보스 움찔: 가로로 살짝 눌렸다 돌아온다.
        _punch *= Mathf.Exp(-Time.deltaTime * 18f);
        if (_bossVisual != null)
            _bossVisual.localScale = Vector3.Scale(_bossScale, new Vector3(1f + _punch, 1f - _punch * .6f, 1f));
    }

    // ── 해킹: 드론은 쉬지 않고 쏜다. 최대치면 맞아도 더 쌓이지 않는다(초과 손실) ──────────

    private void DroneShot()
    {
        _fireUntil = _clock + .06f;
        // 보이는 표식 수는 스택 비율을 따른다: 모자라면 빈 칸에 새 표식, 충분하면 기존 표식을 맞혀 번쩍인다.
        int desired = MarksFor(Mathf.Min(_stackCount + _marks.ReservedCount + 1, _maxStacks));
        bool newMark = _marks.AttachedCount + _marks.ReservedCount < desired;
        int slot = newMark ? _marks.ReserveFreeSlot() : _marks.PickAttached(_rng.Next());
        if (slot < 0) { slot = _marks.ReserveFreeSlot(); newMark = slot >= 0; }
        if (slot >= 0) _marks.LaunchBolt(_deltanDrone.transform.position + _droneMuzzle, slot, _boltFlight, newMark);
        Schedule(_clock + _shotInterval, DroneShot);
    }

    private void OnBoltHit(int chip)
    {
        if (_stackCount < _maxStacks) _stackCount++;
        SetGauges(_stackCount + _pendingConsume);
        Punch(_hitPunch * .6f);
        Shake(_popShake * .2f);
    }

    // ── 감망: 신호가 닿으면 스택을 정해진 만큼만 소모, 그만큼의 표식이 연쇄로 터진다 ──────────

    private void ScheduleGamman(float contactAt)
    {
        int take = Mathf.Clamp(_take, 0, _takes.Length - 1);
        float startAt = Mathf.Max(_clock, contactAt - (ContactOffset(_takes[take]) - _gammanSkip));
        Schedule(startAt, () =>
        {
            _phase = "감망 신호";
            _gamman.BeginTake(take, -_gammanSkip);
            _waitingContact = true;
            _wasFiring = false;
        });
    }

    private void Detonate()
    {
        float contactAt = _clock;
        _consumed = Mathf.Min(_consumePerCast, _stackCount);
        _phase = "기폭 -" + _consumed;
        HitStop(_contactHitStop);
        Shake(_popShake * 1.5f);
        Punch(_hitPunch * 1.5f);
        if (_consumed > 0)
        {
            int before = _stackCount;
            _stackCount -= _consumed;
            _pendingConsume = _consumed;
            foreach (var gauge in _gauges) gauge.Consume(_consumed);
            // 소모 후 비율에 맞게 남길 표식 수를 정하고, 넘치는 표식만 연쇄로 터뜨린다.
            int pops = Mathf.Max(1, _marks.AttachedCount - MarksFor(_stackCount));
            float t = _clock + _igniteTime;
            int order = 0;
            for (int k = 0; k < HackStackMarks.Capacity && order < pops; k++)
            {
                if (!_marks.IsAttached(k)) continue;
                int chip = k, index = ++order;
                _marks.Ignite(chip);
                Schedule(t + (index - 1) * _chainGap, () => PopChip(chip, index, pops));
            }
            Schedule(t + pops * _chainGap + _finishDelay, Finisher);
            _phase = "기폭 " + before + "→" + _stackCount;
        }
        ScheduleGamman(contactAt + _gammanCooldown);
    }

    private void PopChip(int chip, int order, int pops)
    {
        Vector3 pos = _marks.ChipPosition(chip);
        _marks.Pop(chip);
        _pendingConsume = Mathf.RoundToInt(_consumed * (1f - order / (float)pops));
        SetGauges(_stackCount + _pendingConsume);
        Shake(_popShake);
        Punch(_hitPunch);
        AddNumber(RollChip(), BossDamageKind.Normal, pos);
    }

    private void Finisher()
    {
        _phase = "마무리 -" + _consumed;
        _pendingConsume = 0;
        SetGauges(_stackCount);
        HitStop(_contactHitStop);
        Shake(_finishShake);
        Punch(_finishPunch);
        _flashAlpha = _finishFlash;
        AddNumber(_finishDamageBase + _finishDamagePerStack * _consumed, BossDamageKind.Critical, _marks.Center);
    }

    private int MarksFor(int stacks)
        => stacks <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(stacks / (float)_maxStacks * HackStackMarks.Capacity), 1, HackStackMarks.Capacity);

    private decimal RollChip()
        => (decimal)Mathf.Round(_chipDamage * (1f + (float)(_rng.NextDouble() * 2 - 1) * _damageVariance));

    private void SetGauges(int value)
    {
        foreach (var gauge in _gauges) gauge.Set(value);
    }

    private void AddNumber(decimal amount, BossDamageKind kind, Vector3 world)
        => _numbers.AddHit(amount, kind, Time.time, world);

    // 감망 동작 시작부터 빔이 보스에 닿기까지 (발사 박자 앞 박자들의 합 + 빔이 뻗는 시간)
    private static float ContactOffset(GammanTeleportTake take)
    {
        float sum = 0f;
        foreach (var beat in take.Beats)
        {
            if (beat.Fire) return sum + .025f;
            sum += beat.Duration;
        }
        return sum;
    }

    // ── 시간·화면 도우미 ─────────────────────────────────────

    private void Schedule(float at, Action run) => _events.Add((at, run));

    private void RunDue()
    {
        _due.Clear();
        for (int i = _events.Count - 1; i >= 0; i--)
        {
            if (_events[i].At > _clock) continue;
            _due.Add(_events[i].Run);
            _events.RemoveAt(i);
        }
        // 뒤에서부터 모았으니 거꾸로 실행해 예약 순서를 지킨다.
        for (int i = _due.Count - 1; i >= 0; i--) _due[i]();
    }

    private void HitStop(float seconds) => _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + seconds / Mathf.Max(.05f, _reviewTimeScale));

    private void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

    private void Punch(float amount) => _punch = Mathf.Max(_punch, amount);

    private Vector2 WorldToUi(Vector3 world)
        => DamageStyleLabUtil.ScreenToLocal(_uiRoot, RectTransformUtility.WorldToScreenPoint(_camera, world));

    // HP바 프리팹 영역을 숫자 캔버스 좌표로 (오버레이 캔버스라 월드 좌표 = 화면 픽셀)
    private Rect HpBarRect()
    {
        _hpBar.GetWorldCorners(_corners);
        Vector2 min = DamageStyleLabUtil.ScreenToLocal(_uiRoot, _corners[0]);
        Vector2 max = DamageStyleLabUtil.ScreenToLocal(_uiRoot, _corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void OnGUI()
    {
        float s = Mathf.Max(.6f, Screen.width / 540f);
        var old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s, h = Screen.height / s;
        float top = h - 148f;
        GUI.Box(new Rect(8, top, w - 16, 140), "DELTAN HACK -> GAMMAN");
        GUI.Label(new Rect(18, top + 22, w - 36, 22), "스택 " + _stackCount + "/" + _maxStacks + " · 감망 1회 -" + _consumePerCast + " · " + _phase);
        float gw = (w - 36 - 4 * (GaugeLabels.Length - 1)) / GaugeLabels.Length;
        for (int i = 0; i < GaugeLabels.Length; i++)
            if (Toggle(new Rect(18 + i * (gw + 4), top + 46, gw, 28), GaugeLabels[i], _gaugeDesign == i)) SelectGauge(i);
        float tw = (w - 36 - 4 * _takes.Length) / (_takes.Length + 1);
        for (int i = 0; i < _takes.Length; i++)
            if (Toggle(new Rect(18 + i * (tw + 4), top + 80, tw, 28), "감망 " + (char)('A' + i), _take == i)) { _take = i; BeginLoop(); }
        if (GUI.Button(new Rect(18 + _takes.Length * (tw + 4), top + 80, tw, 28), "다시 보기")) BeginLoop();
        if (GUI.Button(new Rect(18, top + 112, w - 36, 22), "소진 연출 보기")) PreviewDepletion();
        GUI.matrix = old;
    }

    private static bool Toggle(Rect rect, string label, bool on)
    {
        var old = GUI.backgroundColor;
        if (on) GUI.backgroundColor = new Color(.45f, .85f, 1f);
        bool clicked = GUI.Button(rect, label);
        GUI.backgroundColor = old;
        return clicked;
    }
}
