using System;
using System.Collections.Generic;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>9122 해킹 자폭의 적중 이후 모습과 E+B 결합안을 비교한다. 실제 게임 장부에는 연결하지 않는다.</summary>
public sealed class BombHackLab : MonoBehaviour
{
    private const int HybridVariant = 5;
    private static readonly string[] Titles = { "A 데이터 흡수", "B 감염 낙인", "C 회로 연결", "D 압축 업로드", "E 국소 침투", "F E충돌+빛점" };
    private static readonly string[] Descriptions = {
        "파열한 자리의 데이터 한 덩어리가\n곡선을 따라 해킹 HUD로 흡수됩니다.",
        "맞은 보스에 해킹 낙인을 남기고\n짧은 스캔선으로 HUD와 연결합니다.",
        "폭발 지점에서 HUD까지 회로를 펼치고\n작은 신호 하나를 빠르게 전송합니다.",
        "흩어진 파편이 작은 코어로 압축된 뒤\nHUD로 한 번에 업로드됩니다.",
        "전장을 가로지르는 선 없이\n적중점 글리치 + HUD 반응으로 전달합니다.",
        "네모 낙인은 빼고 기존 빛점 유지.\n수신 연출은 별도 Receipt 씬에서 비교."
    };
    [SerializeField] private BombHackLabSettings _settings;
    [SerializeField] private LevelUpData _card;
    [SerializeField] private DroneHackingData _hackingData;
    [SerializeField] private HackStackMarks _marks;
    [SerializeField] private Transform _bossVisual;
    [SerializeField] private RectTransform _hpBar, _uiRoot;
    [SerializeField] private UI_BossHpBar _hpBarView;
    [SerializeField] private Camera _camera;
    [SerializeField] private BombHackCanvasFx _fx;
    [SerializeField] private TMP_Text _title, _description, _status;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Button[] _buttons;
    [SerializeField] private GameObject _ownerTemplate, _droneTemplate;
    [SerializeField] private Sprite _bombSprite;
    [SerializeField] private Vector3 _bombScale;
    [SerializeField] private Material _beamMaterial, _glowMaterial;
    [SerializeField] private int _variant;
    [SerializeField] private bool _loop = true;
    [SerializeField] private bool _externalReceiptFeedback;
    private bool _dense, _ambient, _paused, _ready, _pressure;
    private float _clock, _nextLaunch, _hudPulseAt = -100f, _nextStatus;
    private int _stacks, _hits, _granted, _receipts, _serial, _stacksPerBomb, _routeHighWater, _hudTargetStacks;
    private HackGaugeOrbit _gauge;
    private RectTransform _hudRoot;
    private Transform _crowdRoot;
    private StatBuffSpriteAtlas _atlas;
    private WorldGlitchBlocks _glitch;
    private Sprite _pip, _dot, _ring, _sparkle, _streak;
    private Shot[] _shots;
    private DroneBeamVisual[] _ambientBeams;
    private readonly List<Transform> _owners = new();
    private readonly List<Transform> _drones = new();
    private readonly Vector3[] _corners = new Vector3[4];
    private Vector2 _hudCenter;
    private float _nextPressure;
    private sealed class Shot
    {
        public SpriteRenderer Bomb;
        public Vector3 Start, Impact;
        public float Born, HitAt;
        public int Serial;
        public bool Active, Hit, Received, Route;
    }

    /// <summary>현재 시안 번호.</summary>
    public int Variant => _variant;
    /// <summary>이 시안 반복의 미리보기 적중 수.</summary>
    public int HitCount => _hits;
    /// <summary>적중에서 발생한 미리보기 스택 증가 합.</summary>
    public int GrantedStacks => _granted;
    /// <summary>수신 연출 완료 수. 이 시점에는 스택을 추가하지 않는다.</summary>
    public int ReceiptCount => _receipts;
    /// <summary>표시 스택. 실제 게임 장부가 아니다.</summary>
    public int PreviewStacks => _stacks;
    /// <summary>수신 표시의 목표값. 실제로 수용한 미리보기 잔고와 구분한다.</summary>
    public int HudTargetStacks => _hudTargetStacks;
    /// <summary>원본 해킹 설정에서 읽은 미리보기 용량.</summary>
    public int PreviewCapacity => _hackingData.Capacity;
    /// <summary>최대 동시 HUD 이동 표현 수.</summary>
    public int RouteHighWater => _routeHighWater;
    /// <summary>순수 미리보기 시계.</summary>
    public float PreviewClock => _clock;
    /// <summary>미리보기 장부에서 수용한 스택과 적중 위치. 실제 게임 이벤트가 아니다.</summary>
    public event Action<Vector3, int> StackAccepted;
    /// <summary>시안 전환·군집 변경·다시보기에서 대기한 수신 표현도 함께 초기화한다.</summary>
    public event Action PreviewReset;
    /// <summary>같은 캔버스 좌표계의 실제 HACK 중심.</summary>
    public Vector2 HudCenter => _hudCenter;
    /// <summary>현재 군집 비교 상태.</summary>
    public bool IsDense => _dense;
    /// <summary>현재 미리보기 일시정지 상태.</summary>
    public bool IsPaused => _paused;
    /// <summary>보통 단발 반복 상태.</summary>
    public bool LoopEnabled => _loop;
    /// <summary>연속 난사 비교 상태.</summary>
    public bool PressureEnabled => _pressure;
    /// <summary>배경 사격 상태.</summary>
    public bool AmbientEnabled => _ambient;
    /// <summary>별도 수신 씬에서 HUD 표시만 확인한다. 스택 장부/피해는 변경하지 않는다.</summary>
    public void ShowReceivedStacks(int value)
    {
        _hudTargetStacks = Mathf.Clamp(value, 0, _stacks);
        _gauge.Set(_hudTargetStacks);
    }
    /// <summary>검증용 현재 표시 정보.</summary>
    public string Snapshot => $"variant={_variant}; hits={_hits}; granted={_granted}; receipts={_receipts}; stacks={_stacks}; routesMax={_routeHighWater}; owners={_owners.Count}; drones={_drones.Count}; dense={_dense}; ambient={_ambient}";

    private void Start()
    {
        if (_card == null || _card.chooseId != 9122) { enabled = false; return; }
        foreach (var effect in _card.Composition.Effects)
            if (effect is BombHackingDefinition bomb) _stacksPerBomb = bomb.StacksPerBomb;
        _atlas = new StatBuffSpriteAtlas();
        _pip = _atlas.Resolve(StatBuffShapes.Pip); _dot = _atlas.Resolve(StatBuffShapes.Dot);
        _ring = _atlas.Resolve(StatBuffShapes.Ring); _streak = _atlas.Resolve(StatBuffShapes.Streak);
        _sparkle = _atlas.Resolve(StatBuffShapes.Get(StatBuffShape.Sparkle).Fill);
        _fx.Initialize(_pip.texture);
        _gauge = new HackGaugeOrbit(_uiRoot, _font, null, HackGaugeOrbit.Look.Segmented);
        _hudRoot = _uiRoot.Find("HackOrbit_Segmented") as RectTransform;
        var glitchRoot = new GameObject("LocalGlitchPool", typeof(SortingGroup));
        glitchRoot.transform.SetParent(transform, false);
        glitchRoot.GetComponent<SortingGroup>().sortingLayerName = "FX";
        glitchRoot.GetComponent<SortingGroup>().sortingOrder = 55;
        _glitch = new WorldGlitchBlocks(glitchRoot.transform, 1);
        _shots = new Shot[Mathf.Max(8, _settings.ShotCapacity)];
        for (int i = 0; i < _shots.Length; i++)
        {
            var bomb = new GameObject("VisualBomb_" + i).AddComponent<SpriteRenderer>();
            bomb.transform.SetParent(transform, false); bomb.sprite = _bombSprite;
            bomb.sortingLayerName = "FX"; bomb.sortingOrder = 38;
            _shots[i] = new Shot { Bomb = bomb };
        }
        _ambientBeams = new DroneBeamVisual[_settings.AmbientBeamCount];
        for (int i = 0; i < _ambientBeams.Length; i++)
        {
            var beam = new GameObject("AmbientCombatBeam_" + i).AddComponent<DroneBeamVisual>();
            beam.transform.SetParent(transform, false); beam.Initialize(_beamMaterial, _glowMaterial); _ambientBeams[i] = beam;
        }
        _hpBarView.BeginBoss(100000m, 100000m, 1);
        _ready = true;
        BuildCrowd(); ResetPreview();
    }

    /// <summary>기존 시안 또는 E+B 결합안을 선택하고 같은 조건으로 다시 시작한다.</summary>
    public void SelectVariant(int variant) { _variant = Mathf.Clamp(variant, 0, Titles.Length - 1); if (_ready) ResetPreview(); }
    /// <summary>같은 표시 장부로 한 발만 발사한다.</summary>
    public void FireOne() { if (_ready) Launch(0f); }
    /// <summary>8발이 짧은 간격으로 맞는 겹침 시험.</summary>
    public void FireBurst() { if (!_ready) return; for (int i = 0; i < 8; i++) Launch(i * 0.035f); }
    /// <summary>주변 유닛과 드론을 늘린다. 전투 능력은 없다.</summary>
    public void ToggleCrowd() { _dense = !_dense; if (_ready) { BuildCrowd(); ResetPreview(); } }
    /// <summary>기존 드론 탄의 배경 사격을 켜거나 끈다.</summary>
    public void ToggleAmbient() { _ambient = !_ambient; RefreshText(); }
    /// <summary>자동 반복을 켜거나 끈다.</summary>
    public void ToggleLoop()
    {
        _loop = !_loop;
        if (_loop) _pressure = false;
        _nextLaunch = _clock + _settings.FirstLaunch; RefreshText();
    }
    /// <summary>여러 베탕이 계속 발사하는 밀집 비교. 실제 공격 주기를 변경하는 기능이 아니다.</summary>
    public void TogglePressure()
    {
        _pressure = !_pressure;
        if (_pressure) _loop = false;
        _nextPressure = _clock; RefreshText();
    }
    /// <summary>미리보기의 이동과 생성만 정지한다. 게임 시간 배율은 건드리지 않는다.</summary>
    public void TogglePause()
    {
        _paused = !_paused;
        foreach (var spine in _crowdRoot.GetComponentsInChildren<SkeletonAnimation>()) spine.timeScale = _paused ? 0f : 1f;
        foreach (var spine in _bossVisual.GetComponentsInChildren<SkeletonAnimation>()) spine.timeScale = _paused ? 0f : 1f;
        _marks.enabled = !_paused; RefreshText();
    }
    /// <summary>선택 시안의 적중·숫자·이동을 모두 초기화한다.</summary>
    public void ResetPreview()
    {
        if (!_ready) return;
        _clock = 0f; _hits = _granted = _receipts = _serial = _routeHighWater = 0;
        _stacks = Mathf.Clamp(_settings.StartStacks, 0, _hackingData.Capacity);
        _hudTargetStacks = _stacks;
        _nextLaunch = _settings.FirstLaunch; _hudPulseAt = -100f;
        _pressure = false; _nextPressure = 0f;
        foreach (var s in _shots) { s.Active = false; s.Bomb.enabled = false; }
        _glitch.Clear(); _marks.Clear();
        _marks.gameObject.SetActive(_variant == 1 || _variant == HybridVariant);
        _marks.enabled = !_paused;
        _gauge.Reset(_stacks, _hackingData.Capacity);
        _fx.BeginFrame(); _fx.EndFrame(); RefreshText(); PreviewReset?.Invoke();
    }

    private void BuildCrowd()
    {
        if (_crowdRoot != null) { _crowdRoot.gameObject.SetActive(false); Destroy(_crowdRoot.gameObject); }
        _owners.Clear(); _drones.Clear();
        _crowdRoot = new GameObject("VisualOnly_BetanCrowd").transform;
        _crowdRoot.SetParent(transform, false);
        int count = _dense ? _settings.DenseOwners : 1;
        for (int i = 0; i < count; i++)
        {
            int col = _dense ? i % 6 : 2, row = _dense ? i / 6 : 1;
            var go = Instantiate(_ownerTemplate, _crowdRoot); go.SetActive(true);
            go.transform.position = new Vector3(-2.72f + col * 1.088f, -3.23f + row * 1.088f, 0f);
            Sort(go, Mathf.RoundToInt(-go.transform.position.y * 100f)); _owners.Add(go.transform);
            for (int j = 0; j < _settings.DronesPerOwner; j++)
            {
                var drone = Instantiate(_droneTemplate, _crowdRoot); drone.SetActive(true);
                drone.transform.localScale *= 0.7f; Sort(drone, Mathf.RoundToInt(-go.transform.position.y * 100f) + 1); _drones.Add(drone.transform);
            }
        }
    }

    private static void Sort(GameObject go, int order)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.sortingLayerName = "Unit"; r.sortingOrder = order; }
    }

    private void Launch(float offset)
    {
        Shot free = null;
        foreach (var shot in _shots) if (!shot.Active) { free = shot; break; }
        if (free == null) return;
        int serial = _serial++;
        Vector3 from = _owners[serial % _owners.Count].position + new Vector3(0f, 0.7f, -0.6f);
        // 모든 안이 같은 기존 표식 앵커를 조준한다. B의 낙인도 정확히 이 적중점에 붙는다.
        Vector3 impact = _marks.ChipPosition(serial % HackStackMarks.Capacity);
        free.Start = from; free.Impact = impact; free.Born = _clock + offset; free.Serial = serial;
        free.HitAt = free.Born + _settings.ChargeSeconds + _settings.FlightSeconds;
        free.Active = true; free.Hit = free.Received = free.Route = false;
    }

    private void Update()
    {
        if (!_ready) return;
        float dt = _paused ? 0f : Time.deltaTime; _clock += dt;
        _fx.BeginFrame();
        if (_loop && !_paused && _clock >= _nextLaunch) { FireOne(); _nextLaunch = _clock + _settings.RepeatSeconds; }
        if (_pressure && !_paused && _clock >= _nextPressure)
        {
            Launch(0f); _nextPressure = _clock + Mathf.Max(0.03f, _settings.PressureShotInterval);
        }
        UpdateCrowd(); UpdateAmbient();
        foreach (var s in _shots)
        {
            if (!s.Active || _clock < s.Born) continue;
            float elapsed = _clock - s.Born;
            if (_clock < s.HitAt)
            {
                if (_variant == HybridVariant) s.Impact = _marks.ChipPosition(s.Serial % HackStackMarks.Capacity);
                AnimateBomb(s, elapsed);
            }
            else
            {
                s.Bomb.enabled = false;
                if (!s.Hit) Impact(s);
                float age = _clock - s.HitAt;
                DrawImpact(s, age);
                float receipt = ReceiptDelay();
                if (!s.Received && age >= receipt) { s.Received = true; _receipts++; if (s.Route) _hudPulseAt = _clock; }
                if (age >= Mathf.Max(receipt + _settings.HudPulseSeconds, _settings.BrandSeconds)) s.Active = false;
            }
        }
        _glitch.Tick(dt);
        float pulse = 1f - Mathf.Clamp01((_clock - _hudPulseAt) / _settings.HudPulseSeconds);
        if (!_externalReceiptFeedback && pulse > 0f)
        {
            _fx.Sprite(_ring, _hudCenter, Vector2.one * (220f + (1f - pulse) * 28f), Alpha(_settings.DataColor, pulse * 0.85f));
            _fx.Sprite(_sparkle, _hudCenter + new Vector2(-90f, -52f), Vector2.one * (30f + pulse * 15f), Alpha(_settings.DataColor, pulse));
        }
        _fx.EndFrame();
        if (Time.unscaledTime >= _nextStatus) { _nextStatus = Time.unscaledTime + 0.2f; RefreshText(); }
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        _hpBar.GetWorldCorners(_corners);
        Vector3 min = _uiRoot.InverseTransformPoint(_corners[0]), max = _uiRoot.InverseTransformPoint(_corners[2]);
        _gauge.HudBar = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        _gauge.Tick(Time.time, Vector2.zero);
        _hudCenter = _hudRoot.anchoredPosition;
    }

    private void AnimateBomb(Shot s, float elapsed)
    {
        s.Bomb.enabled = true;
        float charge = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _settings.ChargeSeconds));
        if (elapsed < _settings.ChargeSeconds)
        {
            s.Bomb.transform.position = s.Start;
            s.Bomb.transform.localScale = _bombScale * Mathf.Lerp(0.5f, 1f, charge);
            s.Bomb.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.45f), Mathf.Sin(charge * Mathf.PI * 4f) * 0.5f + 0.5f);
            return;
        }
        float t = Mathf.Clamp01((elapsed - _settings.ChargeSeconds) / Mathf.Max(0.01f, _settings.FlightSeconds));
        float q = t * t;
        Vector3 delta = s.Impact - s.Start;
        Vector3 side = new Vector3(-delta.y, delta.x, 0f).normalized;
        s.Bomb.transform.position = Vector3.Lerp(s.Start, s.Impact, q) + side * Mathf.Sin(q * Mathf.PI) * _settings.FlightBend * (s.Serial % 2 == 0 ? 1f : -1f);
        s.Bomb.transform.localScale = _bombScale;
        s.Bomb.color = Color.white;
        Vector2 p = Screen(s.Bomb.transform.position);
        _fx.Line(_pip, p - (Vector2)delta.normalized * 40f * t, p, 5f, Alpha(_settings.RuptureColor, 0.4f));
    }

    private void Impact(Shot s)
    {
        s.Hit = true; _hits++;
        int grant = Mathf.Min(_stacksPerBomb, Mathf.Max(0, _hackingData.Capacity - _stacks));
        _granted += grant; _stacks += grant;
        if (!_externalReceiptFeedback) ShowReceivedStacks(_stacks);
        int routes = 0;
        foreach (var other in _shots) if (other.Active && other.Hit && !other.Received && other.Route) routes++;
        s.Route = _variant != 4 && _variant != HybridVariant && routes < (_variant == 2 ? 1 : _settings.MaxHudRoutes);
        if (s.Route) _routeHighWater = Mathf.Max(_routeHighWater, routes + 1);
        int blocks = _dense ? 3 : 5;
        for (int i = 0; i < blocks; i++)
        {
            Vector3 p = s.Impact + new Vector3((i - 2f) * 0.09f, Mathf.Sin(i * 2f) * 0.16f, -0.35f);
            _glitch.Spawn(p, new Vector2(0.23f + i * 0.04f, 0.045f), _settings.RuptureColor, _settings.RuptureSeconds, 0.12f);
        }
        if (_variant == 1 || _variant == HybridVariant)
        {
            int chip = s.Serial % HackStackMarks.Capacity;
            _marks.AttachInstant(chip);
        }
        // HUD 표시는 여기서 딱 1번 갱신한다. 수신 애니메이션은 장부를 바꾸지 않는다.
        if (_variant == 4 || !s.Route) _hudPulseAt = _clock;
        if (grant > 0) StackAccepted?.Invoke(s.Impact, grant);
    }

    private float ReceiptDelay() => _variant == 3 ? _settings.CompressionSeconds + _settings.UploadSeconds :
        _variant == 4 || _variant == HybridVariant ? 0.12f : _variant == 1 ? 0.35f : _settings.UploadSeconds;

    private void DrawImpact(Shot s, float age)
    {
        Vector2 origin = Screen(s.Impact), port = _hudCenter + new Vector2(-85f, -52f);
        float q = Mathf.Clamp01(age / _settings.RuptureSeconds);
        float detail = _dense ? _settings.DenseDetailScale : 1f;
        if (q < 1f)
        {
            _fx.Sprite(_dot, origin, Vector2.one * _settings.ImpactSize * (0.7f + q), Alpha(_settings.InkColor, (1f - q) * 0.75f));
            if (_variant == 4 || _variant == HybridVariant)
            {
                for (int j = 0; j < 3; j++)
                {
                    Vector2 center = origin + new Vector2(0f, (j - 1) * 17f);
                    _fx.Line(_pip, center + Vector2.left * 32f, center + Vector2.right * 32f, 5f, Alpha(_settings.RuptureColor, 1f - q));
                }
            }
            else
            {
                int bits = _dense ? 4 : 7;
                for (int i = 0; i < bits; i++)
                {
                    float angle = (i * 137.5f + s.Serial * 31f) * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float radius = _settings.ImpactSize * q * detail;
                    if (_variant == 3) radius *= 1f - Mathf.SmoothStep(0f, 1f, age / _settings.CompressionSeconds);
                    _fx.Sprite(_pip, origin + direction * radius, new Vector2(11f, 6f) * detail, Alpha(_settings.RuptureColor, 1f - q), i * 31f);
                }
            }
        }
        if (_variant == 1 || _variant == HybridVariant)
        {
            float fade = 1f - Mathf.Clamp01(age / _settings.BrandSeconds);
            if (_variant == 1) Chip(origin, 46f * detail, Alpha(_settings.RuptureColor, fade), 0f);
            _fx.Sprite(_sparkle, origin, Vector2.one * 28f * detail, Alpha(_settings.DataColor, fade));
            if (_variant == 1 && s.Route && age < 0.35f) Circuit(origin, port, age / 0.35f, true);
        }
        else if (_variant == 2 && s.Route && age < _settings.UploadSeconds)
            Circuit(origin, port, age / _settings.UploadSeconds, false);
        else if ((_variant == 0 || _variant == 3) && s.Route)
        {
            float delay = _variant == 3 ? _settings.CompressionSeconds : 0f;
            if (_variant == 3 && age < delay)
            {
                float k = Mathf.Clamp01(age / delay);
                Chip(origin, Mathf.Lerp(55f, 20f, k) * detail, _settings.DataColor, k * 90f);
            }
            float t = (age - delay) / _settings.UploadSeconds;
            if (t >= 0f && t < 1f)
            {
                Vector2 position = Flight(origin, port, t, s.Serial);
                Vector2 previous = Flight(origin, port, Mathf.Max(0f, t - 0.09f), s.Serial);
                _fx.Line(_pip, previous, position, 8f * detail, Alpha(_settings.DataColor, 0.6f));
                float size = _settings.PacketSize * detail;
                _fx.Sprite(_dot, position, Vector2.one * size * 2.6f, Alpha(_settings.RuptureColor, 0.7f));
                Chip(position, size, _settings.DataColor, _variant == 3 ? 45f : 0f);
            }
        }
    }

    private Vector2 Flight(Vector2 from, Vector2 to, float t, int seed)
    {
        Vector2 control = (from + to) * 0.5f + new Vector2((seed % 2 == 0 ? -1f : 1f) * _settings.ArcPixels, -_settings.ArcPixels * 0.35f);
        return (1f - t) * (1f - t) * from + 2f * (1f - t) * t * control + t * t * to;
    }

    private void Circuit(Vector2 from, Vector2 to, float progress, bool scan)
    {
        Vector2 elbow = new Vector2(Mathf.Lerp(from.x, to.x, 0.7f), Mathf.Lerp(from.y, to.y, 0.35f));
        float opacity = scan ? 0.35f : 0.65f;
        Color color = Alpha(_settings.DataColor, opacity * Mathf.Sin(progress * Mathf.PI));
        float width = _settings.CircuitWidth * (_dense ? _settings.DenseDetailScale : 1f);
        Color outline = Alpha(_settings.InkColor, Mathf.Sin(progress * Mathf.PI));
        _fx.Line(_pip, from, elbow, width + 4f, outline); _fx.Line(_pip, elbow, to, width + 4f, outline);
        _fx.Line(_pip, from, elbow, width, color); _fx.Line(_pip, elbow, to, width, color);
        Vector2 pulse = progress < 0.5f ? Vector2.Lerp(from, elbow, progress * 2f) : Vector2.Lerp(elbow, to, (progress - 0.5f) * 2f);
        _fx.Sprite(_dot, pulse, Vector2.one * (scan ? 25f : 40f), Alpha(_settings.InkColor, 0.95f));
        _fx.Sprite(_sparkle, pulse, Vector2.one * (scan ? 17f : 27f), _settings.DataColor);
        if (!scan) { Chip(elbow, 15f, color, 45f); }
    }

    private void Chip(Vector2 at, float size, Color color, float angle)
    {
        _fx.Sprite(_dot, at, Vector2.one * size * 1.6f, Alpha(_settings.InkColor, color.a));
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        Vector2 a = at + (Vector2)(rotation * new Vector3(-size, -size) * 0.5f);
        Vector2 b = at + (Vector2)(rotation * new Vector3(-size, size) * 0.5f);
        Vector2 c = at + (Vector2)(rotation * new Vector3(size, size) * 0.5f);
        Vector2 d = at + (Vector2)(rotation * new Vector3(size, -size) * 0.5f);
        Color outline = Alpha(_settings.InkColor, color.a);
        _fx.Line(_pip, a, b, 7f, outline); _fx.Line(_pip, b, c, 7f, outline);
        _fx.Line(_pip, c, d, 7f, outline); _fx.Line(_pip, d, a, 7f, outline);
        _fx.Line(_pip, a, b, 3f, color); _fx.Line(_pip, b, c, 3f, color);
        _fx.Line(_pip, c, d, 3f, color); _fx.Line(_pip, d, a, 3f, color);
        _fx.Sprite(_pip, at, Vector2.one * size * 0.32f, color);
    }

    private Vector2 Screen(Vector3 world)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_uiRoot, _camera.WorldToScreenPoint(world), _camera, out var result);
        return result;
    }

    private void UpdateCrowd()
    {
        for (int i = 0; i < _drones.Count; i++)
        {
            float angle = i % _settings.DronesPerOwner * Mathf.PI * 2f / _settings.DronesPerOwner + _clock;
            _drones[i].position = _owners[i / _settings.DronesPerOwner].position + new Vector3(Mathf.Cos(angle) * 0.43f, 0.48f + Mathf.Sin(angle) * 0.16f, -0.2f);
        }
    }

    private void UpdateAmbient()
    {
        for (int i = 0; i < _ambientBeams.Length; i++)
        {
            var beam = _ambientBeams[i];
            float phase = Mathf.Repeat(_clock * 1.1f + i * 0.143f, 1f);
            if (!_ambient || phase > 0.3f || _drones.Count == 0) { beam.Clear(); continue; }
            Vector3 from = _drones[i * 5 % _drones.Count].position;
            Vector3 to = _marks.Center + new Vector3(Mathf.Sin(i) * 0.6f, Mathf.Cos(i) * 0.35f, 0f);
            Vector3 tip = Vector3.Lerp(from, to, phase / 0.3f);
            Vector3 tail = tip - Vector3.ClampMagnitude(tip - from, 0.65f);
            beam.Draw(tail, tip, new Color(0.25f, 1f, 0.82f), 0.12f, 0.8f, 0f, 0.5f);
        }
    }

    private static Color Alpha(Color color, float alpha) { color.a *= Mathf.Clamp01(alpha); return color; }

    private void RefreshText()
    {
        if (_title == null) return;
        _title.text = "9122 해킹 자폭  /  " + Titles[_variant];
        _description.text = Descriptions[_variant];
        _status.text = $"적중 {_hits}회 → 스택 +{_granted}  |  HUD {_stacks}/{_hackingData.Capacity}\n" +
            $"베탕 {_owners.Count} · 전투 드론 {_drones.Count} · {(_paused ? "정지" : "재생")}\n적중 때 +1, HUD 도착은 강조만 · 본게임 미연결";
        for (int i = 0; i < _buttons.Length; i++)
        {
            int action = i - Titles.Length;
            bool active = i < Titles.Length ? i == _variant : action == 2 ? _dense : action == 3 ? _loop :
                action == 4 ? _pressure : action == 5 ? _ambient : action == 6 && _paused;
            _buttons[i].GetComponent<Image>().color = active ? new Color(0.25f, 0.18f, 0.4f, 0.97f) : new Color(0.09f, 0.13f, 0.19f, 0.96f);
        }
    }

    private void OnDestroy()
    {
        _glitch?.Dispose(); _atlas?.Dispose();
        // HUD는 씬 Canvas가 소유한다. Play 종료 때 먼저 파괴된 RectTransform에 다시 접근하지 않는다.
    }
    private void OnDisable() { _glitch?.Clear(); }
}
