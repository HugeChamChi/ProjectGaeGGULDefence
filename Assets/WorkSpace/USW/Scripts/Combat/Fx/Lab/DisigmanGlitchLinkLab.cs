using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이펙트 실험실(FxLab_DisigmanGlitchLink) 전용 — 디시그망 스킬 레일건과 타이머 해킹 글리치를 잇는 연결 시안 비교 (사용자 요청 2026-10-12).
/// 레일건(RailgunBeamFx)과 글리치(TimerBonusLab 인게임 바인딩 = E 해킹 + J 중첩)는 인게임과 같은 컴포넌트이고, 시안마다 글리치가 "언제·어디서" 시작되는지와
/// 그 사이를 잇는 연출(데이터 조각·보스 글리치·글리치 빔)만 다르다. 시안 목록은 DisigmanGlitchLinkVariant.
/// 실험실은 글리치가 시작되는 순간 TimerController.AddTime을 부른다(표시와 값이 같이 움직이게). 인게임에 옮길 때는 시간은 발동 즉시 더하고 표시만 늦춰야 한다.
/// 슬로모션은 실험실 전용으로 Time.timeScale을 바꾼다(레일건·타이머·글리치가 같이 느려지게). 빌더: DisigmanGlitchLinkLabBuilder.
/// </summary>
public sealed class DisigmanGlitchLinkLab : MonoBehaviour
{
    private const float ChargeBlockStart = 0.35f;      // 글리치 빔: 충전이 이만큼 찬 뒤 총구에 조각이 낀다
    private const float ShardStagger = 0.012f;
    private const float ShardLifePad = 0.2f;
    private const float RingLife = 0.25f;
    private const int StatusEveryFrames = 6;
    private static readonly string[] VariantNames = { "현재", "A 착탄 후", "B 시간 강탈", "D 보스 동시", "추천 조합" };
    private static readonly string[] VariantNotes =
    {
        "발동 즉시 글리치 — 작은 신호점이 유닛에서 출발 (지금 인게임)",
        "레일건이 맞는 순간 글리치 시작 — 신호점이 착탄점에서 출발",
        "착탄점에서 데이터 조각이 튀어 타이머로 빨려 들어가고, 도착 순간 글리치",
        "착탄 순간 보스도 같은 글리치로 찢어지며 타이머와 동시에 깨짐",
        "글리치 빔(E) + 보스 글리치(D) + 데이터 조각 회수(B)",
    };
    private static readonly float[] CaptureTimes = { 0.05f, 0.3f, 0.42f, 0.5f, 0.6f, 0.72f, 0.86f, 1.05f };
    private const float CaptureTail = 0.6f;
    private const int CaptureFrameRate = 30;
    private static readonly Vector2Int CaptureResolution = new Vector2Int(540, 1168);
    private const string CaptureFolder = "Temp/FxCapture/DisigmanGlitchLink";
    private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.85f);

    private sealed class Shot
    {
        public int Unit;
        public DisigmanGlitchLinkVariant Variant;
        public Vector3 From;
        public Vector3 To;
        public float CastAt;
        public float ImpactAt;
        public bool ChargeFx;
        public bool Impacted;
        public bool Glitched;
    }

    [Header("장면 (빌더가 연결)")]
    [Tooltip("0 = 기본 발사 유닛, 1~2 = '3마리 연사'용")]
    [SerializeField] private SpriteRenderer[] _units;
    [Tooltip("유닛 발밑 기준 총구 위치 (월드) — Disigman 프리팹 Muzzle(-0.13, 0.34) × 1.7")]
    [SerializeField] private Vector2 _muzzleOffset = new Vector2(-0.22f, 0.58f);
    [SerializeField] private Transform _target;
    [SerializeField] private SpriteRenderer _boss;
    [Tooltip("보스 착탄점 무작위 범위 (월드, 반폭)")]
    [SerializeField] private Vector2 _targetSpread = new Vector2(0.5f, 0.35f);
    [Tooltip("스킬 레일건 템플릿 (비활성, 발사마다 복제)")]
    [SerializeField] private RailgunBeamFx _skillFx;

    [Header("타이머 (빌더가 연결)")]
    [SerializeField] private TimerController _timer;
    [Tooltip("인게임 바인딩 모드(_runtimeTimer 연결)의 타이머 연출 — PresentHack이 E 해킹 + J 중첩을 그린다")]
    [SerializeField] private TimerBonusLab _hack;
    [SerializeField] private TMP_Text _timerText;
    [Tooltip("_hack과 같은 연출 영역")]
    [SerializeField] private RectTransform _stage;
    [Tooltip("데이터 조각(B) 파티클 레이어")]
    [SerializeField] private RectTransform _shardLayer;

    [Header("UI (빌더가 연결)")]
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Image[] _variantButtons;
    [SerializeField] private DisigmanGlitchLinkVariant _variant = DisigmanGlitchLinkVariant.Combo;

    [Header("인게임 수치 (디시그망 SO·레일건 실험실 기준)")]
    [SerializeField] private float _startSeconds = 90f;
    [Tooltip("시간 회복량 — 디시그망 SO SecondsRecovered (현재 0.3)")]
    [SerializeField] private float _bonusSeconds = 0.3f;
    [Tooltip("스킬 레일건 충전 (초) — RailgunProjectile._chargeSeconds")]
    [SerializeField] private float _skillCharge = 0.4f;
    [SerializeField] private float _skillScale = 1.3f;
    [Tooltip("자동 발사 간격 (초)")]
    [SerializeField] private float _autoInterval = 2.6f;
    [Tooltip("'3마리 연사' 발사 간격 (초)")]
    [SerializeField] private float _volleyGap = 0.15f;
    [SerializeField] private float[] _speeds = { 1f, 0.5f, 0.25f };

    [Header("유닛·화면 타격감 (레일건 실험실과 같음)")]
    [SerializeField] private float _recoil = 0.16f;
    [SerializeField] private float _recoilReturn = 0.15f;
    [SerializeField] private float _chargeTremble = 0.025f;
    [SerializeField] private float _impactShake = 0.1f;
    [SerializeField] private float _shakeSeconds = 0.25f;

    [Header("연결 연출 색 (데이터 조각·글리치 조각)")]
    [Tooltip("밝은 하늘 배경에서도 보이게 흰색·분홍·진한 하늘색 위주 (타이머 글리치의 RGB 잔상과 같은 계열)")]
    [SerializeField] private Color[] _linkPalette =
    {
        Color.white,
        new Color(1f, 0.353f, 0.47f),
        new Color(0.05f, 0.55f, 1f),
        new Color(1f, 0.353f, 0.47f),
    };

    [Header("B 데이터 조각")]
    [SerializeField] private int _shardCount = 22;
    [Tooltip("착탄점에서 흩어지는 속도 (px/초, 최소~최대)")]
    [SerializeField] private Vector2 _shardSpeed = new Vector2(160f, 380f);
    [Tooltip("흩어졌다가 타이머로 날아가기 시작하는 시각 (초)")]
    [SerializeField] private float _shardScatterSeconds = 0.07f;
    [Tooltip("타이머까지 날아가는 시간 (초)")]
    [SerializeField] private float _shardTravel = 0.2f;
    [Tooltip("조각 크기 (px 반지름, 최소~최대)")]
    [SerializeField] private Vector2 _shardSize = new Vector2(4f, 7.5f);
    [Tooltip("날아가는 길의 가로 휨 최대 (px)")]
    [SerializeField] private float _shardCurve = 90f;

    [Header("D 보스 글리치")]
    [SerializeField] private float _bossGlitchSeconds = 0.3f;
    [SerializeField] private float _bossJitter = 0.12f;
    [SerializeField] private float _bossGhostOffset = 0.16f;
    [Tooltip("AllIn1 _GlitchAmount 최대치")]
    [SerializeField] private float _bossGlitchAmount = 8f;

    [Header("E 글리치 빔")]
    [SerializeField] private int _beamBlocks = 22;
    [SerializeField] private int _impactBlocks = 16;
    [SerializeField] private int _chargeBlocks = 6;
    [SerializeField] private int _tearLines = 3;
    [SerializeField] private int _blockSortingOrder = 38;

    private readonly List<Shot> _shots = new List<Shot>();
    private readonly List<(float At, int Unit)> _queued = new List<(float, int)>();
    private WorldGlitchBlocks _blocks;
    private BossGlitchFx _bossFx;
    private TimerBonusSprites _sprites;
    private TimerBonusParticles _particles;
    private Vector3[] _unitHome;
    private float[] _fireAt;
    private float[] _chargeFrom;
    private Transform _cam;
    private Vector3 _camHome;
    private float _clock;
    private float _nextAuto;
    private float _shakeAt = -999f;
    private float _lastGlitchDelay = -1f;
    private bool _auto = true;
    private bool _capturing;
    private int _speedIndex;
    private Color _cyan = Color.cyan;
    private Color _hot = Color.magenta;
    private Color[] _palette;

    private void Start()
    {
        var cam = Camera.main;
        if (cam != null) { _cam = cam.transform; _camHome = _cam.position; }
        int n = _units != null ? _units.Length : 0;
        _unitHome = new Vector3[n];
        _fireAt = new float[n];
        _chargeFrom = new float[n];
        for (int i = 0; i < n; i++) _unitHome[i] = _units[i] != null ? _units[i].transform.position : Vector3.zero;

        if (_hack != null)
        {
            _hack.UseUnscaledTime = false;   // 슬로모션(timeScale)에 레일건과 같이 따라가게
            if (_hack.Settings != null) { _cyan = _hack.Settings.CyanColor; _hot = _hack.Settings.HotColor; }
        }
        _palette = _linkPalette != null && _linkPalette.Length > 0 ? _linkPalette : new[] { Color.white, _hot, _cyan };
        _blocks = new WorldGlitchBlocks(transform, _blockSortingOrder);
        if (_boss != null) _bossFx = new BossGlitchFx(_boss, _cyan, _hot);
        if (_shardLayer != null)
        {
            _sprites = new TimerBonusSprites();
            _particles = new TimerBonusParticles(_shardLayer, _sprites.SoftDot, _sprites.Ring);
        }
        RefreshButtons();
        Play();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        _blocks?.Dispose();
        _bossFx?.Dispose();
        _particles?.Clear();
        _sprites?.Dispose();
    }

    // ── 버튼 ──────────────────────────────────────────────────

    /// <summary>타이머·연출을 비우고 지금 시안으로 스킬 한 발부터 다시.</summary>
    public void Play()
    {
        _shots.Clear();
        _queued.Clear();
        _blocks?.Clear();
        _bossFx?.Restore();
        _particles?.Clear();
        if (_hack != null) _hack.CancelPresentation();
        if (_timer != null) _timer.StartTimer(_startSeconds);
        _clock = 0f;
        _shakeAt = -999f;
        _lastGlitchDelay = -1f;
        if (_fireAt != null) for (int i = 0; i < _fireAt.Length; i++) _fireAt[i] = _chargeFrom[i] = -999f;
        FireSkill(0);
        _nextAuto = _autoInterval;
    }

    /// <summary>시안 선택 (DisigmanGlitchLinkVariant 순서) 후 처음부터.</summary>
    public void SelectVariant(int index)
    {
        _variant = (DisigmanGlitchLinkVariant)Mathf.Clamp(index, 0, VariantNames.Length - 1);
        RefreshButtons();
        Play();
    }

    /// <summary>기본 유닛 스킬 한 발.</summary>
    public void FireOne() => FireSkill(0);

    /// <summary>세 마리가 _volleyGap 간격으로 연달아 스킬 — J 중첩(합산·이어 붙임)과 겹쳐 보기.</summary>
    public void FireVolley()
    {
        int n = _units != null ? _units.Length : 0;
        for (int i = 0; i < n; i++) _queued.Add((_clock + i * _volleyGap, i));
    }

    /// <summary>자동 발사 켜기/끄기.</summary>
    public void ToggleAuto()
    {
        _auto = !_auto;
        _nextAuto = _clock + _autoInterval;
        UpdateStatus();
    }

    /// <summary>1 → 0.5 → 0.25배속 순환 (실험실 전용 timeScale).</summary>
    public void NextSpeed()
    {
        _speedIndex = (_speedIndex + 1) % Mathf.Max(1, _speeds.Length);
        Time.timeScale = _speeds.Length > 0 ? _speeds[_speedIndex] : 1f;
        UpdateStatus();
    }

    /// <summary>
    /// 검증용 — 시안 5개를 차례로 한 발씩 쏘며 정해진 시각(CaptureTimes, 발동 기준 초)의 화면을 PNG로 저장한다.
    /// 프레임 시간을 고정(captureFramerate)해 매번 같은 시각을 찍는다. 메뉴: Tools/USW/Fx/Disigman Glitch Link - Capture All (Play).
    /// </summary>
    public void CaptureAllVariants()
    {
        if (_capturing) return;
        CaptureAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // ── 발사·연결 ─────────────────────────────────────────────

    private void FireSkill(int unit)
    {
        if (_units == null || unit < 0 || unit >= _units.Length || _units[unit] == null || _skillFx == null || _target == null) return;
        Vector3 from = _unitHome[unit] + (Vector3)_muzzleOffset;
        Vector3 to = _target.position + new Vector3(Random.Range(-_targetSpread.x, _targetSpread.x),
            Random.Range(-_targetSpread.y, _targetSpread.y), 0f);
        var fx = Instantiate(_skillFx);
        fx.gameObject.SetActive(true);
        fx.Play(transform, from, to, _skillCharge, _skillScale);

        var shot = new Shot { Unit = unit, Variant = _variant, From = from, To = to, CastAt = _clock, ImpactAt = _clock + _skillCharge };
        _shots.Add(shot);
        _chargeFrom[unit] = _clock;
        _fireAt[unit] = shot.ImpactAt;
        // 지금 인게임: 스킬이 받아들여지는 순간(TryCast) 시간 추가 → 바로 글리치, 신호는 유닛 위치에서 출발.
        if (_variant == DisigmanGlitchLinkVariant.Current) Glitch(shot, _units[unit].transform.position, ToStage(_units[unit].transform.position));
    }

    private void TickShots()
    {
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var s = _shots[i];
            if (s.Variant == DisigmanGlitchLinkVariant.Combo && !s.ChargeFx && _clock >= s.CastAt + _skillCharge * ChargeBlockStart)
            {
                s.ChargeFx = true;
                ChargeBlocks(s.From, s.ImpactAt - _clock);
            }
            if (s.Impacted || _clock < s.ImpactAt) continue;
            s.Impacted = true;
            OnImpact(s);
            _shots.RemoveAt(i);
        }
    }

    private void OnImpact(Shot s)
    {
        _shakeAt = _clock;
        switch (s.Variant)
        {
            case DisigmanGlitchLinkVariant.AfterImpact:
                Glitch(s, s.To, ToStage(s.To));
                break;
            case DisigmanGlitchLinkVariant.StealShards:
                Shards(s);
                break;
            case DisigmanGlitchLinkVariant.BossGlitch:
                BossGlitch();
                Glitch(s, s.To, ToStage(s.To));
                break;
            case DisigmanGlitchLinkVariant.Combo:
                BeamGlitch(s.From, s.To);
                ImpactBlocks(s.To);
                BossGlitch();
                Shards(s);
                break;
        }
    }

    // 실제 시간 추가 + 표시. 인게임 BossClearPresentation.PresentHack과 같은 호출 (origin은 신호가 출발할 연출 영역 좌표).
    private void Glitch(Shot s, Vector3? world, Vector2 originOnStage)
    {
        if (s.Glitched || _timer == null || _hack == null) return;
        s.Glitched = true;
        _lastGlitchDelay = _clock - s.CastAt;
        _timer.AddTime(_bonusSeconds, TimeAddSource.Hack, world);
        _hack.PresentHack(_timer, _bonusSeconds, originOnStage);
    }

    private void Shards(Shot s)
    {
        if (_particles == null || _hack == null) { Glitch(s, s.To, ToStage(s.To)); return; }
        float px = _hack.Px;
        Vector2 from = ToStage(s.To);
        Vector2 to = _hack.TimerPosition;
        _particles.Emit(new TimerBonusParticles.Particle
        {
            Kind = TimerBonusParticles.Kind.Ring, Position = from, RadiusFrom = 8f * px, RadiusTo = 60f * px,
            Color = Color.white, MaxLife = RingLife,
        });
        for (int i = 0; i < _shardCount; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float delay = _shardScatterSeconds + i * ShardStagger;
            var p = new TimerBonusParticles.Particle
            {
                Kind = TimerBonusParticles.Kind.Home,
                Position = from,
                Velocity = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(_shardSpeed.x, _shardSpeed.y) * px,
                Drag = 0.12f,
                Size = Random.Range(_shardSize.x, _shardSize.y) * px,
                HomeDelay = delay,
                HomeDuration = _shardTravel,
                Target = to,
                Curve = Random.Range(-_shardCurve, _shardCurve) * px,
                MaxLife = delay + _shardTravel + ShardLifePad,
                Color = _palette[i % _palette.Length],
            };
            // 첫 조각이 닿는 순간 글리치 — 신호 출발점을 타이머로 줘서 기존 신호점은 도착 섬광처럼 타이머에서 터진다.
            if (i == 0) p.OnArrive = _ => Glitch(s, null, to);
            _particles.Emit(p);
        }
    }

    private void BossGlitch() => _bossFx?.Trigger(_bossGlitchSeconds, _bossJitter, _bossGhostOffset, _bossGlitchAmount);

    // 빔 길 위로 가로 띠 조각이 깜빡이며 튄다 — 타이머 글리치의 가로 띠 어긋남과 같은 모양.
    private void BeamGlitch(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        Vector3 n = new Vector3(-d.y, d.x, 0f).normalized;
        for (int i = 0; i < _beamBlocks; i++)
        {
            Vector3 pos = from + d * Random.value + n * Random.Range(-0.14f, 0.14f);
            _blocks.Spawn(pos, new Vector2(Random.Range(0.25f, 0.7f), Random.Range(0.05f, 0.11f)), Pick(), Random.Range(0.12f, 0.24f), 0.14f);
        }
    }

    private void ImpactBlocks(Vector3 at)
    {
        for (int i = 0; i < _impactBlocks; i++)
        {
            float size = Random.Range(0.1f, 0.24f);
            _blocks.Spawn(at + (Vector3)(Random.insideUnitCircle * 0.6f), new Vector2(size, size), Pick(), Random.Range(0.18f, 0.35f), 0.08f);
        }
        for (int i = 0; i < _tearLines; i++)
            _blocks.Spawn(at + new Vector3(0f, Random.Range(-0.5f, 0.5f), 0f), new Vector2(Random.Range(1.2f, 2.2f), 0.025f),
                i % 2 == 0 ? _hot : Color.white, 0.14f, 0.25f);
    }

    private void ChargeBlocks(Vector3 at, float life)
    {
        for (int i = 0; i < _chargeBlocks; i++)
        {
            float size = Random.Range(0.05f, 0.12f);
            _blocks.Spawn(at + (Vector3)(Random.insideUnitCircle * 0.35f), new Vector2(size, size), Pick(), life, 0.05f);
        }
    }

    private Color Pick() => _palette[Random.Range(0, _palette.Length)];

    private Vector2 ToStage(Vector3 world)
    {
        var cam = Camera.main;
        if (cam == null || _stage == null) return Vector2.zero;
        return DamageStyleLabUtil.ScreenToLocal(_stage, RectTransformUtility.WorldToScreenPoint(cam, world));
    }

    // ── 매 프레임 ─────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.deltaTime;
        _clock += dt;
        if (_auto && !_capturing && _clock >= _nextAuto) { FireSkill(0); _nextAuto = _clock + _autoInterval; }
        for (int i = _queued.Count - 1; i >= 0; i--)
        {
            if (_clock < _queued[i].At) continue;
            int unit = _queued[i].Unit;
            _queued.RemoveAt(i);
            FireSkill(unit);
        }
        TickShots();
        _blocks?.Tick(dt);
        _bossFx?.Tick(dt);
        _particles?.Tick(dt);
        UpdateUnits();
        UpdateCamera();
        if (_timerText != null && _timer != null && (_hack == null || !_hack.IsPresentingHack))
            _timerText.text = _timer.RemainingTime.ToString("F2", CultureInfo.InvariantCulture);
        if (Time.frameCount % StatusEveryFrames == 0) UpdateStatus();
    }

    // 발사 순간 아래로 밀렸다가 돌아오고, 충전 중에는 찰수록 부르르 떨린다 (레일건 실험실과 같음).
    private void UpdateUnits()
    {
        if (_units == null) return;
        for (int i = 0; i < _units.Length; i++)
        {
            if (_units[i] == null) continue;
            float since = _clock - _fireAt[i];
            float kick = since >= 0f && since < _recoilReturn ? 1f - since / _recoilReturn : 0f;
            Vector3 offset = Vector3.down * (_recoil * kick * kick);
            if (_clock < _fireAt[i] && _clock >= _chargeFrom[i])
            {
                float p = Mathf.Clamp01((_clock - _chargeFrom[i]) / Mathf.Max(0.01f, _fireAt[i] - _chargeFrom[i]));
                float amp = _chargeTremble * p * p;
                offset += new Vector3(Mathf.Sin(_clock * 97f) * amp, Mathf.Sin(_clock * 131f + 1.3f) * amp * 0.6f, 0f);
            }
            _units[i].transform.position = _unitHome[i] + offset;
        }
    }

    private void UpdateCamera()
    {
        if (_cam == null) return;
        float st = _clock - _shakeAt;
        float k = st >= 0f && st < _shakeSeconds ? 1f - st / _shakeSeconds : 0f;
        float a = _impactShake * k * k;
        _cam.position = _camHome + new Vector3(Mathf.Sin(_clock * 83f) * a, Mathf.Sin(_clock * 71f + 0.7f) * a, 0f);
    }

    private void RefreshButtons()
    {
        if (_variantButtons != null)
            for (int i = 0; i < _variantButtons.Length; i++)
                if (_variantButtons[i] != null) _variantButtons[i].color = i == (int)_variant ? ButtonSelected : ButtonIdle;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_status == null) return;
        int v = (int)_variant;
        string glitch = _lastGlitchDelay >= 0f ? $"{_lastGlitchDelay:0.00}s" : "-";
        float speed = _speeds.Length > 0 ? _speeds[Mathf.Clamp(_speedIndex, 0, _speeds.Length - 1)] : 1f;
        _status.text = $"<b>{VariantNames[v]}</b>  {VariantNotes[v]}\n"
            + $"발사 0.00s → 착탄 {_skillCharge:0.00}s → 글리치 시작 {glitch}   /   {speed:0.##}배속   /   자동 {(_auto ? "켬" : "끔")}";
    }

    // ── 검증 캡처 ─────────────────────────────────────────────

    private async UniTaskVoid CaptureAsync(CancellationToken token)
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[DisigmanGlitchLinkLab] 카메라 없음"); return; }
        _capturing = true;
        string dir = Path.Combine(Directory.GetCurrentDirectory(), CaptureFolder);
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);

        var rt = new RenderTexture(CaptureResolution.x, CaptureResolution.y, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(CaptureResolution.x, CaptureResolution.y, TextureFormat.RGB24, false);
        var prevTarget = cam.targetTexture;
        int prevCapture = Time.captureFramerate;
        var prevVariant = _variant;
        float prevScale = Time.timeScale;
        try
        {
            cam.targetTexture = rt;
            Time.captureFramerate = CaptureFrameRate;
            Time.timeScale = 1f;
            await UniTask.DelayFrame(3, cancellationToken: token);
            for (int v = 0; v < VariantNames.Length; v++)
            {
                Random.InitState(1234);
                SelectVariant(v);
                int k = 0;
                while (k < CaptureTimes.Length)
                {
                    await UniTask.WaitForEndOfFrame(this, token);
                    if (_clock + 0.5f / CaptureFrameRate < CaptureTimes[k]) continue;
                    var prevActive = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply(false);
                    RenderTexture.active = prevActive;
                    File.WriteAllBytes(Path.Combine(dir, $"v{v}_{k}_{_clock:0.00}.png"), tex.EncodeToPNG());
                    k++;
                }
                float until = _clock + CaptureTail;
                while (_clock < until) await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            Debug.Log($"[DisigmanGlitchLinkLab] 캡처 완료 → {dir}");
        }
        finally
        {
            Time.captureFramerate = prevCapture;
            Time.timeScale = prevScale;
            if (cam != null) cam.targetTexture = prevTarget;
            rt.Release();
            Destroy(rt);
            Destroy(tex);
            _capturing = false;
            _variant = prevVariant;
            RefreshButtons();
        }
    }
}
