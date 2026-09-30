using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI 캔버스 위에 그리는 경량 파티클 (불씨/반짝이 등). 파티클 전부를 메시 하나로 그려 드로우콜 1개.
/// ParticleSystem은 Screen Space Overlay 캔버스에 그려지지 않고 UI 파티클 패키지는 쓰지 않으므로 직접 구현했다.
/// 모양은 머티리얼(USW/UI/FxGlow 등)이 UV로 그리고, 이 컴포넌트는 위치/크기/색만 계산한다.
/// 생성 영역 = 이 RectTransform의 사각형. 기본은 unscaled 시간 (레벨업 일시정지 중에도 움직임).
/// 매 프레임 메시를 다시 만들므로 부모에 별도 Canvas를 두어 다른 UI와 리빌드를 분리할 것.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UiFxParticleEmitter : MaskableGraphic
{
    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public float Size;
        public Color Color;
        public float SwayPhase;
        public float SwayFrequency;
        public float TwinklePhase;
        public float TwinkleSpeed;
        public float Rotation;
        public float AngularVelocity;
    }

    [Tooltip("파티클 모양 텍스처 (비우면 흰 텍스처 — 모양은 머티리얼이 UV로 그림). USW/UI/FxAlphaMask와 쓰면 알파를 모양으로 쓴다")]
    [SerializeField] private Texture _texture;

    [Header("Playback")]
    [SerializeField] private bool _playOnEnable;
    [SerializeField] private bool _useUnscaledTime = true;
    [Tooltip("방출 지속 시간(초). 0 이하이면 Stop 전까지 계속 방출")]
    [SerializeField] private float _duration;
    [SerializeField] private int _maxParticles = 64;

    [Header("Emission")]
    [Tooltip("초당 방출 수")]
    [SerializeField] private float _rate = 20f;
    [Tooltip("Play 순간 한 번에 방출하는 수")]
    [SerializeField] private int _burst;
    [Tooltip("Play 시 수명 중간 상태로 영역에 미리 채워 둘 개수 (처음부터 화면에 퍼져 있게)")]
    [SerializeField] private int _prewarmCount;

    [Header("Converge (모이기)")]
    [Tooltip("켜면 영역 중심 둘레(반지름 범위)에서 생겨 중심으로 날아가 도착하는 순간 사라진다 (수명 = 거리 / 속력). 방향/퍼짐/가속/감쇠는 무시")]
    [SerializeField] private bool _converge;
    [SerializeField] private Vector2 _convergeRadius = new Vector2(400f, 900f);

    [Header("Particle")]
    [SerializeField] private Vector2 _lifetime = new Vector2(1f, 2f);
    [SerializeField] private Vector2 _size = new Vector2(8f, 16f);
    [SerializeField] private Vector2 _speed = new Vector2(20f, 60f);
    [Tooltip("진행 방향(도). 90 = 위")]
    [SerializeField] private float _direction = 90f;
    [Tooltip("방향 퍼짐(±도)")]
    [SerializeField] private float _spread = 30f;
    [Tooltip("가속도 (캔버스 단위/초²)")]
    [SerializeField] private Vector2 _acceleration;
    [Tooltip("속도 감쇠 (초당 비율, 0 = 없음)")]
    [SerializeField] private float _drag;
    [Tooltip("좌우 흔들림 폭 (캔버스 단위/초)")]
    [SerializeField] private float _swayAmplitude;
    [SerializeField] private Vector2 _swayFrequency = new Vector2(0.5f, 1.5f);
    [Tooltip("깜빡임 세기 (0 = 없음, 1 = 완전히 꺼졌다 켜짐)")]
    [Range(0f, 1f)] [SerializeField] private float _twinkle;
    [SerializeField] private Vector2 _twinkleSpeed = new Vector2(4f, 10f);
    [SerializeField] private Vector2 _angularVelocity;
    [Tooltip("속도 방향으로 늘이기(초) — 0이면 정사각형, >0이면 길이 = 크기 + 속력×값 (빛줄기 입자). 늘일 때는 회전 대신 진행 방향을 따른다")]
    [SerializeField] private float _velocityStretch;
    [Tooltip("시작 색 (그라디언트에서 무작위로 고름)")]
    [SerializeField] private Gradient _startColor = new Gradient();
    [Tooltip("수명 비율(0~1)에 따른 알파 배수")]
    [SerializeField] private AnimationCurve _alphaOverLife = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f));
    [Tooltip("수명 비율(0~1)에 따른 크기 배수")]
    [SerializeField] private AnimationCurve _sizeOverLife = AnimationCurve.Constant(0f, 1f, 1f);
    [Tooltip("영역 위쪽 이 비율(영역 높이 대비) 구간에서 서서히 사라지고, 위 끝을 넘으면 없어진다 (0 = 끔). " +
             "아래에서 떠오르다 영역 위 끝에서 녹아 없어지는 빛 알갱이용")]
    [Range(0f, 1f)] [SerializeField] private float _topFade;

    private Particle[] _particles;
    private int _count;
    private bool _emitting;
    private float _emitElapsed;
    private float _emitAccumulator;
    private bool _hadParticlesLastFrame;

    /// <summary>살아 있는 파티클이 있거나 방출 중이면 true.</summary>
    public bool IsAlive => _emitting || _count > 0;

    /// <summary>timeScale 무시 여부.</summary>
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

    /// <summary>방출 수 (초당). 연출 중 조절용.</summary>
    public float Rate { get => _rate; set => _rate = value; }

    /// <inheritdoc />
    public override Texture mainTexture => _texture != null ? _texture : base.mainTexture;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        EnsureBuffer();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_playOnEnable && Application.isPlaying) Play();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Clear();
    }

    /// <summary>방출을 시작한다 (버스트·미리 채우기 포함).</summary>
    public void Play()
    {
        EnsureBuffer();
        _emitting = true;
        _emitElapsed = 0f;
        _emitAccumulator = 0f;
        for (int i = 0; i < _prewarmCount; i++) Spawn(Random.value);
        Emit(_burst);
    }

    /// <summary>방출을 멈춘다. clear면 남은 파티클도 즉시 지운다.</summary>
    public void Stop(bool clear = false)
    {
        _emitting = false;
        if (clear) Clear();
    }

    /// <summary>파티클을 즉시 count개 방출한다.</summary>
    public void Emit(int count)
    {
        EnsureBuffer();
        for (int i = 0; i < count; i++) Spawn(0f);
    }

    /// <summary>모든 파티클을 지운다.</summary>
    public void Clear()
    {
        _count = 0;
        SetVerticesDirty();
    }

    private void EnsureBuffer()
    {
        int max = Mathf.Max(1, _maxParticles);
        if (_particles == null || _particles.Length != max) { _particles = new Particle[max]; _count = 0; }
    }

    private void Update()
    {
        if (!Application.isPlaying || _particles == null) return;
        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f && !_hadParticlesLastFrame) return;

        if (_emitting)
        {
            _emitElapsed += dt;
            _emitAccumulator += _rate * dt;
            int n = (int)_emitAccumulator;
            _emitAccumulator -= n;
            for (int i = 0; i < n; i++) Spawn(0f);
            if (_duration > 0f && _emitElapsed >= _duration) _emitting = false;
        }

        float dragFactor = _drag > 0f ? Mathf.Exp(-_drag * dt) : 1f;
        float killY = _topFade > 0f ? rectTransform.rect.yMax : float.PositiveInfinity;
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var p = ref _particles[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime || p.Position.y >= killY)
            {
                _particles[i] = _particles[--_count];
                continue;
            }
            if (!_converge) p.Velocity = (p.Velocity + _acceleration * dt) * dragFactor;
            float sway = _swayAmplitude * Mathf.Sin(p.SwayPhase + p.Age * p.SwayFrequency * Mathf.PI * 2f);
            p.Position += (p.Velocity + new Vector2(sway, 0f)) * dt;
            p.Rotation += p.AngularVelocity * dt;
        }

        if (_count > 0 || _hadParticlesLastFrame) SetVerticesDirty();
        _hadParticlesLastFrame = _count > 0;
    }

    /// <param name="startAge01">0이면 막 태어난 파티클, 0~1이면 수명 중간에서 시작 (미리 채우기).</param>
    private void Spawn(float startAge01)
    {
        if (_count >= _particles.Length) return;
        var rect = rectTransform.rect;
        float angle = (_direction + Random.Range(-_spread, _spread)) * Mathf.Deg2Rad;
        float speed = Random.Range(_speed.x, _speed.y);
        Vector2 position = new Vector2(Random.Range(rect.xMin, rect.xMax), Random.Range(rect.yMin, rect.yMax));
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float lifetime = Mathf.Max(0.01f, Random.Range(_lifetime.x, _lifetime.y));
        if (_converge)
        {
            float a = Random.value * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float r = Random.Range(_convergeRadius.x, _convergeRadius.y);
            position = rect.center + dir * r;
            velocity = -dir * speed;
            lifetime = Mathf.Max(0.01f, r / Mathf.Max(speed, 1f));
        }
        var p = new Particle
        {
            Position = position,
            Velocity = velocity,
            Lifetime = lifetime,
            Size = Random.Range(_size.x, _size.y),
            Color = _startColor.Evaluate(Random.value),
            SwayPhase = Random.value * Mathf.PI * 2f,
            SwayFrequency = Random.Range(_swayFrequency.x, _swayFrequency.y),
            TwinklePhase = Random.value * Mathf.PI * 2f,
            TwinkleSpeed = Random.Range(_twinkleSpeed.x, _twinkleSpeed.y),
            Rotation = Random.value * 360f,
            AngularVelocity = Random.Range(_angularVelocity.x, _angularVelocity.y),
        };
        p.Age = p.Lifetime * Mathf.Clamp01(startAge01);
        p.Position += p.Velocity * p.Age;
        _particles[_count++] = p;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_particles == null || _count == 0) return;

        var vert = UIVertex.simpleVert;
        Color tint = color;
        var rect = rectTransform.rect;
        float fadeBand = Mathf.Max(_topFade * rect.height, 1e-3f);
        for (int i = 0; i < _count; i++)
        {
            ref var p = ref _particles[i];
            float life01 = p.Age / p.Lifetime;
            float half = p.Size * _sizeOverLife.Evaluate(life01) * 0.5f;
            if (half <= 0f) continue;

            float twinkle = 1f - _twinkle * (0.5f + 0.5f * Mathf.Sin(p.TwinklePhase + p.Age * p.TwinkleSpeed));
            float top = _topFade > 0f ? Mathf.SmoothStep(0f, 1f, (rect.yMax - p.Position.y) / fadeBand) : 1f;
            Color c = p.Color * tint;
            c.a *= _alphaOverLife.Evaluate(life01) * twinkle * top;
            if (c.a <= 0.001f) continue;
            vert.color = c;

            Vector2 ax, ay;
            float speed = p.Velocity.magnitude;
            if (_velocityStretch > 0f && speed > 0.001f)
            {
                // 진행 방향으로 길게: ax = 길이 방향 반축, ay = 폭 방향 반축
                Vector2 dir = p.Velocity / speed;
                ax = dir * (half + speed * _velocityStretch * 0.5f);
                ay = new Vector2(-dir.y, dir.x) * half;
            }
            else
            {
                float rad = p.Rotation * Mathf.Deg2Rad;
                ax = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * half;
                ay = new Vector2(-ax.y, ax.x);
            }

            int start = vh.currentVertCount;
            vert.position = p.Position - ax - ay; vert.uv0 = new Vector4(0f, 0f); vh.AddVert(vert);
            vert.position = p.Position - ax + ay; vert.uv0 = new Vector4(0f, 1f); vh.AddVert(vert);
            vert.position = p.Position + ax + ay; vert.uv0 = new Vector4(1f, 1f); vh.AddVert(vert);
            vert.position = p.Position + ax - ay; vert.uv0 = new Vector4(1f, 0f); vh.AddVert(vert);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
