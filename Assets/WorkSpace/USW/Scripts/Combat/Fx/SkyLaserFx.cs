using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>하늘 레이저 발사 방식.</summary>
public enum SkyLaserMode
{
    /// <summary>짧은 빔을 끊어서 여러 번 내리꽂는다 (레퍼런스 design/레이저연출예시1.mp4).</summary>
    Pulse,
    /// <summary>충전 후 굵은 빔을 길게 유지한다. 반복 설정 시 주기적으로 다시 쏜다 (레퍼런스 design/레이저연출예시2.gif).</summary>
    Sustain,
}

/// <summary>
/// 하늘 레이저 연출 — 타겟 위 공중에 발사판이 나타나 아래로 레이저를 쏜다. 월드 공간(FX 정렬 레이어) 전용.
/// Pulse: 발사판(들)이 번갈아 짧은 빔을 "뚝뚝" 끊어 쏘고, 맞을 때마다 섬광·충격파·스파크가 튄다. 마지막 발은 굵다.
/// Sustain: 발사판에 빛이 모인 뒤 굵은 빔이 "찌이잉" 유지되며 착탄점에서 스파크가 계속 튄다. _loop이면 쉬었다 반복.
/// 순서(언제 쏠지)는 UniTask 시퀀스가, 감쇠(섬광이 사그라드는 모양)는 매 프레임 LateUpdate가 계산한다 —
/// 같은 섬광이 겹쳐 다시 터져도 트윈을 끊을 필요가 없다.
/// 쿼드 색·밝기는 MaterialPropertyBlock으로 넣는다 (머티리얼은 공유). 시간은 기본 scaled — 인게임 일시정지 시 멈춘다.
/// 요소 생성·연결은 SkyLaserFxLabBuilder(Tools/USW/Sky Laser FX)가 한다.
/// </summary>
public class SkyLaserFx : MonoBehaviour, IFxLabPlayable
{
    /// <summary>발사판 하나와 그 빔.</summary>
    [Serializable]
    public class EmitterRig
    {
        [Tooltip("발사판 루트 — 재생 시 타겟 + Offset 위치로 옮긴다")] public Transform Root;
        [Tooltip("타겟 기준 발사판 위치 (월드)")] public Vector2 Offset = new Vector2(0f, 5f);
        [Tooltip("타겟 기준 앞뒤 깊이 (월드 z, 음수 = 카메라 쪽). 원근 카메라에서 크기·빔 각도가 달라져 2.5D 느낌")] public float Depth;
        [Tooltip("발사판 프레임 쿼드")] public Renderer Pad;
        [Tooltip("발사판 안쪽 마름모 (빛나는 렌즈)")] public Renderer PadCore;
        [Tooltip("발사판 뒤 번짐 쿼드")] public Renderer PadGlow;
        [Tooltip("발사 직전 번쩍임 쿼드")] public Renderer Charge;
        [Tooltip("Sustain 충전 중 안쪽으로 조여드는 고리 (Pulse는 비워 둠)")] public Renderer ChargeRing;
        [Tooltip("빔 쿼드 (USW/Fx/LaserBeam)")] public Renderer Beam;

        [Header("드론 발사체 (선택 — 비우면 발사판)")]
        [Tooltip("발사판 대신 쓰는 드론 몸체. Root의 자식, 렌즈가 타겟을 향하도록 회전한다")] public SpriteRenderer Drone;
        [Tooltip("대기 스프라이트")] public Sprite DroneIdle;
        [Tooltip("발사 중 스프라이트 (날개 펼침)")] public Sprite DroneFire;
        [Tooltip("스프라이트 원본에서 렌즈가 향하는 각도 (도, 0 = 오른쪽, 180 = 왼쪽)")] public float DroneFacing = 180f;
        [Tooltip("빔이 나가는 렌즈 위치 — Root에서 조준 방향으로의 거리 (월드)")] public float Muzzle;

        [NonSerialized] public float Visible;     // 발사판 표시량 0~1
        [NonSerialized] public float FlashTime;   // 마지막 번쩍임 시각 (로컬 시계)
        [NonSerialized] public float ChargeAmount; // Sustain 충전 진행 0~1
        [NonSerialized] public bool Firing;        // 빔을 쏘는 중 (드론 발사 스프라이트)
        [NonSerialized] public Vector3 AimDir;     // 발사판 → 타겟 단위 벡터
        [NonSerialized] public Vector3 DroneScale;
        [NonSerialized] public bool Behind;        // 타겟보다 뒤 (보스에 가려지도록 정렬 전환됨)
        [NonSerialized] public string FrontLayer;
        [NonSerialized] public int FrontOrder;
        [NonSerialized] public Vector3 PadScale;
        [NonSerialized] public Vector3 CoreScale;
        [NonSerialized] public Vector3 GlowScale;
        [NonSerialized] public Vector3 ChargeScale;
        [NonSerialized] public Vector3 RingScale;
    }

    private static readonly int ColorId  = Shader.PropertyToID("_Color");
    private static readonly int LengthId = Shader.PropertyToID("_Length");
    private static readonly int AlphaId  = Shader.PropertyToID("_Alpha");
    private static readonly int SeedId   = Shader.PropertyToID("_Seed");

    private const float NeverTime = -999f;

    [Header("공통")]
    [SerializeField] private SkyLaserMode _mode = SkyLaserMode.Pulse;
    [Tooltip("레이저 색 (빔 번짐·발사판·충격파). 심지와 섬광 중심은 흰색")]
    [SerializeField] private Color _tint = new Color(0.25f, 1f, 0.85f, 1f);
    [Tooltip("파라미터 없는 Play()와 실험실에서 쓰는 타겟")]
    [SerializeField] private Transform _target;
    [Tooltip("활성화될 때 _target으로 자동 재생 (실험실용)")]
    [SerializeField] private bool _playOnEnable;

    [Header("요소 (빌더가 연결)")]
    [SerializeField] private EmitterRig[] _emitters = Array.Empty<EmitterRig>();
    [Tooltip("착탄 섬광 (밝은 원)")]
    [SerializeField] private Renderer _impactFlash;
    [Tooltip("착탄 충격파 고리 (바닥 원근으로 납작)")]
    [SerializeField] private Renderer _impactRing;
    [Tooltip("착탄 십자 반짝임")]
    [SerializeField] private Renderer _impactFlare;
    [Tooltip("Sustain 착탄점 지속 번짐")]
    [SerializeField] private Renderer _impactGlow;
    [SerializeField] private ParticleSystem _sparks;

    [Header("발사판")]
    [Tooltip("발사판이 나타나는 시간")]
    [SerializeField] private float _padAppear = 0.12f;
    [Tooltip("발사판이 사라지는 시간")]
    [SerializeField] private float _padDisappear = 0.15f;
    [Tooltip("발사판이 등장할 때 위에서 내려오는 거리")]
    [SerializeField] private float _padDrop = 0.35f;
    [Tooltip("발사판 번쩍임 지속")]
    [SerializeField] private float _padFlashDuration = 0.12f;
    [Tooltip("드론: 위아래 둥실거림 폭 (월드)")]
    [SerializeField] private float _droneBob = 0.05f;
    [Tooltip("드론: 둥실거림 속도 (rad/s)")]
    [SerializeField] private float _droneBobSpeed = 5f;
    [Tooltip("드론: 발사 순간 뒤로 밀리는 거리 (월드)")]
    [SerializeField] private float _droneRecoil = 0.08f;

    [Header("Pulse — 뚝뚝 끊어 쏘기")]
    [SerializeField] private int _pulseCount = 5;
    [Tooltip("발 시작 간격 — 한 발이 다 꺼지기 전에 다음 발사판이 쏜다 (발사판을 번갈아 쓰므로 겹쳐도 된다)")]
    [SerializeField] private float _pulseInterval = 0.17f;
    [Tooltip("번쩍임 후 빔이 나가기까지")]
    [SerializeField] private float _pulseCharge = 0.05f;
    [Tooltip("빔이 발사판에서 착탄점까지 뻗는 시간")]
    [SerializeField] private float _pulseExtend = 0.04f;
    [Tooltip("빔 최대 굵기 유지")]
    [SerializeField] private float _pulseHold = 0.06f;
    [Tooltip("빔이 가늘어지며 꺼지는 시간")]
    [SerializeField] private float _pulseFade = 0.09f;
    [Tooltip("빔 굵기 (월드)")]
    [SerializeField] private float _pulseWidth = 0.62f;
    [Tooltip("마지막 발 굵기 배율")]
    [SerializeField] private float _pulseFinalWidthScale = 1.7f;
    [Tooltip("마지막 발 유지 배율")]
    [SerializeField] private float _pulseFinalHoldScale = 2.5f;
    [Tooltip("한 발당 스파크 수")]
    [SerializeField] private int _pulseSparks = 14;

    [Header("Sustain — 찌이잉 유지")]
    [Tooltip("발사판에 빛이 모이는 시간")]
    [SerializeField] private float _sustainCharge = 0.4f;
    [SerializeField] private float _sustainExtend = 0.06f;
    [Tooltip("빔 유지 시간")]
    [SerializeField] private float _sustainDuration = 1.1f;
    [Tooltip("빔이 가늘어지며 꺼지는 시간")]
    [SerializeField] private float _sustainFade = 0.16f;
    [Tooltip("빔 굵기 (월드)")]
    [SerializeField] private float _sustainWidth = 0.95f;
    [Tooltip("굵기 떨림 비율 (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _sustainJitter = 0.14f;
    [Tooltip("굵기 떨림 속도")]
    [SerializeField] private float _sustainJitterSpeed = 38f;
    [Tooltip("빔이 착탄점에 닿는 순간 굵기 배율 (점점 원래 굵기로)")]
    [SerializeField] private float _sustainImpactWidthScale = 1.6f;
    [Tooltip("유지 중 초당 스파크 수")]
    [SerializeField] private float _sustainSparkRate = 60f;
    [Tooltip("유지 중 착탄 섬광을 다시 터뜨리는 간격")]
    [SerializeField] private float _sustainFlashInterval = 0.22f;
    [Tooltip("끝나면 쉬었다 다시 쏜다")]
    [SerializeField] private bool _loop = true;
    [Tooltip("반복 사이 쉬는 시간")]
    [SerializeField] private float _loopInterval = 0.9f;

    [Header("2.5D 궤도")]
    [Tooltip("발사판/드론이 타겟 위 수직축을 도는 속도 (도/초, 0 = 고정, 음수 = 반대 방향). 깊이가 바뀌며 원근으로 커지고 작아진다")]
    [SerializeField] private float _orbitSpeed;
    [Tooltip("타겟보다 이만큼 뒤로 가면 드론을 보스 뒤 정렬로 바꾼다 (월드 z)")]
    [SerializeField] private float _behindThreshold = 0.15f;
    [Tooltip("뒤로 갔을 때 드론·번짐 정렬 레이어 (보스보다 아래)")]
    [SerializeField] private string _behindSortingLayer = "Default";
    [SerializeField] private int _behindSortingOrder = -1;
    [Tooltip("빔이 카메라를 향하도록 돌릴 때 쓰는 카메라 (비우면 Camera.main)")]
    [SerializeField] private Camera _camera;

    [Header("착탄 감쇠")]
    [SerializeField] private float _flashDuration = 0.14f;
    [SerializeField] private float _ringDuration = 0.28f;
    [SerializeField] private float _flareDuration = 0.12f;
    [Tooltip("충격파 고리가 퍼지는 최대 배율")]
    [SerializeField] private float _ringExpand = 2.2f;
    [Tooltip("스파크 색 — 흰색에서 레이저 색으로 섞는 비율")]
    [Range(0f, 1f)] [SerializeField] private float _sparkTintMix = 0.55f;

    private MaterialPropertyBlock _mpb;
    private CancellationTokenSource _runCts;
    private float _clock;
    private float _hitTime = NeverTime;
    private float _hitScale = 1f;
    private float _impactGlowAmount;
    private Vector3 _flashScale, _ringScale, _flareScale, _impactGlowScale;
    private bool _cached;
    private Vector3 _rigBase; // 타겟의 로컬 위치 — 발사판 Offset의 기준
    private float _orbitAngle; // 재생 중 누적 궤도 각 (도)

    /// <inheritdoc/>
    public bool UseUnscaledTime { get; set; }

    /// <summary>레이저 색. 바꾸면 다음 프레임부터 반영된다.</summary>
    public Color Tint
    {
        get => _tint;
        set => _tint = value;
    }

    /// <summary>Sustain 반복 여부. 끄면 한 번 쏘고 끝난다.</summary>
    public bool Loop
    {
        get => _loop;
        set => _loop = value;
    }

    /// <summary>재생 중이면 true.</summary>
    public bool IsPlaying => _runCts != null;

    private float Delta => UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private void Awake()
    {
        CacheScales();
        HideAll();
    }

    private void OnEnable()
    {
        if (_playOnEnable && _target != null) Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    /// <summary>_target 위치로 처음부터 재생한다 (실험실/IFxLabPlayable).</summary>
    public void Play()
    {
        Play(_target != null ? _target.position : transform.position);
    }

    /// <summary>지정 월드 위치를 향해 처음부터 재생한다. 재생 중이면 끊고 다시 시작한다.</summary>
    public void Play(Vector3 target)
    {
        CacheScales();
        CancelRun();
        HideAll();
        target.z = transform.position.z;
        _orbitAngle = 0f;
        if (_camera == null) _camera = Camera.main;
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        RunAsync(target, _runCts).Forget();
    }

    /// <summary>즉시 멈추고 모두 숨긴다.</summary>
    public void Stop()
    {
        CancelRun();
        HideAll();
    }

    private void CancelRun()
    {
        if (_runCts == null) return;
        var cts = _runCts;
        _runCts = null;
        cts.Cancel();
    }

    private async UniTaskVoid RunAsync(Vector3 target, CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            PlaceRig(target);
            if (_mode == SkyLaserMode.Pulse) await PulseAsync(target, token);
            else
            {
                do
                {
                    await SustainAsync(target, token);
                    if (_loop) await WaitAsync(_loopInterval, token);
                } while (_loop);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_runCts == cts) _runCts = null;
            cts.Dispose();
        }
    }

    // ── 시퀀스 ─────────────────────────────────────────────

    private async UniTask PulseAsync(Vector3 target, CancellationToken token)
    {
        await ShowPadsAsync(token);

        // 발사판을 번갈아 쓰며 _pulseInterval마다 한 발씩 — 앞 발이 꺼지는 동안 다음 발이 꽂힌다
        var shots = new UniTask[_pulseCount];
        for (int i = 0; i < _pulseCount; i++)
        {
            shots[i] = FirePulseAsync(_emitters[i % _emitters.Length], target, i, i == _pulseCount - 1, token);
            if (i < _pulseCount - 1) await WaitAsync(_pulseInterval, token);
        }
        await UniTask.WhenAll(shots);

        await WaitAsync(_ringDuration * 0.5f, token);
        await HidePadsAsync(token);
    }

    private async UniTask FirePulseAsync(EmitterRig rig, Vector3 target, int index, bool last, CancellationToken token)
    {
        float width = _pulseWidth * (last ? _pulseFinalWidthScale : 1f);
        float hold = _pulseHold * (last ? _pulseFinalHoldScale : 1f);
        float seed = index * 1.37f;
        rig.Firing = true;
        rig.FlashTime = _clock;
        await WaitAsync(_pulseCharge, token);

        // 뻗기: 빔 끝이 발사판에서 착탄점으로 내려간다
        await TweenAsync(_pulseExtend, p =>
        {
            Vector3 from = MuzzleOf(rig);
            Vector3 head = Vector3.Lerp(from, target, EaseOutQuad(p));
            SetBeam(rig.Beam, from, head, width * Mathf.Lerp(0.6f, 1f, p), 1f, seed);
        }, token);

        Hit(last ? 1.5f : 1f, last ? _pulseSparks * 2 : _pulseSparks);

        float holdT = 0f;
        while (holdT < hold)
        {
            // 유지 중에도 굵기가 살짝 출렁여 "지직" 느낌
            float wobble = 1f + 0.08f * Mathf.Sin((_clock + seed) * 70f);
            SetBeam(rig.Beam, MuzzleOf(rig), target, width * wobble, 1f, seed);
            await NextFrame(token);
            holdT += Delta;
        }

        await TweenAsync(_pulseFade, p =>
            SetBeam(rig.Beam, MuzzleOf(rig), target, width * (1f - EaseInQuad(p)) + 0.001f, 1f - p * p, seed), token);
        HideBeam(rig.Beam);
        rig.Firing = false;
    }

    private async UniTask SustainAsync(Vector3 target, CancellationToken token)
    {
        await ShowPadsAsync(token);

        // 충전: 고리가 조여들며 빛 구체가 커진다
        await TweenAsync(_sustainCharge, p =>
        {
            foreach (var rig in _emitters) rig.ChargeAmount = p;
        }, token);
        foreach (var rig in _emitters)
        {
            rig.FlashTime = _clock;
            rig.Firing = true;
        }

        float seed = UnityEngine.Random.value * 10f;
        await TweenAsync(_sustainExtend, p =>
        {
            foreach (var rig in _emitters)
            {
                Vector3 from = MuzzleOf(rig);
                SetBeam(rig.Beam, from, Vector3.Lerp(from, target, EaseOutQuad(p)), _sustainWidth * Mathf.Lerp(0.5f, 1f, p), 1f, seed);
            }
        }, token);

        Hit(1.4f, _pulseSparks * 2);
        float t = 0f, sparkAccum = 0f, nextFlash = _sustainFlashInterval;
        while (t < _sustainDuration)
        {
            float impactBoost = Mathf.Lerp(_sustainImpactWidthScale, 1f, Mathf.Clamp01(t / 0.25f));
            float jitter = 1f + _sustainJitter * (Mathf.Sin(t * _sustainJitterSpeed) * 0.6f
                                                + Mathf.Sin(t * _sustainJitterSpeed * 2.3f + 1.7f) * 0.4f);
            foreach (var rig in _emitters)
            {
                rig.ChargeAmount = 1f;
                SetBeam(rig.Beam, MuzzleOf(rig), target, _sustainWidth * impactBoost * jitter, 1f, seed);
            }
            _impactGlowAmount = Mathf.Clamp01(t / 0.08f) * (0.85f + 0.15f * jitter);

            sparkAccum += _sustainSparkRate * Delta;
            int emit = (int)sparkAccum;
            if (emit > 0) { EmitSparks(emit); sparkAccum -= emit; }

            if (t >= nextFlash)
            {
                Hit(0.75f, 0);
                nextFlash += _sustainFlashInterval;
            }

            await NextFrame(token);
            t += Delta;
        }

        await TweenAsync(_sustainFade, p =>
        {
            foreach (var rig in _emitters)
            {
                rig.ChargeAmount = 1f - p;
                SetBeam(rig.Beam, MuzzleOf(rig), target, _sustainWidth * (1f - EaseInQuad(p)) + 0.001f, 1f - p * p, seed);
            }
            _impactGlowAmount = 1f - p;
        }, token);
        foreach (var rig in _emitters)
        {
            HideBeam(rig.Beam);
            rig.ChargeAmount = 0f;
            rig.Firing = false;
        }
        _impactGlowAmount = 0f;

        await HidePadsAsync(token);
    }

    private UniTask ShowPadsAsync(CancellationToken token) =>
        TweenAsync(_padAppear, p => { foreach (var rig in _emitters) rig.Visible = p; }, token);

    private UniTask HidePadsAsync(CancellationToken token) =>
        TweenAsync(_padDisappear, p => { foreach (var rig in _emitters) rig.Visible = 1f - p; }, token);

    private void Hit(float scale, int sparks)
    {
        _hitTime = _clock;
        _hitScale = scale;
        if (sparks > 0) EmitSparks(sparks);
        if (_impactFlare != null) _impactFlare.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-25f, 25f));
    }

    private void EmitSparks(int count)
    {
        if (_sparks == null) return;
        var main = _sparks.main;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.Lerp(Color.white, _tint, _sparkTintMix));
        _sparks.Emit(count);
    }

    // ── 매 프레임 감쇠/표시 ─────────────────────────────────

    private void LateUpdate()
    {
        _clock += Delta;
        if (_mpb == null) return;
        if (IsPlaying) _orbitAngle += _orbitSpeed * Delta;

        foreach (var rig in _emitters) UpdateRig(rig);

        float age = _clock - _hitTime;
        float flash = Fade(age, _flashDuration);
        SetQuad(_impactFlash, Color.Lerp(_tint, Color.white, 0.6f), flash,
                _flashScale * _hitScale * Mathf.Lerp(1.5f, 0.7f, Mathf.Clamp01(age / _flashDuration)));

        float ringP = Mathf.Clamp01(age / _ringDuration);
        SetQuad(_impactRing, _tint, Fade(age, _ringDuration),
                _ringScale * _hitScale * Mathf.Lerp(0.3f, _ringExpand, EaseOutQuad(ringP)));

        SetQuad(_impactFlare, Color.white, Fade(age, _flareDuration),
                _flareScale * _hitScale * Mathf.Lerp(1.3f, 0.5f, Mathf.Clamp01(age / _flareDuration)));

        float pulse = 1f + 0.12f * Mathf.Sin(_clock * 45f);
        SetQuad(_impactGlow, _tint, _impactGlowAmount, _impactGlowScale * pulse);
    }

    private void UpdateRig(EmitterRig rig)
    {
        if (rig.Root == null) return;
        float v = rig.Visible;
        float appear = EaseOutBack(v);
        float flash = Fade(_clock - rig.FlashTime, _padFlashDuration);

        Vector3 pos = OrbitOffset(rig) + Vector3.up * ((1f - v) * _padDrop) + _rigBase;
        if (rig.Drone != null) pos += Vector3.up * (_droneBob * Mathf.Sin(_clock * _droneBobSpeed + rig.Offset.x * 2f))
                                    - rig.AimDir * (_droneRecoil * flash);
        rig.Root.localPosition = pos;
        rig.AimDir = (_rigBase - pos).normalized;
        UpdateDepthSorting(rig, pos.z - _rigBase.z);
        UpdateDrone(rig, v, appear);
        SetQuad(rig.Pad, Color.Lerp(_tint, Color.white, 0.25f + flash * 0.6f), v, rig.PadScale * appear);
        SetQuad(rig.PadCore, Color.Lerp(_tint, Color.white, 0.45f + flash * 0.55f), v * (0.7f + 0.3f * flash),
                rig.CoreScale * appear * (1f + flash * 0.35f));
        SetQuad(rig.PadGlow, _tint, v * (0.45f + 0.55f * Mathf.Max(flash, rig.ChargeAmount)),
                rig.GlowScale * appear * (1f + flash * 0.4f + rig.ChargeAmount * 0.3f));

        // Pulse: 번쩍임마다 십자 반짝임 / Sustain: 충전량만큼 빛 구체 + 조여드는 고리
        float charge = Mathf.Max(flash, rig.ChargeAmount * (0.75f + 0.25f * Mathf.Sin(_clock * 50f)));
        if (rig.Drone != null)
        {
            // 드론은 렌즈 앞에서 빛이 모인다
            Vector3 muzzle = rig.AimDir * rig.Muzzle;
            if (rig.Charge != null) rig.Charge.transform.localPosition = muzzle;
            if (rig.ChargeRing != null) rig.ChargeRing.transform.localPosition = muzzle;
        }
        SetQuad(rig.Charge, Color.Lerp(_tint, Color.white, 0.5f), charge * v,
                rig.ChargeScale * (0.4f + 0.6f * Mathf.Max(flash, rig.ChargeAmount)));
        float ringP = rig.ChargeAmount;
        SetQuad(rig.ChargeRing, _tint, (ringP > 0f && ringP < 1f ? Mathf.Sin(ringP * Mathf.PI) : 0f) * v,
                rig.RingScale * Mathf.Lerp(2.4f, 0.5f, ringP));
    }

    // ── 요소 조작 ──────────────────────────────────────────

    private void PlaceRig(Vector3 target)
    {
        _rigBase = transform.InverseTransformPoint(target);
        _rigBase.z = 0f;
        foreach (var rig in _emitters)
        {
            rig.Visible = 0f;
            rig.FlashTime = NeverTime;
            rig.ChargeAmount = 0f;
            rig.Firing = false;
            Vector3 offset = OrbitOffset(rig);
            if (rig.Root != null) rig.Root.localPosition = _rigBase + offset;
            rig.AimDir = (-offset).normalized; // 타겟은 _rigBase, 발사판은 _rigBase + offset
        }
        Transform impact = _impactFlash != null ? _impactFlash.transform.parent : null;
        if (impact != null && impact != transform) impact.position = target;
        if (_sparks != null) _sparks.transform.position = target;
    }

    /// <summary>타겟 기준 발사판 위치 — (Offset.x, Depth)를 수평 원으로 보고 _orbitAngle만큼 돌린다. 높이는 Offset.y.</summary>
    private Vector3 OrbitOffset(EmitterRig rig)
    {
        if (Mathf.Approximately(_orbitAngle, 0f)) return new Vector3(rig.Offset.x, rig.Offset.y, rig.Depth);
        float radius = new Vector2(rig.Offset.x, rig.Depth).magnitude;
        float angle = Mathf.Atan2(rig.Depth, rig.Offset.x) + _orbitAngle * Mathf.Deg2Rad;
        return new Vector3(radius * Mathf.Cos(angle), rig.Offset.y, radius * Mathf.Sin(angle));
    }

    /// <summary>타겟보다 뒤로 가면 드론과 그 번짐/충전 빛을 보스 아래 정렬로 내려 보스에 가려지게 한다. 빔은 FX 그대로.</summary>
    private void UpdateDepthSorting(EmitterRig rig, float depth)
    {
        if (rig.Drone == null) return;
        if (rig.FrontLayer == null)
        {
            rig.FrontLayer = rig.Drone.sortingLayerName;
            rig.FrontOrder = rig.Drone.sortingOrder;
        }
        bool behind = depth > _behindThreshold;
        if (behind == rig.Behind) return;
        rig.Behind = behind;

        string layer = behind ? _behindSortingLayer : rig.FrontLayer;
        SortAs(rig.Drone, layer, behind ? _behindSortingOrder : rig.FrontOrder);
        // 번짐·충전은 드론 바로 아래/위 순서를 유지
        SortAs(rig.PadGlow, layer, (behind ? _behindSortingOrder : rig.FrontOrder) - 1);
        SortAs(rig.ChargeRing, layer, (behind ? _behindSortingOrder : rig.FrontOrder) + 1);
        SortAs(rig.Charge, layer, (behind ? _behindSortingOrder : rig.FrontOrder) + 2);
    }

    private static void SortAs(Renderer r, string layer, int order)
    {
        if (r == null) return;
        r.sortingLayerName = layer;
        r.sortingOrder = order;
    }

    /// <summary>빔 시작점 — 드론이면 렌즈 앞, 아니면 발사판 중심.</summary>
    private static Vector3 MuzzleOf(EmitterRig rig)
    {
        Vector3 p = rig.Root.position;
        return rig.Drone != null ? p + rig.AimDir * rig.Muzzle : p;
    }

    /// <summary>드론 몸체: 렌즈가 타겟을 향하도록 회전, 쏘는 동안 발사 스프라이트, 등장량만큼 투명도·크기.</summary>
    private static void UpdateDrone(EmitterRig rig, float visible, float appear)
    {
        var drone = rig.Drone;
        if (drone == null) return;
        bool on = visible > 0.001f;
        drone.enabled = on;
        if (!on) return;

        float aim = Mathf.Atan2(rig.AimDir.y, rig.AimDir.x) * Mathf.Rad2Deg;
        var rotation = Quaternion.Euler(0f, 0f, aim - rig.DroneFacing);
        var scale = rig.DroneScale * appear;
        var sprite = rig.Firing && rig.DroneFire != null ? rig.DroneFire : rig.DroneIdle;
        if (sprite != null && drone.sprite != sprite) drone.sprite = sprite;

        // 스프라이트 피벗이 바닥 가운데라도 그림 중심이 Root에 오도록 보정한 뒤 회전한다
        Vector3 center = drone.sprite != null ? Vector3.Scale(drone.sprite.bounds.center, scale) : Vector3.zero;
        drone.transform.localRotation = rotation;
        drone.transform.localScale = scale;
        drone.transform.localPosition = -(rotation * center);
        drone.color = new Color(1f, 1f, 1f, Mathf.Clamp01(visible));
    }

    private void SetBeam(Renderer beam, Vector3 from, Vector3 to, float width, float alpha, float seed)
    {
        if (beam == null) return;
        Vector3 d = from - to;
        float len = d.magnitude;
        if (len < 1e-4f) { HideBeam(beam); return; }

        var tr = beam.transform;
        tr.position = (from + to) * 0.5f;
        // 로컬 +Y(uv.y=1) = 발사판 쪽. 빔 축은 그대로 두고 면만 카메라를 향하게 돌린다 (깊이가 있는 빔도 납작해지지 않게)
        Vector3 axis = d / len;
        Vector3 view = _camera != null ? tr.position - _camera.transform.position : Vector3.forward;
        Vector3 facing = Vector3.ProjectOnPlane(view, axis);
        tr.rotation = facing.sqrMagnitude > 1e-6f
            ? Quaternion.LookRotation(facing, axis)
            : Quaternion.FromToRotation(Vector3.up, axis);
        tr.localScale = new Vector3(width, len, 1f);

        _mpb.Clear();
        _mpb.SetColor(ColorId, _tint);
        _mpb.SetFloat(LengthId, len);
        _mpb.SetFloat(AlphaId, Mathf.Clamp01(alpha));
        _mpb.SetFloat(SeedId, seed);
        beam.SetPropertyBlock(_mpb);
        beam.enabled = alpha > 0f && width > 0.0011f;
    }

    private static void HideBeam(Renderer beam)
    {
        if (beam != null) beam.enabled = false;
    }

    private void SetQuad(Renderer quad, Color color, float alpha, Vector3 scale)
    {
        if (quad == null) return;
        bool on = alpha > 0.001f;
        quad.enabled = on;
        if (!on) return;
        quad.transform.localScale = scale;
        color.a = Mathf.Clamp01(alpha);
        _mpb.Clear();
        _mpb.SetColor(ColorId, color);
        quad.SetPropertyBlock(_mpb);
    }

    private void HideAll()
    {
        _mpb ??= new MaterialPropertyBlock();
        _hitTime = NeverTime;
        _impactGlowAmount = 0f;
        foreach (var rig in _emitters)
        {
            rig.Visible = 0f;
            rig.FlashTime = NeverTime;
            rig.ChargeAmount = 0f;
            rig.Firing = false;
            if (rig.Drone != null) rig.Drone.enabled = false;
            Hide(rig.Pad); Hide(rig.PadCore); Hide(rig.PadGlow); Hide(rig.Charge); Hide(rig.ChargeRing); Hide(rig.Beam);
        }
        Hide(_impactFlash); Hide(_impactRing); Hide(_impactFlare); Hide(_impactGlow);
        if (_sparks != null) _sparks.Clear(true);
    }

    private static void Hide(Renderer r)
    {
        if (r != null) r.enabled = false;
    }

    /// <summary>프리팹에 저장된 쿼드 크기를 기준 크기로 기억한다 (재생 중 배율만 곱한다).</summary>
    private void CacheScales()
    {
        if (_cached) return;
        _cached = true;
        foreach (var rig in _emitters)
        {
            rig.PadScale    = ScaleOf(rig.Pad);
            rig.CoreScale   = ScaleOf(rig.PadCore);
            rig.DroneScale  = rig.Drone != null ? rig.Drone.transform.localScale : Vector3.one;
            rig.GlowScale   = ScaleOf(rig.PadGlow);
            rig.ChargeScale = ScaleOf(rig.Charge);
            rig.RingScale   = ScaleOf(rig.ChargeRing);
        }
        _flashScale      = ScaleOf(_impactFlash);
        _ringScale       = ScaleOf(_impactRing);
        _flareScale      = ScaleOf(_impactFlare);
        _impactGlowScale = ScaleOf(_impactGlow);
    }

    private static Vector3 ScaleOf(Renderer r) => r != null ? r.transform.localScale : Vector3.one;

    // ── 시간 유틸 ──────────────────────────────────────────

    private UniTask NextFrame(CancellationToken token) => UniTask.Yield(PlayerLoopTiming.Update, token);

    private async UniTask WaitAsync(float seconds, CancellationToken token)
    {
        float t = 0f;
        while (t < seconds)
        {
            await NextFrame(token);
            t += Delta;
        }
    }

    private async UniTask TweenAsync(float duration, Action<float> step, CancellationToken token)
    {
        float t = 0f;
        while (t < duration)
        {
            step(Mathf.Clamp01(t / duration));
            await NextFrame(token);
            t += Delta;
        }
        step(1f);
    }

    /// <summary>나이 age가 duration 동안 1 → 0으로 빠르게 꺼지는 곡선 (시작 전·끝난 뒤 0).</summary>
    private static float Fade(float age, float duration)
    {
        if (age < 0f || age >= duration) return 0f;
        float p = 1f - age / duration;
        return p * p;
    }

    private static float EaseOutQuad(float p) => 1f - (1f - p) * (1f - p);
    private static float EaseInQuad(float p) => p * p;

    private static float EaseOutBack(float p)
    {
        const float s = 1.70158f;
        float q = p - 1f;
        return 1f + q * q * ((s + 1f) * q + s);
    }
}
