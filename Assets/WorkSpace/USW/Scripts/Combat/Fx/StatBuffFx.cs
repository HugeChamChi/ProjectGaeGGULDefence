using UnityEngine;

/// <summary>
/// 유닛 버프 연출 — 분위기 레퍼런스 design/버프형식.gif, design/버프예시2.gif.
/// 모양새는 StatBuffFxStyle(SO 시안)로 정한다. 요소:
///   떠오르는 조각(세모·화살·번개·별·잎…, 섞기·겹치기·아래로) · 반짝이 · 속도선 ·
///   발밑 소용돌이 / 오라 / 남은 시간 눈금 · 유닛 뒤 빛기둥 · 머리 위 배지 · 시작 고리.
/// 전장 바닥이 밝아서 몸통은 기본 스프라이트 머티리얼로 색을 그대로 칠하고, 번짐·반짝이만 USW/Fx/AdditiveWorld로 더한다
/// (더하기만 쓰면 하얗게 날아감 — 실험실 1차 캡처 2026-10-11). 처음 시안의 빨강·노랑 마름모는 사용자 피드백으로 뺐다.
/// 그림은 StatBuffShapes가 코드로 만든다. 실험실(StatBuffFxLab)에서 고르고, 게임에서는 버프가 걸릴 때 Play(유닛, 시안, 지속시간)를 부른다.
/// 한 인스턴스는 시안 하나를 보여 준다 — 버프가 여럿이면 버프마다 인스턴스를 둔다.
/// </summary>
public sealed class StatBuffFx : MonoBehaviour
{
    private const int MaxRisers = 64;
    private const int MaxSparkles = 20;
    private const int MaxLines = 24;
    private const int MaxArcs = 4;
    private const int MaxPips = 16;
    private const float SparkleLife = 0.45f;
    private const float BadgePop = 0.25f;
    private const float BadgeFade = 0.25f;
    private const float BurstSeconds = 0.45f;
    private const float GroundFlatten = 0.36f; // 발밑 타원 납작함 (세로/가로)

    [Header("머티리얼 (번짐 — USW/Fx/AdditiveWorld)")]
    [SerializeField] private Material _additiveMaterial;
    [SerializeField] private string _sortingLayer = "Default";
    [Tooltip("유닛보다 앞에 그릴 기준 순서")]
    [SerializeField] private int _frontOrder = 20;
    [Tooltip("유닛 뒤에 그릴 순서 (빛기둥·발밑 뒤쪽)")]
    [SerializeField] private int _behindOrder = 2;
    [SerializeField] private bool _useUnscaledTime;

    private sealed class Riser
    {
        public bool Active;
        public float Born;
        public float Life;
        public Vector3 Start;
        public float Size;
        public float Spin;
        public float Tilt;
        public float Phase;
        public bool Down;
        public Color Color;
        public SpriteRenderer Fill;
        public SpriteRenderer Outline;
    }

    private sealed class Line
    {
        public bool Active;
        public float Born;
        public Vector3 Start;
        public SpriteRenderer Renderer;
    }

    private Riser[] _risers = new Riser[MaxRisers];
    private Riser[] _sparkles = new Riser[MaxSparkles];
    private Line[] _lines = new Line[MaxLines];
    private SpriteRenderer[] _arcs = new SpriteRenderer[MaxArcs];
    private SpriteRenderer[] _pips = new SpriteRenderer[MaxPips];
    private readonly System.Random _random = new System.Random();
    private bool _compact;
    private bool _ownsAdditiveMaterial;
    private Renderer[] _sortingRenderers;
    private int[] _sortingOffsets;
    private SpriteRenderer _unused;
    private System.Func<Sprite, Sprite> _spriteLookup;
    private Sprite Resolve(Sprite sprite) => _spriteLookup != null ? _spriteLookup(sprite) : sprite;
    private Transform _arcRoot;
    private SpriteRenderer _burst, _aura, _pillarBack, _pillarCore, _badgeGlow, _badgeOutline, _badgeFill;

    private StatBuffFxStyle _style;
    private Color? _tint;
    private Transform _anchor;
    private float _scale = 1f;
    private float _clock;
    private float _startAt = -1f;
    private float _endAt;
    private float _duration;
    private float _burstAt = -1f;
    private float _badgeAt = -1f;
    private float _nextRiserAt, _nextSparkleAt, _nextLineAt, _nextBadgeAt;
    private int _side;
    private bool _built;

    /// <summary>표시 중(사라지는 중 포함)이면 true.</summary>
    public bool IsPlaying => _startAt >= 0f;
    /// <summary>지금 보여 주는 시안.</summary>
    public StatBuffFxStyle Style => _style;
    /// <summary>scaled/unscaled 시계 선택 (실험실 고정 프레임 캡처용).</summary>
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

    private Color Primary => _tint ?? (_style != null ? _style.Primary : Color.white);

    /// <summary>
    /// anchor(유닛 발밑) 위치에 시안 style로 버프 연출을 건다. duration ≤ 0이면 Stop()까지 계속.
    /// 같은 시안으로 표시 중에 다시 부르면 끝나는 시각만 늘리고 시작 고리·배지를 다시 띄운다 (처음부터 재생하지 않음).
    /// </summary>
    public void Play(Transform anchor, StatBuffFxStyle style, float duration, float scale = 1f, bool playIntro = true)
    {
        if (style == null) return;
        if (!_built) _style = style;
        Build();
        _anchor = anchor;
        _scale = Mathf.Max(0.05f, scale);
        bool fresh = !IsPlaying || _clock >= _endAt || style != _style;
        if (fresh)
        {
            if (style != _style) HideAll();
            _style = style;
            _startAt = _clock;
            _endAt = duration > 0f ? _clock + duration : float.PositiveInfinity;
            _duration = duration;
            _nextRiserAt = _clock;
            _nextSparkleAt = _clock + 0.05f;
            _nextLineAt = _clock;
            if (playIntro) for (int i = 0; i < style.BurstCount; i++) SpawnRiser();
        }
        else if (duration > 0f)
        {
            _endAt = Mathf.Max(_endAt, _clock + duration);
            _duration = _endAt - _clock;
        }
        else _endAt = float.PositiveInfinity;
        _burstAt = playIntro && style.BurstRing ? _clock : -1f;
        _badgeAt = playIntro && style.Badge != StatBuffShape.None ? _clock : -1f;
        _nextBadgeAt = style.BadgeRepeat > 0f ? _clock + style.BadgeRepeat : float.PositiveInfinity;
        Follow();
    }

    /// <summary>조각·오라·빛기둥·눈금의 기본색(Primary)을 덮는다. null이면 시안 색으로 돌아간다.</summary>
    public void SetTint(Color? tint) => _tint = tint;

    /// <summary>인게임 풀 전용: 첫 Play 전에 필요한 시안 요소만 생성하도록 설정한다.</summary>
    public void ConfigureCompact(Material additiveMaterial, System.Func<Sprite, Sprite> spriteLookup = null)
    {
        if (_built) return;
        _compact = true; _additiveMaterial = additiveMaterial;
        _spriteLookup = spriteLookup;
    }

    /// <summary>외부 수명 안에서만 퇴장시킨다. 조각이 실제 버프 종료 뒤에 남지 않는다.</summary>
    public void SetRemaining(float remaining)
    {
        if (!_built || _style == null) return;
        float fade = Mathf.Clamp01(remaining / Mathf.Max(.05f, _style.Outro));
        foreach (var renderer in _sortingRenderers)
        {
            if (renderer is not SpriteRenderer sprite || !sprite.enabled) continue;
            var color = sprite.color; color.a *= fade; sprite.color = color;
        }
    }

    /// <summary>유닛의 실제 레이어와 드래그 깊이를 따라간다.</summary>
    public void SetSorting(int layer, int front, int behind)
    {
        if (!_built || (_frontOrder == front && _behindOrder == behind && _sortingRenderers[0].sortingLayerID == layer)) return;
        _frontOrder = front; _behindOrder = behind;
        for (int i = 0; i < _sortingRenderers.Length; i++)
        {
            _sortingRenderers[i].sortingLayerID = layer;
            bool back = _sortingRenderers[i] == _aura || _sortingRenderers[i] == _pillarBack;
            _sortingRenderers[i].sortingOrder = (back ? behind : front) + _sortingOffsets[i];
        }
    }

    private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    /// <summary>버프가 끝났을 때 — 남은 연출을 자연스럽게 사라지게 한다.</summary>
    public void Stop()
    {
        if (!IsPlaying) return;
        _endAt = Mathf.Min(_endAt, _clock);
    }

    /// <summary>모든 연출을 즉시 지운다.</summary>
    public void Clear()
    {
        _startAt = -1f;
        HideAll();
    }

    private void HideAll()
    {
        _burstAt = -1f;
        _badgeAt = -1f;
        if (!_built) return;
        foreach (var r in _risers) { r.Active = false; r.Fill.enabled = false; r.Outline.enabled = false; }
        foreach (var s in _sparkles) { s.Active = false; s.Fill.enabled = false; }
        foreach (var l in _lines) { l.Active = false; l.Renderer.enabled = false; }
        foreach (var a in _arcs) a.enabled = false;
        foreach (var p in _pips) p.enabled = false;
        _burst.enabled = _aura.enabled = _pillarBack.enabled = _pillarCore.enabled = false;
        _badgeGlow.enabled = _badgeOutline.enabled = _badgeFill.enabled = false;
    }

    private void Update()
    {
        _clock += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (!IsPlaying || !_built || _style == null) return;
        Follow();

        bool alive = _clock < _endAt;
        if (alive) Spawn();

        float intro = Mathf.Clamp01((_clock - _startAt) / _style.Intro);
        float outro = alive ? 1f : 1f - Mathf.Clamp01((_clock - _endAt) / _style.Outro);

        bool any = UpdateRisers();
        any |= UpdateSparkles();
        any |= UpdateLines();
        any |= UpdateBurst();
        any |= UpdateBadge(alive);
        UpdateSwoosh(intro, outro);
        UpdateAura(intro, outro);
        UpdatePillar(intro, outro);
        UpdatePips(intro, outro);

        if (!alive && outro <= 0f && !any) Clear();
    }

    private void Follow()
    {
        if (_anchor != null) transform.position = _anchor.position;
    }

    // ── 생성 ─────────────────────────────────────────────

    private void Spawn()
    {
        var s = _style;
        if (_clock >= _nextRiserAt)
        {
            _nextRiserAt = _clock + s.Interval * Range(0.7f, 1.3f);
            SpawnRiser();
        }
        if (s.SparkleInterval > 0f && s.SparkleSize > 0f && _clock >= _nextSparkleAt)
        {
            _nextSparkleAt = _clock + s.SparkleInterval * Range(0.6f, 1.4f);
            var start = new Vector3(Range(-1f, 1f) * s.Area.x * 1.1f, Range(0f, s.Area.y * 1.2f), 0f);
            Take(_sparkles, start, s.SparkleSize * Range(0.6f, 1.2f), s.SparkleColor, SparkleLife, false);
        }
        if (s.SpeedLines && _clock >= _nextLineAt)
        {
            _nextLineAt = _clock + s.SpeedLineInterval * Range(0.7f, 1.3f);
            foreach (var l in _lines)
            {
                if (l.Active) continue;
                l.Active = true;
                l.Born = _clock;
                l.Start = new Vector3(Range(-1f, 1f) * s.Area.x * 0.9f, Range(0f, s.Area.y * 0.5f), 0f);
                break;
            }
        }
        if (_clock >= _nextBadgeAt)
        {
            _badgeAt = _clock;
            _nextBadgeAt = _clock + s.BadgeRepeat;
        }
    }

    private void SpawnRiser()
    {
        var s = _style;
        bool secondary = s.SecondaryShare > 0f && _random.NextDouble() < s.SecondaryShare;
        var shape = secondary && s.SecondaryShape != StatBuffShape.None ? s.SecondaryShape : s.Shape;
        var color = secondary ? s.Secondary : Primary;
        bool down = secondary ? s.SecondaryDownward : s.Downward;

        Vector3 start;
        switch (s.Pattern)
        {
            case StatBuffSpawnPattern.Column:
                start = new Vector3(Range(-0.3f, 0.3f) * s.Area.x, Range(0f, 0.35f) * s.Area.y, 0f);
                break;
            case StatBuffSpawnPattern.Ring:
                float a = Range(0f, Mathf.PI * 2f);
                start = new Vector3(Mathf.Cos(a) * s.Area.x, 0.05f + Mathf.Sin(a) * s.Area.x * GroundFlatten, 0f);
                break;
            default:
                _side = _side > 0 ? -1 : 1; // 좌우를 번갈아 고르게 흩뿌린다
                start = new Vector3(_side * Range(0.15f, 1f) * s.Area.x, Range(0f, s.Area.y), 0f);
                break;
        }
        if (down) start.y += s.Rise;

        float size = Range(s.Size.x, s.Size.y);
        float life = s.Life * Range(0.85f, 1.15f);
        var r = Take(_risers, start, size, color, life, down);
        if (r == null) return;
        Dress(r, shape);
        if (s.Stacked)
        {
            var r2 = Take(_risers, start + new Vector3(0f, (down ? -1f : 1f) * size * s.StackGap, 0f), size, color, life * 1.05f, down);
            if (r2 != null) { Dress(r2, shape); r2.Spin = r.Spin; r2.Tilt = r.Tilt; r2.Phase = r.Phase; }
        }
    }

    private void Dress(Riser r, StatBuffShape shape)
    {
        var pair = StatBuffShapes.Get(shape);
        r.Fill.sprite = Resolve(pair.Fill);
        r.Outline.sprite = Resolve(pair.Outline);
        r.Spin = Range(_style.SpinRange.x, _style.SpinRange.y);
        r.Tilt = (_style.RandomTilt ? Range(-35f, 35f) : 0f) + (r.Down ? 180f : 0f);
        r.Phase = Range(0f, Mathf.PI * 2f);
    }

    private Riser Take(Riser[] pool, Vector3 start, float size, Color color, float life, bool down)
    {
        foreach (var r in pool)
        {
            if (r.Active) continue;
            r.Active = true;
            r.Born = _clock;
            r.Life = life;
            r.Start = start;
            r.Size = size;
            r.Color = color;
            r.Down = down;
            return r;
        }
        return null;
    }

    // ── 갱신 ─────────────────────────────────────────────

    private bool UpdateRisers()
    {
        var s = _style;
        bool any = false;
        foreach (var r in _risers)
        {
            if (!r.Active) continue;
            float t = (_clock - r.Born) / r.Life;
            if (t >= 1f) { r.Active = false; r.Fill.enabled = false; r.Outline.enabled = false; continue; }
            any = true;
            float alpha = Mathf.Clamp01(t / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t)));
            var pos = r.Start + new Vector3(Mathf.Sin(t * s.SwayFrequency + r.Phase) * s.Sway, (r.Down ? -1f : 1f) * s.Rise * EaseOutCubic(t), 0f);
            float size = r.Size * Mathf.Lerp(1f, s.EndScale, t) * (t < 0.15f ? EaseOutBack(t / 0.15f) : 1f);
            var rot = Quaternion.Euler(0f, 0f, r.Tilt + r.Spin * t * r.Life);
            var scale = Vector3.one * Mathf.Max(0.001f, size);
            var color = s.AnimateRiserColor && s.RiserColorOverLife != null && !_tint.HasValue
                ? s.RiserColorOverLife.Evaluate(t) : r.Color;
            Place(r.Fill, pos, scale, color, alpha * color.a);
            r.Fill.transform.localRotation = rot;
            if (s.Outline && r.Outline.sprite != null)
            {
                var oc = s.ResolveOutline(color);
                Place(r.Outline, pos, scale, oc, alpha * oc.a);
                r.Outline.transform.localRotation = rot;
            }
            else r.Outline.enabled = false;
        }
        return any;
    }

    private bool UpdateSparkles()
    {
        bool any = false;
        foreach (var r in _sparkles)
        {
            if (!r.Active) continue;
            float t = (_clock - r.Born) / r.Life;
            if (t >= 1f) { r.Active = false; r.Fill.enabled = false; continue; }
            any = true;
            Place(r.Fill, r.Start, Vector3.one * Mathf.Max(0.001f, r.Size * Mathf.Sin(t * Mathf.PI)), r.Color, r.Color.a);
            r.Fill.transform.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
        }
        return any;
    }

    private bool UpdateLines()
    {
        var s = _style;
        bool any = false;
        float life = s.SpeedLineSpeed > 0f ? Mathf.Max(0.15f, (s.Area.y + s.SpeedLineLength) / s.SpeedLineSpeed) : 0.4f;
        foreach (var l in _lines)
        {
            if (!l.Active) continue;
            float t = (_clock - l.Born) / life;
            if (t >= 1f) { l.Active = false; l.Renderer.enabled = false; continue; }
            any = true;
            var pos = l.Start + new Vector3(0f, s.SpeedLineSpeed * (_clock - l.Born), 0f);
            float alpha = Mathf.Sin(t * Mathf.PI) * 0.9f;
            Place(l.Renderer, pos, new Vector3(0.5f, s.SpeedLineLength, 1f), s.Secondary, alpha * s.Secondary.a);
        }
        return any;
    }

    private bool UpdateBurst()
    {
        if (_burstAt < 0f) { _burst.enabled = false; return false; }
        float t = (_clock - _burstAt) / BurstSeconds;
        if (t >= 1f) { _burst.enabled = false; _burstAt = -1f; return false; }
        float r = Mathf.Lerp(0.25f, 1f, EaseOutCubic(t)) * 1.7f;
        Place(_burst, new Vector3(0f, 0.04f, 0f), new Vector3(r, r * GroundFlatten, 1f), Primary, Mathf.Pow(1f - t, 1.5f));
        return true;
    }

    private bool UpdateBadge(bool alive)
    {
        var s = _style;
        if (_badgeAt < 0f || s.Badge == StatBuffShape.None)
        {
            _badgeGlow.enabled = _badgeOutline.enabled = _badgeFill.enabled = false;
            return false;
        }
        float age = _clock - _badgeAt;
        float fadeStart = BadgePop + s.BadgeHold;
        if (!alive) fadeStart = Mathf.Min(fadeStart, age);
        float fade = Mathf.Clamp01((age - fadeStart) / BadgeFade);
        if (fade >= 1f) { _badgeAt = -1f; _badgeGlow.enabled = _badgeOutline.enabled = _badgeFill.enabled = false; return false; }

        var pair = StatBuffShapes.Get(s.Badge);
        _badgeFill.sprite = Resolve(pair.Fill);
        _badgeOutline.sprite = Resolve(pair.Outline);
        float pop = EaseOutBack(Mathf.Clamp01(age / BadgePop));
        float bob = Mathf.Sin(age * 5f) * 0.03f;
        var pos = new Vector3(0f, s.BadgeY + bob + EaseInCubic(fade) * 0.25f, 0f);
        var scale = Vector3.one * Mathf.Max(0.001f, s.BadgeSize * pop * Mathf.Lerp(1f, 0.7f, fade));
        float alpha = 1f - fade;
        Place(_badgeGlow, pos, scale * 2.2f, s.BadgeColor, alpha * 0.45f);
        if (pair.Outline != null)
        {
            var oc = s.ResolveOutline(s.BadgeColor);
            Place(_badgeOutline, pos, scale, oc, alpha * oc.a);
        }
        else _badgeOutline.enabled = false;
        Place(_badgeFill, pos, scale, s.BadgeColor, alpha);
        return true;
    }

    private void UpdateSwoosh(float intro, float outro)
    {
        var s = _style;
        bool on = s.Swoosh && outro > 0f;
        float grow = Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(intro));
        float radius = s.SwooshRadius * _scale * grow / StatBuffShapes.ArcTextureRadius;
        _arcRoot.localPosition = new Vector3(0f, 0.04f * _scale, 0f);
        _arcRoot.localScale = new Vector3(radius, radius * GroundFlatten, 1f);
        for (int i = 0; i < _arcs.Length; i++)
        {
            var arc = _arcs[i];
            if (!on || i >= s.SwooshArcs) { arc.enabled = false; continue; }
            float angle = (_clock - _startAt) * s.SwooshSpeed + i * (360f / s.SwooshArcs);
            arc.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            // 호 가운데가 타원 위쪽(먼 쪽)이면 유닛 뒤에 그린다
            float mid = (angle + StatBuffShapes.ArcSpan * 0.5f) * Mathf.Deg2Rad;
            arc.sortingOrder = Mathf.Sin(mid) > 0f ? _behindOrder : _frontOrder - 2;
            var c = s.Secondary;
            c.a *= Mathf.Clamp01(intro * 2f) * outro;
            arc.color = c;
            arc.enabled = true;
        }
    }

    private void UpdateAura(float intro, float outro)
    {
        var s = _style;
        if (!s.GroundAura || outro <= 0f) { _aura.enabled = false; return; }
        float pulse = 0.85f + 0.15f * Mathf.Sin((_clock - _startAt) * 4f);
        float d = s.AuraRadius * 2f * Mathf.LerpUnclamped(0.5f, 1f, EaseOutBack(intro)) * pulse;
        Place(_aura, new Vector3(0f, 0.04f, 0f), new Vector3(d, d * GroundFlatten, 1f), Primary, 0.55f * intro * outro * Primary.a);
    }

    private void UpdatePillar(float intro, float outro)
    {
        var s = _style;
        if (!s.Pillar || outro <= 0f) { _pillarBack.enabled = _pillarCore.enabled = false; return; }
        float rise = EaseOutCubic(intro);
        float flicker = 0.85f + 0.15f * Mathf.Sin((_clock - _startAt) * 7f);
        var scale = new Vector3(s.PillarWidth / 0.5f, s.PillarHeight * rise, 1f);
        Place(_pillarBack, Vector3.zero, scale, Primary, 0.6f * outro * flicker);
        Place(_pillarCore, Vector3.zero, Vector3.Scale(scale, new Vector3(0.25f, 0.85f, 1f)), Color.white, 0.28f * outro * flicker);
    }

    private void UpdatePips(float intro, float outro)
    {
        var s = _style;
        bool on = s.TimerPips && _duration > 0f && outro > 0f;
        int count = Mathf.Clamp(s.PipCount, 4, MaxPips);
        float remain = on ? Mathf.Clamp01((_endAt - _clock) / _duration) : 0f;
        float radius = s.SwooshRadius * 1.1f;
        for (int i = 0; i < _pips.Length; i++)
        {
            var pip = _pips[i];
            if (!on || i >= count) { pip.enabled = false; continue; }
            // 앞쪽 가운데에서 시작해 시계 방향으로 한 바퀴 — 남은 비율만큼 켠다
            float a = (-90f - i * 360f / count) * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Cos(a) * radius, 0.04f + Mathf.Sin(a) * radius * GroundFlatten, 0f);
            bool lit = (i + 0.5f) / count < remain;
            float pop = Mathf.Clamp01(intro * 1.5f - i * 0.03f);
            Place(pip, pos, Vector3.one * 0.12f * EaseOutBack(pop), lit ? Primary : new Color(0f, 0f, 0f, 1f), (lit ? 1f : 0.22f) * outro);
            pip.sortingOrder = Mathf.Sin(a) > 0f ? _behindOrder : _frontOrder - 1;
        }
    }

    private void Place(SpriteRenderer sr, Vector3 localPos, Vector3 localScale, Color color, float alpha)
    {
        sr.transform.localPosition = localPos * _scale;
        sr.transform.localScale = localScale * _scale;
        color.a = Mathf.Clamp01(alpha);
        sr.color = color;
        sr.enabled = color.a > 0.002f;
    }

    // ── 만들기 ───────────────────────────────────────────

    private void OnDisable() => Clear();

    private void OnDestroy()
    {
        if (_ownsAdditiveMaterial && _additiveMaterial != null) Destroy(_additiveMaterial);
    }

    private void Build()
    {
        if (_built) return;
        _built = true;
        if (_additiveMaterial == null)
        {
            var shader = Shader.Find("USW/Fx/AdditiveWorld");
            if (shader != null)
            {
                _additiveMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                _ownsAdditiveMaterial = true;
            }
        }

        if (_compact)
        {
            var s = _style;
            int risers = Mathf.Clamp(Mathf.CeilToInt(s.Life * 1.15f / Mathf.Max(.02f, s.Interval * .7f)) * (s.Stacked ? 2 : 1) + s.BurstCount, 4, MaxRisers);
            _risers = new Riser[risers];
            _sparkles = new Riser[s.SparkleSize > 0f && s.SparkleInterval > 0f ? Mathf.Min(MaxSparkles, Mathf.CeilToInt(SparkleLife / (s.SparkleInterval * .6f)) + 1) : 0];
            float lineLife = s.SpeedLineSpeed > 0f ? Mathf.Max(.15f, (s.Area.y + s.SpeedLineLength) / s.SpeedLineSpeed) : .4f;
            _lines = new Line[s.SpeedLines ? Mathf.Min(MaxLines, Mathf.CeilToInt(lineLife / Mathf.Max(.02f, s.SpeedLineInterval * .7f)) + 1) : 0];
            _arcs = new SpriteRenderer[s.Swoosh ? Mathf.Clamp(s.SwooshArcs, 1, MaxArcs) : 0];
            _pips = new SpriteRenderer[s.TimerPips ? Mathf.Clamp(s.PipCount, 4, MaxPips) : 0];
            _unused = NewRenderer("Unused", null, false, _frontOrder);
        }

        for (int i = 0; i < _risers.Length; i++)
        {
            _risers[i] = new Riser
            {
                Outline = !_compact || _style.Outline ? NewRenderer($"Riser{i}_Outline", null, false, _frontOrder + 1) : _unused,
                Fill = NewRenderer($"Riser{i}", null, false, _frontOrder + 2),
            };
        }
        var sparkle = StatBuffShapes.Get(StatBuffShape.Sparkle).Fill;
        for (int i = 0; i < _sparkles.Length; i++) _sparkles[i] = new Riser { Fill = NewRenderer($"Sparkle{i}", sparkle, true, _frontOrder + 4) };
        for (int i = 0; i < _lines.Length; i++) _lines[i] = new Line { Renderer = NewRenderer($"SpeedLine{i}", StatBuffShapes.Streak, false, _frontOrder) };

        _arcRoot = new GameObject("Swoosh").transform;
        _arcRoot.SetParent(transform, false);
        for (int i = 0; i < _arcs.Length; i++)
        {
            _arcs[i] = NewRenderer($"Arc{i}", StatBuffShapes.Arc, false, _frontOrder - 2);
            _arcs[i].transform.SetParent(_arcRoot, false);
        }
        for (int i = 0; i < _pips.Length; i++) _pips[i] = NewRenderer($"Pip{i}", StatBuffShapes.Pip, false, _frontOrder - 1);
        _burst = !_compact || _style.BurstRing ? NewRenderer("Burst", StatBuffShapes.Ring, false, _frontOrder - 2) : _unused;
        _aura = !_compact || _style.GroundAura ? NewRenderer("Aura", StatBuffShapes.Dot, false, _behindOrder - 1) : _unused;
        _pillarBack = !_compact || _style.Pillar ? NewRenderer("PillarBack", StatBuffShapes.Column, false, _behindOrder) : _unused;
        _pillarCore = !_compact || _style.Pillar ? NewRenderer("PillarCore", StatBuffShapes.Column, true, _frontOrder - 3) : _unused;
        _badgeGlow = !_compact || _style.Badge != StatBuffShape.None ? NewRenderer("BadgeGlow", StatBuffShapes.Dot, true, _frontOrder + 5) : _unused;
        _badgeOutline = !_compact || _style.Badge != StatBuffShape.None ? NewRenderer("BadgeOutline", null, false, _frontOrder + 6) : _unused;
        _badgeFill = !_compact || _style.Badge != StatBuffShape.None ? NewRenderer("Badge", null, false, _frontOrder + 7) : _unused;
        _sortingRenderers = GetComponentsInChildren<Renderer>(true);
        _sortingOffsets = new int[_sortingRenderers.Length];
        for (int i = 0; i < _sortingRenderers.Length; i++)
        {
            bool back = _sortingRenderers[i] == _aura || _sortingRenderers[i] == _pillarBack;
            _sortingOffsets[i] = _sortingRenderers[i].sortingOrder - (back ? _behindOrder : _frontOrder);
        }
        Clear();
    }

    private SpriteRenderer NewRenderer(string name, Sprite sprite, bool additive, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Resolve(sprite);
        if (additive && _additiveMaterial != null) sr.sharedMaterial = _additiveMaterial;
        sr.sortingLayerName = _sortingLayer;
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    // ── 이징 ─────────────────────────────────────────────

    private static float EaseOutCubic(float p) { p = 1f - Mathf.Clamp01(p); return 1f - p * p * p; }
    private static float EaseInCubic(float p) { p = Mathf.Clamp01(p); return p * p * p; }

    private static float EaseOutBack(float p)
    {
        p = Mathf.Clamp01(p);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float q = p - 1f;
        return 1f + c3 * q * q * q + c1 * q * q;
    }
}
