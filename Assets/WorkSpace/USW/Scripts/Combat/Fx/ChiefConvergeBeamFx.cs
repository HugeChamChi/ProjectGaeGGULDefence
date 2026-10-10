using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 족장 스킬(알팡 집결 일제 사격) 수렴 빔 연출 — 레퍼런스 design/엥이런드론연출이있었다고.gif, ~2.png.
///   ① 충전: 드론마다 총구에 빛 구슬이 차오르고, 가는 조준선이 대형 앞의 합류점으로 모인다.
///   ② 발사: 합류점에서 보스까지 굵은 주황·흰 빔이 터져 나가고, 드론 빔들도 굵어져 합류점으로 꽂힌다 (부채꼴 빔 다발).
///   ③ 유지: 빔이 살짝 일렁이고 굵기가 흔들리며 착탄점에 섬광·고리·불꽃이 튄다.
///   ④ 소멸: 빔이 가늘어지며 사라진다.
/// 빔은 리본 메시(LaserBeam 셰이더: uv.x = 폭, uv.y = 길이, 1 = 출발점). 기본은 곧은 빔이고(사용자 선호),
/// _feedCurve·_feedTipRatio로 휘거나 끝을 가늘게 할 수 있다. 합류점은 십자 섬광 대신 둥근 빛만 쓴다 (사용자 피드백 2026-10-11).
/// 실험실(ChiefConvergeBeamLab)에서 다듬고, 게임에서는 DroneManager 집결 사격이 Play를 부르면 된다.
/// </summary>
public sealed class ChiefConvergeBeamFx : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int LengthId = Shader.PropertyToID("_Length");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int CoreWidthId = Shader.PropertyToID("_CoreWidth");
    private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");
    private static readonly int GlowOpacityId = Shader.PropertyToID("_GlowOpacity");
    private static readonly int TipSoftId = Shader.PropertyToID("_TipSoft");
    private const string FxLayer = "FX";
    private const int MaxDrones = 24;
    private const int SparkCount = 32;
    private const int RingCount = 4;
    private const int FeedSegments = 16;
    private const int MainSegments = 24;

    [Header("머티리얼 (SkyLaser 공용)")]
    [SerializeField] private Material _beamMaterial;
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Material _flareMaterial;
    [SerializeField] private Material _ringMaterial;
    [SerializeField] private Material _pointMaterial;

    [Header("색")]
    [ColorUsage(true, true)] [SerializeField] private Color _hotColor = new Color(2.7f, 0.7f, 0.12f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color _chargeColor = new Color(1.8f, 0.55f, 0.12f, 1f);
    [SerializeField] private Color _sparkColor = new Color(1f, 0.8f, 0.4f, 1f);

    [Header("시간 (초)")]
    [SerializeField, Min(0.05f)] private float _charge = 0.6f;
    [SerializeField, Min(0.05f)] private float _hold = 0.9f;
    [SerializeField, Min(0.05f)] private float _fade = 0.35f;
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("모양 (월드 단위)")]
    [Tooltip("대형 중심에서 보스 쪽으로 합류점까지 거리")]
    [SerializeField, Min(0f)] private float _mergeDistance = 1.5f;
    [SerializeField, Min(0f)] private float _mainWidth = 1.15f;
    [Tooltip("굵은 빔이 합류점 쪽에서 얼마나 더 굵은지 (보스 쪽 = 1)")]
    [SerializeField, Min(0.5f)] private float _mainBaseFlare = 1f;
    [Tooltip("굵은 빔 바깥 은은한 번짐 — 폭 배율 / 밝기")]
    [SerializeField, Min(1f)] private float _haloScale = 2.2f;
    [SerializeField, Range(0f, 1f)] private float _haloAlpha = 0.45f;
    [Tooltip("발사 순간 굵기 배율 (넘쳤다 돌아옴)")]
    [SerializeField, Min(1f)] private float _popScale = 1.45f;
    [SerializeField, Min(0f)] private float _aimWidth = 0.07f;
    [Tooltip("드론 빔 굵기 (합류점 쪽). 드론 쪽은 _feedTipRatio만큼 가늘다")]
    [SerializeField, Min(0f)] private float _feedWidth = 0.38f;
    [SerializeField, Range(0.1f, 1f)] private float _feedTipRatio = 1f;
    [Tooltip("드론 빔 휘는 정도 — 0 = 곧은 빔(기본, 사용자 선호). 클수록 합류점에 빔 방향으로 휘어 들어간다")]
    [SerializeField, Range(0f, 1f)] private float _feedCurve = 0f;
    [SerializeField, Min(0f)] private float _orbSize = 0.55f;
    [SerializeField, Min(0f)] private float _mergeFlareSize = 1.6f;
    [SerializeField, Min(0f)] private float _impactFlareSize = 2.6f;
    [Tooltip("유지 중 굵기 흔들림 (비율)")]
    [SerializeField, Range(0f, 0.5f)] private float _wobble = 0.12f;
    [Tooltip("유지 중 굵은 빔이 옆으로 일렁이는 폭 (양 끝은 고정)")]
    [SerializeField, Min(0f)] private float _waveAmplitude = 0.06f;
    [SerializeField, Min(0f)] private float _sparkSpeed = 4.5f;
    [Tooltip("드론이 이 수를 넘으면 드론 빔·빛 구슬을 가늘게 한다 (16기 이상에서 한 덩어리로 뭉개지지 않게)")]
    [SerializeField, Min(1)] private int _crowdStart = 8;
    [Tooltip("드론 수가 _crowdStart의 2배일 때 드론 빔·구슬 굵기 배율")]
    [SerializeField, Range(0.3f, 1f)] private float _crowdThin = 0.7f;

    [Header("부드러움 (LaserBeam 셰이더 값 덮기)")]
    [Tooltip("하얀 심지 폭 (0~1). 작을수록 가운데만 하얗고 가장자리는 색 번짐")]
    [SerializeField, Range(0.01f, 1f)] private float _feedCore = 0.32f;
    [SerializeField, Range(0.01f, 1f)] private float _mainCore = 0.32f;
    [Tooltip("번짐 감쇠 (작을수록 가장자리가 넓게 퍼진다)")]
    [SerializeField, Range(0.5f, 8f)] private float _glowPower = 1.6f;
    [Tooltip("번짐이 배경을 덮는 정도 (0 = 순수 더하기). 높으면 빔 가장자리에 탁한 띠가 생긴다")]
    [SerializeField, Range(0f, 1f)] private float _glowOpacity = 0.75f;
    [Tooltip("빔 끝을 흐리는 길이 (월드)")]
    [SerializeField, Min(0f)] private float _tipSoft = 0.25f;

    [Header("화면 흔들림")]
    [SerializeField, Min(0f)] private float _shake = 0.12f;
    [SerializeField, Min(0f)] private float _shakeSeconds = 0.35f;

    private readonly List<Transform> _muzzles = new List<Transform>();
    private Transform _target;
    private float _scale = 1f;
    private float _t = -1f;
    private float _seed;
    private bool _fired;
    private float _holdExtra;
    private float _popAt;
    private Ribbon _main, _halo;
    private Ribbon[] _feeds;
    private Renderer _mergeFlare, _mergeGlow, _impactFlare;
    private Renderer[] _orbs, _rings, _sparks;
    private Vector3[] _sparkVel;
    private float[] _sparkBorn, _ringBorn;
    private int _nextSpark, _nextRing;
    private float _lastRingAt, _lastSparkAt;
    private MaterialPropertyBlock _mpb;
    private Transform _cam;
    private Vector3 _camHome;
    private bool _built;

    /// <summary>재생 중이면 true.</summary>
    public bool IsPlaying => _t >= 0f;
    /// <summary>발사(굵은 빔)가 시작되기까지 걸리는 시간.</summary>
    public float ChargeSeconds => _charge;
    /// <summary>처음부터 끝까지 걸리는 시간.</summary>
    public float TotalSeconds => _charge + _hold + _holdExtra + _fade;
    /// <summary>scaled/unscaled 시계 선택 (실험실 고정 프레임 캡처용).</summary>
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }
    /// <summary>굵은 빔이 터지는 순간 (피해 적용 시점으로 쓸 수 있다).</summary>
    public event Action Fired;

    /// <summary>총구들에서 target으로 수렴 빔을 처음부터 재생한다. 총구는 재생 중 움직여도 따라간다.</summary>
    public void Play(IReadOnlyList<Transform> muzzles, Transform target, float scale = 1f)
    {
        Build();
        bool shaking = IsPlaying && _t >= _charge && _t - _charge <= _shakeSeconds; // 흔드는 중이면 카메라 원위치를 다시 잡지 않는다
        _muzzles.Clear();
        if (muzzles != null)
            foreach (var m in muzzles) if (m != null && _muzzles.Count < MaxDrones) _muzzles.Add(m);
        _target = target;
        _scale = Mathf.Max(0.05f, scale);
        _seed = UnityEngine.Random.value * 100f;
        _t = 0f;
        _fired = false;
        _holdExtra = 0f;
        _popAt = _charge;
        _lastRingAt = _lastSparkAt = -1f;
        var cam = Camera.main;
        if (cam != null && (!shaking || _cam != cam.transform)) { _cam = cam.transform; _camHome = _cam.position; }
    }

    /// <summary>
    /// 발사 중 한 번 더 때리는 순간(족장 연속 사격 등) — 빔이 다시 넘치고 착탄점에 고리·불꽃이 튄다.
    /// 빔이 이 시점부터 최소 <paramref name="keepSeconds"/>초는 더 유지되도록 늘린다. 발사 전이나 재생 중이 아니면 무시한다.
    /// </summary>
    public void Pulse(float keepSeconds = 0.35f)
    {
        if (_t < _charge || !_fired) return;
        _popAt = _t;
        _holdExtra = Mathf.Max(_holdExtra, _t + keepSeconds - _charge - _hold);
        if (_target != null) for (int i = 0; i < 8; i++) SpawnSpark(_target.position, Vector3.zero);
        SpawnRing();
    }

    /// <summary>즉시 멈추고 숨긴다.</summary>
    public void Stop()
    {
        _t = -1f;
        HideAll();
        if (_cam != null) _cam.position = _camHome;
    }

    private void Update()
    {
        if (_t < 0f) return;
        if (_target == null || _muzzles.Count == 0) { Stop(); return; }
        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _t += dt;
        if (_t >= TotalSeconds) { Stop(); return; }

        Vector3 target = _target.position;
        Vector3 centroid = Vector3.zero;
        foreach (var m in _muzzles) centroid += m.position;
        centroid /= _muzzles.Count;
        Vector3 toTarget = target - centroid;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 1e-4f ? toTarget / dist : Vector3.up;
        Vector3 merge = centroid + dir * Mathf.Min(_mergeDistance * _scale, dist * 0.45f);

        bool firing = _t >= _charge;
        if (firing && !_fired) { _fired = true; OnFire(target); }
        float crowd = Mathf.Lerp(1f, _crowdThin, Mathf.Clamp01((_muzzles.Count - _crowdStart) / (float)_crowdStart));
        float chargeP = Mathf.Clamp01(_t / _charge);
        float fadeP = Mathf.Clamp01((_t - _charge - _hold - _holdExtra) / _fade);
        float life = 1f - fadeP * fadeP;
        float since = _t - _charge;

        // ① ② 드론 쪽: 총구 빛 구슬 + 휘어 들어가는 드론 빔
        for (int i = 0; i < _feeds.Length; i++)
        {
            if (i >= _muzzles.Count) { Hide(_orbs[i]); _feeds[i].Hide(); continue; }
            Vector3 muzzle = _muzzles[i].position;
            float flicker = 0.85f + 0.15f * Mathf.Sin((_t * 40f) + i * 1.7f);
            float orb = firing ? Mathf.Lerp(1.2f, 0.7f, Mathf.Clamp01(since / 0.25f)) * life : EaseOut(chargeP);
            SetQuad(_orbs[i], muzzle, _chargeColor, orb * flicker, Vector3.one * (_orbSize * crowd * _scale * (0.4f + 0.6f * orb)));

            // 제어점을 합류점 뒤(대형 쪽)에 두어 빔 방향으로 들어가게 한다 → 손가락처럼 휘어 모인다.
            float reach = (merge - muzzle).magnitude;
            Vector3 control = merge - dir * reach * _feedCurve;
            float tipW, baseW, alpha;
            if (firing)
            {
                float grow = Mathf.Clamp01(since / 0.15f);
                baseW = _feedWidth * _scale * crowd * life * (1f + _wobble * Noise(i)) * Mathf.Lerp(0.4f, 1f, grow);
                tipW = baseW * _feedTipRatio;
                alpha = life;
            }
            else
            {
                baseW = tipW = _aimWidth * _scale * crowd * (0.4f + 0.6f * chargeP);
                alpha = (0.3f + 0.5f * chargeP) * flicker;
            }
            _feeds[i].SetCurve(muzzle, control, merge, tipW, baseW);
            Draw(_feeds[i], alpha, firing ? _hotColor : _chargeColor, _feedCore, i * 0.37f);
        }

        // 합류점: 드론 빔과 굵은 빔이 녹아드는 큰 빛 (이음매를 덮는다)
        float mergeSize = firing ? _mergeFlareSize * _scale * (1f + 0.15f * Noise(17)) * life : _mergeFlareSize * 0.45f * _scale * EaseOut(chargeP);
        SetQuad(_mergeGlow, merge, _chargeColor, firing ? 0.7f * life : 0.4f * chargeP, Vector3.one * mergeSize * 1.8f);
        SetQuad(_mergeFlare, merge, _hotColor, firing ? life : chargeP * 0.8f, Vector3.one * mergeSize);

        // ② ③ ④ 굵은 빔 + 착탄
        if (firing)
        {
            float sincePop = _t - _popAt;
            float popFrom = _popAt > _charge ? 1f : 0.2f; // 첫 발사는 가늘게 시작, 연속 사격은 지금 굵기에서 다시 넘친다
            float pop = sincePop < 0.12f ? Mathf.Lerp(popFrom, _popScale, sincePop / 0.12f)
                      : Mathf.Lerp(_popScale, 1f, Mathf.Clamp01((sincePop - 0.12f) / 0.2f));
            float width = _mainWidth * _scale * pop * (1f + _wobble * Noise(3)) * life;
            float wave = _waveAmplitude * _scale * life;
            float phase = since * 7f + _seed;
            Vector3 start = merge - dir * (0.2f * _scale); // 합류점 빛 아래에서 시작해 이음매를 숨긴다
            _halo.SetLine(start, target, width * _haloScale * _mainBaseFlare, width * _haloScale, wave, phase);
            Draw(_halo, _haloAlpha * life, _hotColor, 0.02f, 0.5f);
            _main.SetLine(start, target, width * _mainBaseFlare, width, wave, phase);
            Draw(_main, life, _hotColor, _mainCore, 0f);
            SetQuad(_impactFlare, target, _hotColor, life, Vector3.one * (_impactFlareSize * _scale * (0.85f + 0.3f * Noise(9))));

            if (since - _lastRingAt >= 0.16f && fadeP <= 0f) { _lastRingAt = since; SpawnRing(); }
            if (since - _lastSparkAt >= 0.03f && fadeP <= 0f) { _lastSparkAt = since; SpawnSpark(target, dir); SpawnSpark(target, dir); }
            UpdateRings(target);
            Shake(since);
        }
        else
        {
            _main.Hide(); _halo.Hide(); Hide(_impactFlare);
        }
        UpdateSparks(dt);
    }

    private void OnFire(Vector3 target)
    {
        Fired?.Invoke();
        for (int i = 0; i < 12; i++) SpawnSpark(target, Vector3.zero);
        SpawnRing();
    }

    private void SpawnRing()
    {
        _ringBorn[_nextRing] = _t;
        _nextRing = (_nextRing + 1) % _rings.Length;
    }

    private void UpdateRings(Vector3 at)
    {
        for (int i = 0; i < _rings.Length; i++)
        {
            float age = _t - _ringBorn[i];
            if (_ringBorn[i] < 0f || age > 0.4f) { Hide(_rings[i]); continue; }
            float p = age / 0.4f;
            SetQuad(_rings[i], at, _hotColor, 1f - p, Vector3.one * (_impactFlareSize * 0.6f * _scale * (0.3f + 1.4f * EaseOut(p))));
        }
    }

    private void SpawnSpark(Vector3 at, Vector3 against)
    {
        int i = _nextSpark;
        _nextSpark = (_nextSpark + 1) % _sparks.Length;
        Vector2 r = UnityEngine.Random.insideUnitCircle.normalized;
        Vector3 v = new Vector3(r.x, r.y, 0f) * UnityEngine.Random.Range(0.4f, 1f) * _sparkSpeed * _scale;
        v -= against * _sparkSpeed * 0.3f * _scale; // 빔 반대쪽으로 더 튄다
        _sparkVel[i] = v;
        _sparkBorn[i] = _t;
        _sparks[i].transform.position = at;
    }

    private void UpdateSparks(float dt)
    {
        for (int i = 0; i < _sparks.Length; i++)
        {
            float age = _t - _sparkBorn[i];
            if (_sparkBorn[i] < 0f || age > 0.45f) { Hide(_sparks[i]); continue; }
            var tr = _sparks[i].transform;
            _sparkVel[i] *= Mathf.Max(0f, 1f - 3f * dt);
            tr.position += _sparkVel[i] * dt;
            float p = age / 0.45f;
            SetQuad(_sparks[i], tr.position, _sparkColor, 1f - p, Vector3.one * (0.16f * _scale * (1f - 0.6f * p)));
        }
    }

    private void Shake(float since)
    {
        if (_cam == null || _shake <= 0f) return;
        if (since > _shakeSeconds) { _cam.position = _camHome; return; }
        float k = 1f - since / _shakeSeconds;
        Vector2 r = UnityEngine.Random.insideUnitCircle * (_shake * k);
        _cam.position = _camHome + new Vector3(r.x, r.y, 0f);
    }

    private float Noise(int lane) => Mathf.PerlinNoise(_t * 14f + lane * 3.1f, _seed) * 2f - 1f;
    private static float EaseOut(float p) => 1f - (1f - p) * (1f - p);

    private void Draw(Ribbon ribbon, float alpha, Color color, float core, float seedOffset)
    {
        if (alpha <= 0.001f || ribbon.Length < 1e-4f) { ribbon.Hide(); return; }
        _mpb.Clear();
        _mpb.SetColor(ColorId, color);
        _mpb.SetFloat(LengthId, ribbon.Length);
        _mpb.SetFloat(AlphaId, Mathf.Clamp01(alpha));
        _mpb.SetFloat(SeedId, _seed + seedOffset);
        _mpb.SetFloat(CoreWidthId, core);
        _mpb.SetFloat(GlowPowerId, _glowPower);
        _mpb.SetFloat(GlowOpacityId, _glowOpacity);
        _mpb.SetFloat(TipSoftId, _tipSoft * _scale);
        ribbon.Renderer.SetPropertyBlock(_mpb);
        ribbon.Renderer.enabled = true;
    }

    private void Build()
    {
        if (_built) return;
        _built = true;
        _mpb = new MaterialPropertyBlock();
        _halo = new Ribbon(transform, "MainHalo", _beamMaterial, 39, MainSegments);
        _main = new Ribbon(transform, "Main", _beamMaterial, 42, MainSegments);
        _mergeGlow = Quad("MergeGlow", _glowMaterial, 43);
        _mergeFlare = Quad("MergeCore", _glowMaterial, 44); // 십자 섬광(Flare) 대신 둥근 빛 — 합류점에 십자가가 서 보이지 않게 (사용자 피드백 2026-10-11)
        _impactFlare = Quad("ImpactFlare", _flareMaterial, 44);
        _feeds = new Ribbon[MaxDrones];
        _orbs = new Renderer[MaxDrones];
        for (int i = 0; i < MaxDrones; i++)
        {
            _feeds[i] = new Ribbon(transform, $"Feed_{i}", _beamMaterial, 41, FeedSegments);
            _orbs[i] = Quad($"Orb_{i}", _glowMaterial, 43);
        }
        _rings = new Renderer[RingCount];
        _ringBorn = new float[RingCount];
        for (int i = 0; i < RingCount; i++) { _rings[i] = Quad($"Ring_{i}", _ringMaterial, 45); _ringBorn[i] = -1f; }
        _sparks = new Renderer[SparkCount];
        _sparkVel = new Vector3[SparkCount];
        _sparkBorn = new float[SparkCount];
        for (int i = 0; i < SparkCount; i++) { _sparks[i] = Quad($"Spark_{i}", _pointMaterial, 46); _sparkBorn[i] = -1f; }
    }

    private void HideAll()
    {
        if (!_built) return;
        _main.Hide(); _halo.Hide();
        Hide(_mergeFlare); Hide(_mergeGlow); Hide(_impactFlare);
        foreach (var r in _feeds) r.Hide();
        foreach (var r in _orbs) Hide(r);
        foreach (var r in _rings) Hide(r);
        foreach (var r in _sparks) Hide(r);
        for (int i = 0; i < _sparkBorn.Length; i++) _sparkBorn[i] = -1f;
        for (int i = 0; i < _ringBorn.Length; i++) _ringBorn[i] = -1f;
    }

    private void OnDisable() => Stop();

    private void OnDestroy()
    {
        if (!_built) return;
        _main.Dispose(); _halo.Dispose();
        foreach (var r in _feeds) r.Dispose();
    }

    private Renderer Quad(string name, Material mat, int order)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingLayerName = FxLayer;
        r.sortingOrder = order;
        r.enabled = false;
        return r;
    }

    private void SetQuad(Renderer quad, Vector3 at, Color color, float alpha, Vector3 scale)
    {
        if (alpha <= 0.001f || scale.x <= 0.0005f) { Hide(quad); return; }
        var tr = quad.transform;
        tr.position = at;
        tr.rotation = Quaternion.identity;
        tr.localScale = scale;
        color.a = Mathf.Clamp01(alpha);
        _mpb.Clear();
        _mpb.SetColor(ColorId, color);
        quad.SetPropertyBlock(_mpb);
        quad.enabled = true;
    }

    private static void Hide(Renderer r)
    {
        if (r != null) r.enabled = false;
    }

    /// <summary>
    /// 곡선 띠 메시 — 월드 좌표로 모양을 계산한 뒤 자기 로컬로 바꿔 넣는다 (부모가 움직여도 맞다).
    /// uv.x = 폭(0~1), uv.y = 길이(1 = 출발점, 0 = 도착점) — LaserBeam 셰이더 규약.
    /// </summary>
    private sealed class Ribbon
    {
        public readonly MeshRenderer Renderer;
        public float Length { get; private set; }
        private readonly Transform _transform;
        private readonly Mesh _mesh;
        private readonly Vector3[] _vertices;
        private readonly Vector2[] _uvs;
        private readonly Vector3[] _points;
        private readonly float[] _along;
        private readonly int _segments;

        public Ribbon(Transform parent, string name, Material material, int order, int segments)
        {
            _segments = segments;
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            _transform = go.transform;
            _transform.SetParent(parent, false);
            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            _vertices = new Vector3[(segments + 1) * 2];
            _uvs = new Vector2[_vertices.Length];
            _points = new Vector3[segments + 1];
            _along = new float[segments + 1];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.triangles = triangles;
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            Renderer = go.GetComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Renderer.sortingLayerName = FxLayer;
            Renderer.sortingOrder = order;
            Renderer.enabled = false;
        }

        /// <summary>2차 베지어 곡선 (출발 a → 제어 c → 도착 b). 굵기는 출발 widthA → 도착 widthB.</summary>
        public void SetCurve(Vector3 a, Vector3 c, Vector3 b, float widthA, float widthB)
        {
            for (int i = 0; i <= _segments; i++)
            {
                float t = i / (float)_segments;
                float u = 1f - t;
                _points[i] = u * u * a + 2f * u * t * c + t * t * b;
            }
            Fill(widthA, widthB, 0f, 0f);
        }

        /// <summary>직선 (a → b) + 옆으로 일렁임 (양 끝은 고정).</summary>
        public void SetLine(Vector3 a, Vector3 b, float widthA, float widthB, float wave, float phase)
        {
            for (int i = 0; i <= _segments; i++)
                _points[i] = Vector3.Lerp(a, b, i / (float)_segments);
            Fill(widthA, widthB, wave, phase);
        }

        private void Fill(float widthA, float widthB, float wave, float phase)
        {
            float total = 0f;
            _along[0] = 0f;
            for (int i = 1; i <= _segments; i++)
            {
                total += Vector3.Distance(_points[i - 1], _points[i]);
                _along[i] = total;
            }
            Length = total;
            if (total < 1e-4f) return;
            for (int i = 0; i <= _segments; i++)
            {
                Vector3 tangent = (i < _segments ? _points[i + 1] - _points[i] : _points[i] - _points[i - 1]).normalized;
                Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
                float t = _along[i] / total;
                float envelope = 4f * t * (1f - t);
                Vector3 p = _points[i] + normal * (wave * envelope * Mathf.Sin(phase + t * 9f));
                float half = Mathf.Lerp(widthA, widthB, t) * 0.5f;
                _vertices[i * 2] = _transform.InverseTransformPoint(p - normal * half);
                _vertices[i * 2 + 1] = _transform.InverseTransformPoint(p + normal * half);
                float v = 1f - t; // 출발점 = 1, 도착점 = 0
                _uvs[i * 2] = new Vector2(0f, v);
                _uvs[i * 2 + 1] = new Vector2(1f, v);
            }
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.RecalculateBounds();
        }

        public void Hide() => Renderer.enabled = false;

        public void Dispose()
        {
            if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
        }
    }
}
