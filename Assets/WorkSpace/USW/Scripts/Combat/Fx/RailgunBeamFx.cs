using UnityEngine;

/// <summary>
/// 레일건 한 발의 시각 연출 (임시 — 디시그망). 차지(빛이 모이고 조준선이 깜빡임) → 순간 빔 → 착탄 섬광 순서.
/// RailgunProjectile이 Play()로 시작시키며, 시작과 동시에 투사체에서 떨어져 나와 혼자 끝까지 재생한 뒤 스스로 파괴된다
/// (투사체는 적중 즉시 파괴되므로 빔 잔광을 남기려면 분리해야 한다). 차지 도중 투사체가 사라지면 발사하지 않고 정리한다.
/// 쿼드는 Awake에서 내장 Quad 메시로 만들고 머티리얼은 하늘 레이저(SkyLaser) 공용 머티리얼을 그대로 쓴다.
/// 시간은 scaled — 인게임 일시정지 시 함께 멈춘다.
/// </summary>
public class RailgunBeamFx : MonoBehaviour
{
    private static readonly int ColorId  = Shader.PropertyToID("_Color");
    private static readonly int LengthId = Shader.PropertyToID("_Length");
    private static readonly int AlphaId  = Shader.PropertyToID("_Alpha");
    private static readonly int SeedId   = Shader.PropertyToID("_Seed");

    private const string FxLayer = "FX";
    private const int OrderAim = 30, OrderBeam = 31, OrderGlow = 32, OrderRing = 33, OrderCore = 34, OrderFlash = 35, OrderFlare = 36;

    [Header("머티리얼 (SkyLaser 공용)")]
    [SerializeField] private Material _beamMaterial;
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Material _pointMaterial;
    [SerializeField] private Material _ringMaterial;
    [SerializeField] private Material _flareMaterial;

    [Header("색")]
    [Tooltip("빔 번짐·충전 빛 색. 심지와 섬광 중심은 흰색")]
    [SerializeField] private Color _tint = new Color(0.3f, 0.5f, 1f, 1f);

    [Header("차지")]
    [Tooltip("총구 번짐 최대 크기 (월드)")]
    [SerializeField] private float _chargeGlowSize = 1.7f;
    [Tooltip("총구 중심 빛 최대 크기 (월드)")]
    [SerializeField] private float _chargeCoreSize = 0.65f;
    [Tooltip("조여드는 고리 시작 크기 → 끝 크기 (월드)")]
    [SerializeField] private Vector2 _chargeRingSize = new Vector2(2.2f, 0.3f);
    [Tooltip("조준선 굵기 (월드)")]
    [SerializeField] private float _aimWidth = 0.1f;
    [Tooltip("조준선 최대 밝기 (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _aimAlpha = 0.75f;
    [Tooltip("조준선 깜빡임 속도")]
    [SerializeField] private float _aimFlickerSpeed = 40f;

    [Header("발사")]
    [Tooltip("빔 굵기 (월드)")]
    [SerializeField] private float _beamWidth = 0.42f;
    [Tooltip("발사 순간 굵기 배율 (점점 원래 굵기 → 0으로)")]
    [SerializeField] private float _beamPopScale = 1.8f;
    [Tooltip("최대 굵기 유지 시간")]
    [SerializeField] private float _beamHold = 0.05f;
    [Tooltip("빔이 가늘어지며 꺼지는 시간")]
    [SerializeField] private float _beamFade = 0.2f;
    [Tooltip("총구/착탄 섬광 크기 (월드)")]
    [SerializeField] private float _flashSize = 1.3f;
    [Tooltip("착탄 고리 최대 크기 (월드)")]
    [SerializeField] private float _impactRingSize = 1.8f;
    [Tooltip("섬광이 사그라드는 시간")]
    [SerializeField] private float _flashFade = 0.25f;

    private Renderer _aim, _beam, _chargeGlow, _chargeCore, _chargeRing, _muzzleFlash, _impactFlash, _impactRing, _impactFlare;
    private MaterialPropertyBlock _mpb;
    private Transform _owner;
    private Vector3 _from, _to;
    private float _scale = 1f, _chargeSeconds, _time, _seed;
    private bool _playing, _fired;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _aim         = Quad("Aim", _beamMaterial, OrderAim);
        _beam        = Quad("Beam", _beamMaterial, OrderBeam);
        _chargeGlow  = Quad("ChargeGlow", _glowMaterial, OrderGlow);
        _chargeRing  = Quad("ChargeRing", _ringMaterial, OrderRing);
        _chargeCore  = Quad("ChargeCore", _pointMaterial, OrderCore);
        _muzzleFlash = Quad("MuzzleFlash", _flareMaterial, OrderFlare);
        _impactRing  = Quad("ImpactRing", _ringMaterial, OrderRing);
        _impactFlash = Quad("ImpactFlash", _pointMaterial, OrderFlash);
        _impactFlare = Quad("ImpactFlare", _flareMaterial, OrderFlare);
    }

    /// <summary>from(총구) → to(착탄점) 레일건 연출을 시작한다. chargeSeconds 뒤에 빔이 나간다.
    /// owner가 차지 도중 사라지면 발사하지 않는다. scale은 굵기·섬광 크기 배율.</summary>
    public void Play(Transform owner, Vector3 from, Vector3 to, float chargeSeconds, float scale)
    {
        _owner = owner;
        _from = from;
        _to = to;
        _chargeSeconds = Mathf.Max(0f, chargeSeconds);
        _scale = Mathf.Max(0.01f, scale);
        _seed = Random.value * 10f;
        _time = 0f;
        _fired = false;
        _playing = true;

        // 투사체 크기 배율과 수명에서 분리 — 이후 위치·크기는 월드 값 그대로 쓴다.
        transform.SetParent(owner != null ? owner.parent : null, false);
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        _chargeGlow.transform.position = _chargeCore.transform.position = _chargeRing.transform.position = from;
        _muzzleFlash.transform.position = from;
        _impactFlash.transform.position = _impactRing.transform.position = _impactFlare.transform.position = to;
        _impactFlare.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
        Tick();
    }

    private void Update()
    {
        if (!_playing) return;
        _time += Time.deltaTime;
        Tick();
    }

    private void Tick()
    {
        if (!_fired && _owner == null)
        {
            Destroy(gameObject); // 차지 중 투사체가 사라짐 (씬 종료·풀 정리 등) — 쏘지 않는다
            return;
        }

        if (_time < _chargeSeconds)
        {
            DrawCharge(_chargeSeconds > 0f ? _time / _chargeSeconds : 1f);
            return;
        }

        if (!_fired)
        {
            _fired = true;
            Hide(_aim); Hide(_chargeRing);
        }

        float t = _time - _chargeSeconds;
        if (!DrawFire(t)) Destroy(gameObject);
    }

    private void DrawCharge(float p)
    {
        float grow = p * p;
        float flicker = 0.75f + 0.25f * Mathf.Sin(_time * _aimFlickerSpeed + _seed);

        SetQuad(_chargeGlow, _tint, grow, Vector3.one * (_chargeGlowSize * _scale * Mathf.Lerp(0.3f, 1f, grow)));
        SetQuad(_chargeCore, Color.Lerp(_tint, Color.white, p), Mathf.Lerp(0.2f, 1f, grow) * flicker,
            Vector3.one * (_chargeCoreSize * _scale * Mathf.Lerp(0.4f, 1f, grow)));
        SetQuad(_chargeRing, _tint, Mathf.Sin(p * Mathf.PI) * 0.9f,
            Vector3.one * (Mathf.Lerp(_chargeRingSize.x, _chargeRingSize.y, p * (2f - p)) * _scale));
        // 조준선: 뒤로 갈수록 또렷해지고 빠르게 떨린다 — "곧 쏜다" 예고
        SetBeam(_aim, _from, _to, _aimWidth * _scale * Mathf.Lerp(0.5f, 1f, p), _aimAlpha * grow * flicker);
    }

    // 발사 후 경과 t초 그리기. 모두 꺼졌으면 false.
    private bool DrawFire(float t)
    {
        float width = _beamWidth * _scale;
        bool beamOn = t < _beamHold + _beamFade;
        if (beamOn)
        {
            if (t < _beamHold)
                SetBeam(_beam, _from, _to, width * Mathf.Lerp(_beamPopScale, 1.2f, t / _beamHold), 1f);
            else
            {
                float p = (t - _beamHold) / _beamFade;
                SetBeam(_beam, _from, _to, width * 1.2f * (1f - p * p) + 0.001f, 1f - p * p);
            }
        }
        else Hide(_beam);

        float f = Mathf.Clamp01(t / _flashFade);
        float fade = 1f - f;
        bool flashOn = f < 1f;
        float flash = _flashSize * _scale;

        // 총구: 남은 충전 빛이 섬광으로 터지며 사그라든다
        SetQuad(_chargeGlow, _tint, fade * fade * 0.9f, Vector3.one * (_chargeGlowSize * _scale * (1f + f * 0.4f)));
        SetQuad(_chargeCore, Color.white, fade * fade, Vector3.one * (_chargeCoreSize * _scale * (1f + f)));
        SetQuad(_muzzleFlash, _tint, fade * fade, new Vector3(flash * 1.3f, flash * 0.6f, 1f) * (1f + f * 0.3f));

        SetQuad(_impactFlash, Color.Lerp(Color.white, _tint, f), fade * fade, Vector3.one * (flash * (1f + f * 0.5f)));
        SetQuad(_impactFlare, _tint, fade, new Vector3(flash * 2.2f, flash * 1.4f, 1f) * (1f - f * 0.3f));
        SetQuad(_impactRing, _tint, fade * 0.9f, new Vector3(1f, 0.55f, 1f) * (_impactRingSize * _scale * Mathf.Lerp(0.3f, 1f, 1f - fade * fade)));

        return beamOn || flashOn;
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

    private void SetBeam(Renderer beam, Vector3 from, Vector3 to, float width, float alpha)
    {
        Vector3 d = from - to;
        float len = d.magnitude;
        if (len < 1e-4f || alpha <= 0.001f) { Hide(beam); return; }

        // 로컬 +Y(uv.y=1) = 총구 쪽, uv.y=0 = 착탄점 (LaserBeam 셰이더 규약)
        var tr = beam.transform;
        tr.position = (from + to) * 0.5f;
        tr.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
        tr.localScale = new Vector3(width, len, 1f);

        _mpb.Clear();
        _mpb.SetColor(ColorId, _tint);
        _mpb.SetFloat(LengthId, len);
        _mpb.SetFloat(AlphaId, Mathf.Clamp01(alpha));
        _mpb.SetFloat(SeedId, _seed);
        beam.SetPropertyBlock(_mpb);
        beam.enabled = true;
    }

    private void SetQuad(Renderer quad, Color color, float alpha, Vector3 scale)
    {
        bool on = alpha > 0.001f;
        quad.enabled = on;
        if (!on) return;
        quad.transform.localScale = scale;
        color.a = Mathf.Clamp01(alpha);
        _mpb.Clear();
        _mpb.SetColor(ColorId, color);
        quad.SetPropertyBlock(_mpb);
    }

    private static void Hide(Renderer r)
    {
        if (r != null) r.enabled = false;
    }
}
