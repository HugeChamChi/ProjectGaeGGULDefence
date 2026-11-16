using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 9105 긴급 교신: 족장 스킬이 보스의 해킹 스택을 전량 소모하는 순간을 크게 보여준다.
/// 보스 몸의 해킹 표식(5스택당 1개)을 넘겨받아 시안(A 흡수, B 연쇄, C 일제)대로 착탄점에 모아 터뜨리고 소모 스택 수를 띄운다.
/// 진입은 <see cref="Play"/> 하나다. 게임에서는 스택을 실제로 소모(Commit)한 직후에 부른다.
/// </summary>
[DefaultExecutionOrder(320)]
public sealed class ChiefStackBurstFx : MonoBehaviour
{
    private const int MaxMarks = 16, MaxSparks = 32, ZigzagPoints = 7;
    private const float LinkWidth = .07f, TrailWidth = .12f, SparkWidth = .07f;
    private const float TextPunchSeconds = .16f, TextFadeSeconds = .3f, TextRise = .45f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private ChiefStackBurstSettings _settings;

    private sealed class Mark
    {
        public Renderer Glow, Core, Ring;
        public LineRenderer Link;
        public Vector3 Start, Bend;
        public float Depart, Arrive, PopAt;
    }

    private readonly Mark[] _marks = new Mark[MaxMarks];
    private readonly LineRenderer[] _sparks = new LineRenderer[MaxSparks];
    private readonly Vector3[] _sparkDirections = new Vector3[MaxSparks];
    private readonly float[] _sparkLengths = new float[MaxSparks];
    private readonly Vector3[] _zigzag = new Vector3[ZigzagPoints];
    private readonly List<Vector3> _starts = new List<Vector3>(MaxMarks);
    private Renderer _centerGlow, _centerCore, _ringA, _ringB, _flash;
    private LineRenderer _finalLink;
    private TextMeshPro _text;
    private MaterialPropertyBlock _block;
    private bool _built, _burstRaised;
    private int _count, _consumed, _shownNumber = -1;
    private Vector3 _impact;
    private float _t = -1f, _burstAt, _endAt;

    /// <summary>현재 시안 설정. 실험실에서 시안을 바꿀 때 교체한다.</summary>
    public ChiefStackBurstSettings Settings { get => _settings; set => _settings = value; }

    /// <summary>재생 중 여부.</summary>
    public bool IsPlaying => _t >= 0f;

    /// <summary>모인 스택이 한 번에 터지는 순간(효과음 연결 지점).</summary>
    public event Action OnBurst;

    /// <summary>
    /// 소모된 표식 위치를 받아 처음부터 재생한다. <paramref name="marks"/>가 비어 있으면 착탄점 둘레에 표식을 대신 놓는다.
    /// </summary>
    /// <param name="impact">족장 빔이 꽂히는 지점(월드).</param>
    /// <param name="marks">방금 소모되어 사라진 보스 해킹 표식의 월드 위치.</param>
    /// <param name="consumed">실제로 소모된 스택 수.</param>
    public void Play(Vector3 impact, IReadOnlyList<Vector3> marks, int consumed)
    {
        if (_settings == null || consumed <= 0) return;
        Build();
        var s = _settings;
        _impact = impact;
        _consumed = consumed;
        _shownNumber = -1;
        _burstRaised = false;

        _starts.Clear();
        int given = marks != null ? Mathf.Min(MaxMarks, marks.Count) : 0;
        for (int i = 0; i < given; i++) _starts.Add(marks[i]);
        if (_starts.Count == 0)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(consumed / (float)Mathf.Max(1, s.StacksPerMark)), 1, MaxMarks);
            for (int i = 0; i < n; i++)
            {
                float angle = (i * 137.508f + 20f) * Mathf.Deg2Rad;
                float radius = s.FallbackRadius * Mathf.Sqrt((i + .5f) / n);
                _starts.Add(impact + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .8f, 0f));
            }
        }
        // 바깥(착탄점에서 먼) 표식부터: 흡수는 멀리서부터 빨려 들고, 연쇄는 바깥에서 안으로 번진다.
        _starts.Sort((a, b) => (b - impact).sqrMagnitude.CompareTo((a - impact).sqrMagnitude));
        _count = _starts.Count;

        for (int i = 0; i < _count; i++)
        {
            var m = _marks[i];
            m.Start = _starts[i];
            Vector3 toImpact = impact - m.Start;
            Vector3 side = new Vector3(-toImpact.y, toImpact.x, 0f).normalized * toImpact.magnitude * s.SiphonCurve;
            m.Bend = (m.Start + impact) * .5f + (i % 2 == 0 ? side : -side);
            switch (s.Style)
            {
                case ChiefStackBurstSettings.BurstStyle.Siphon:
                    m.Depart = i * s.SiphonStagger; m.Arrive = m.Depart + s.SiphonTravel; m.PopAt = m.Arrive; break;
                case ChiefStackBurstSettings.BurstStyle.Chain:
                    m.PopAt = i * s.ChainSpacing; m.Depart = m.Arrive = m.PopAt; break;
                default:
                    m.PopAt = m.Depart = m.Arrive = 0f; break;
            }
        }
        float last = _count > 0 ? _marks[_count - 1].PopAt : 0f;
        _burstAt = s.Style switch
        {
            ChiefStackBurstSettings.BurstStyle.Siphon => last + .04f,
            ChiefStackBurstSettings.BurstStyle.Chain => s.ImpactBurst ? last + s.ChainSpacing + s.ChainLinkSeconds * .5f : last + s.ChainSpacing,
            _ => s.VolleyDelay,
        };
        float tail = Mathf.Max(s.ImpactBurst ? s.BurstSeconds : s.PopSeconds,
            s.ShowCount ? TextPunchSeconds + s.TextHoldSeconds + TextFadeSeconds : 0f);
        _endAt = _burstAt + tail;

        int sparks = Mathf.Min(MaxSparks, s.SparkCount);
        for (int k = 0; k < sparks; k++)
        {
            float angle = (k / (float)Mathf.Max(1, sparks) * 360f + UnityEngine.Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
            _sparkDirections[k] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            _sparkLengths[k] = UnityEngine.Random.Range(.55f, 1f);
        }
        _t = 0f;
        Render();
    }

    /// <summary>즉시 멈추고 모두 숨긴다.</summary>
    public void Stop()
    {
        _t = -1f;
        if (!_built) return;
        foreach (var m in _marks) HideMark(m);
        foreach (var line in _sparks) line.enabled = false;
        _centerGlow.enabled = _centerCore.enabled = _ringA.enabled = _ringB.enabled = _flash.enabled = false;
        _finalLink.enabled = false;
        _text.enabled = false;
    }

    private void Update()
    {
        if (_t < 0f) return;
        _t += Time.deltaTime;
        if (_t >= _endAt) { Stop(); return; }
        Render();
    }

    private void OnDisable() => Stop();

    private void Render()
    {
        var s = _settings;
        float t = _t;
        int done = 0;
        for (int i = 0; i < _count; i++)
            if (RenderMark(_marks[i], i, t)) done++;
        for (int i = _count; i < MaxMarks; i++) HideMark(_marks[i]);
        float gathered = _count > 0 ? done / (float)_count : 1f;
        Color tint = Color.Lerp(s.Tint, s.HotTint, gathered);

        // B 연쇄: 마지막 표식에서 착탄점으로 넘어가는 번개
        _finalLink.enabled = false;
        if (s.ImpactBurst && s.Style == ChiefStackBurstSettings.BurstStyle.Chain && _count > 0)
        {
            float from = _marks[_count - 1].PopAt + s.ChainSpacing * .5f;
            float q = (t - from) / Mathf.Max(.01f, s.ChainLinkSeconds + s.ChainSpacing);
            if (q >= 0f && q < 1f) Zigzag(_finalLink, _marks[_count - 1].Start, _impact, LinkWidth * 1.6f, s.CoreColor, tint, 1f - q, t);
        }

        float tb = t - _burstAt;
        if (tb < 0f)
        {
            // 터지기 전: 착탄점에 모인 만큼 핵이 부푼다.
            float charge = s.Style == ChiefStackBurstSettings.BurstStyle.Volley ? Mathf.Clamp01(t / Mathf.Max(.01f, s.VolleyDelay)) : gathered;
            if (charge > 0f && s.ImpactBurst)
            {
                float wobble = 1f + .08f * Mathf.Sin(t * 70f);
                Draw(_centerGlow, _impact, s.MarkGlowSize * (.8f + 1.8f * charge) * wobble, tint, .45f + .5f * charge);
                Draw(_centerCore, _impact, s.MarkCoreSize * (1f + 2.2f * charge) * wobble, s.CoreColor, .9f);
            }
            else _centerGlow.enabled = _centerCore.enabled = false;
            _ringA.enabled = _ringB.enabled = _flash.enabled = false;
            foreach (var line in _sparks) line.enabled = false;
            ShowNumber(s.Style == ChiefStackBurstSettings.BurstStyle.Volley ? _consumed : Mathf.RoundToInt(_consumed * gathered), -1f);
            return;
        }

        if (!_burstRaised) { _burstRaised = true; OnBurst?.Invoke(); }
        if (!s.ImpactBurst)
        {
            _centerGlow.enabled = _centerCore.enabled = _ringA.enabled = _ringB.enabled = _flash.enabled = false;
            foreach (var line in _sparks) line.enabled = false;
            ShowNumber(_consumed, tb);
            return;
        }
        float qb = Mathf.Clamp01(tb / s.BurstSeconds);
        float easeOut = 1f - Mathf.Pow(1f - qb, 3f);
        float flashQ = Mathf.Clamp01(qb * 2.2f);
        Draw(_flash, _impact, Mathf.Lerp(.6f, s.BurstFlash, 1f - (1f - flashQ) * (1f - flashQ)), Color.Lerp(s.CoreColor, tint, .25f), (1f - flashQ) * (1f - flashQ));
        Draw(_centerGlow, _impact, s.MarkGlowSize * 2.6f * (1f + .6f * easeOut), tint, (1f - qb) * .9f);
        Draw(_centerCore, _impact, s.MarkCoreSize * 3.2f * (1f - .7f * qb), s.CoreColor, 1f - qb);
        Draw(_ringA, _impact, Mathf.Lerp(.3f, s.BurstRing, easeOut), Color.Lerp(s.CoreColor, tint, .4f), 1f - qb);
        float qb2 = Mathf.Clamp01((tb - .07f) / s.BurstSeconds);
        if (qb2 > 0f) Draw(_ringB, _impact, Mathf.Lerp(.2f, s.BurstRing * .62f, 1f - Mathf.Pow(1f - qb2, 3f)), tint, (1f - qb2) * .85f);
        else _ringB.enabled = false;
        int sparks = Mathf.Min(MaxSparks, s.SparkCount);
        for (int k = 0; k < MaxSparks; k++)
        {
            var line = _sparks[k];
            float qs = Mathf.Clamp01(tb / (s.BurstSeconds * .7f));
            if (k >= sparks || qs >= 1f) { line.enabled = false; continue; }
            float reach = s.SparkReach * _sparkLengths[k];
            float head = 1f - Mathf.Pow(1f - qs, 3f), tail = 1f - Mathf.Pow(1f - Mathf.Clamp01(qs - .22f), 3f);
            SetLine(line, _impact + _sparkDirections[k] * reach * tail, _impact + _sparkDirections[k] * reach * head,
                SparkWidth * (1f - qs), SparkWidth * .2f * (1f - qs), s.CoreColor, tint, 1f - qs);
        }
        ShowNumber(_consumed, tb);
    }

    // 표식 하나. 소모가 끝난(착탄점에 도착했거나 터진) 표식이면 true.
    private bool RenderMark(Mark m, int index, float t)
    {
        var s = _settings;
        float pulse = .5f + .5f * Mathf.Sin(t * 40f + index * 2.4f);
        switch (s.Style)
        {
            case ChiefStackBurstSettings.BurstStyle.Siphon:
            {
                if (t < m.Depart)
                {
                    // 출발 전: 착탄점과 실선으로 이어진 채 하얗게 달아오른다.
                    DrawMarkBody(m, m.Start, 1.15f + .25f * pulse, Color.Lerp(s.Tint, s.CoreColor, .5f * pulse), 1f);
                    SetLine(m.Link, m.Start, _impact, LinkWidth * .6f, LinkWidth * .25f, s.CoreColor, s.Tint, .45f);
                    m.Ring.enabled = false;
                    return false;
                }
                if (t < m.Arrive)
                {
                    float p = (t - m.Depart) / Mathf.Max(.001f, m.Arrive - m.Depart);
                    float e = p * p;
                    Vector3 pos = Bezier(m.Start, m.Bend, _impact, e);
                    Vector3 tail = Bezier(m.Start, m.Bend, _impact, Mathf.Max(0f, e - .3f));
                    DrawMarkBody(m, pos, 1.1f - .45f * e, s.Tint, 1f);
                    SetLine(m.Link, tail, pos, TrailWidth * .25f, TrailWidth, s.Tint, s.CoreColor, .95f);
                    m.Ring.enabled = false;
                    return false;
                }
                // 도착: 착탄점에서 작은 고리 한 번
                m.Glow.enabled = m.Core.enabled = false; m.Link.enabled = false;
                float q = (t - m.Arrive) / s.PopSeconds;
                if (q < 1f) Draw(m.Ring, _impact, Mathf.Lerp(.25f, s.PopRing * .7f, 1f - (1f - q) * (1f - q)), s.Tint, 1f - q);
                else m.Ring.enabled = false;
                return true;
            }
            case ChiefStackBurstSettings.BurstStyle.Chain:
            {
                if (t < m.PopAt)
                {
                    DrawMarkBody(m, m.Start, 1f + .15f * pulse, s.Tint, 1f);
                    m.Link.enabled = false; m.Ring.enabled = false;
                    return false;
                }
                float q = (t - m.PopAt) / s.PopSeconds;
                PopMark(m, m.Start, q, s.PopRing * (.8f + .7f * index / Mathf.Max(1f, _count - 1)));
                float link = (t - m.PopAt) / Mathf.Max(.01f, s.ChainLinkSeconds);
                if (index > 0 && link < 1f) Zigzag(m.Link, _marks[index - 1].Start, m.Start, LinkWidth, s.CoreColor, s.Tint, 1f - link, t);
                else m.Link.enabled = false;
                return true;
            }
            default:
            {
                float q = t / s.PopSeconds;
                PopMark(m, m.Start, q, s.PopRing * 1.25f);
                // 빛줄기가 각 표식에서 착탄점으로 꽂힌다.
                float head = Mathf.Clamp01(t / Mathf.Max(.01f, s.VolleyDelay));
                float fade = Mathf.Clamp01(1f - (t - s.VolleyDelay) / .2f);
                if (fade > 0f) SetLine(m.Link, Vector3.Lerp(m.Start, _impact, head * .35f), Vector3.Lerp(m.Start, _impact, head),
                    TrailWidth * .4f, TrailWidth, s.Tint, s.CoreColor, fade);
                else m.Link.enabled = false;
                return true;
            }
        }
    }

    private void PopMark(Mark m, Vector3 position, float q, float ring)
    {
        var s = _settings;
        if (q >= 1f) { m.Glow.enabled = m.Core.enabled = m.Ring.enabled = false; return; }
        float ease = 1f - Mathf.Pow(1f - q, 3f), fade = 1f - q;
        Draw(m.Glow, position, s.MarkGlowSize * (1.2f + 1.3f * ease), Color.Lerp(s.CoreColor, s.Tint, q), fade * fade);
        Draw(m.Core, position, s.MarkCoreSize * (1.6f + 1.4f * ease), s.CoreColor, fade);
        Draw(m.Ring, position, Mathf.Lerp(.2f, ring, ease), s.Tint, fade);
    }

    private void DrawMarkBody(Mark m, Vector3 position, float scale, Color tint, float alpha)
    {
        var s = _settings;
        Draw(m.Glow, position, s.MarkGlowSize * scale, tint, .85f * alpha);
        Draw(m.Core, position, s.MarkCoreSize * scale, s.CoreColor, alpha);
    }

    private void ShowNumber(int number, float sinceBurst)
    {
        var s = _settings;
        if (number <= 0 || !s.ShowCount) { _text.enabled = false; return; }
        _text.enabled = true;
        if (number != _shownNumber)
        {
            _shownNumber = number;
            _text.text = string.IsNullOrEmpty(s.Label) ? number.ToString() : "<size=45%>" + s.Label + "</size>\n" + number;
        }
        Vector3 position = _impact + s.TextOffset;
        float scale = .72f, alpha = .9f;
        if (sinceBurst >= 0f)
        {
            float punch = Mathf.Clamp01(sinceBurst / TextPunchSeconds);
            scale = Mathf.Lerp(1.75f, 1f, 1f - (1f - punch) * (1f - punch));
            float fade = Mathf.Clamp01((sinceBurst - TextPunchSeconds - s.TextHoldSeconds) / TextFadeSeconds);
            alpha = 1f - fade;
            position += Vector3.up * TextRise * fade;
        }
        _text.transform.position = position;
        _text.transform.localScale = Vector3.one * scale;
        _text.alpha = alpha;
    }

    private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * c + t * t * b;
    }

    private void Zigzag(LineRenderer line, Vector3 a, Vector3 b, float width, Color head, Color tail, float alpha, float time)
    {
        Vector3 along = b - a;
        Vector3 side = new Vector3(-along.y, along.x, 0f).normalized * Mathf.Min(.28f, along.magnitude * .12f);
        for (int i = 0; i < ZigzagPoints; i++)
        {
            float u = i / (float)(ZigzagPoints - 1);
            float jitter = i == 0 || i == ZigzagPoints - 1 ? 0f : Mathf.Sin(i * 12.9898f + time * 55f) * (i % 2 == 0 ? 1f : -1f);
            _zigzag[i] = a + along * u + side * jitter;
        }
        line.positionCount = ZigzagPoints;
        line.SetPositions(_zigzag);
        line.startWidth = width; line.endWidth = width * .6f;
        head.a = alpha; tail.a = alpha;
        line.startColor = head; line.endColor = tail;
        line.enabled = alpha > .005f;
    }

    private static void SetLine(LineRenderer line, Vector3 a, Vector3 b, float startWidth, float endWidth, Color start, Color end, float alpha)
    {
        line.positionCount = 2;
        line.SetPosition(0, a); line.SetPosition(1, b);
        line.startWidth = startWidth; line.endWidth = endWidth;
        start.a = alpha; end.a = alpha;
        line.startColor = start; line.endColor = end;
        line.enabled = alpha > .005f;
    }

    private void Draw(Renderer renderer, Vector3 position, float size, Color color, float alpha)
    {
        renderer.enabled = alpha > .005f && renderer.sharedMaterial != null;
        if (!renderer.enabled) return;
        renderer.transform.position = position;
        Vector3 scale = transform.lossyScale;
        renderer.transform.localScale = new Vector3(size / Mathf.Max(.0001f, Mathf.Abs(scale.x)), size / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1f);
        color.a = alpha;
        _block.Clear(); _block.SetColor(ColorId, color); renderer.SetPropertyBlock(_block);
    }

    private static void HideMark(Mark m)
    {
        if (m == null) return;
        m.Glow.enabled = m.Core.enabled = m.Ring.enabled = false;
        m.Link.enabled = false;
    }

    private void Build()
    {
        if (_built) return;
        _built = true;
        var s = _settings;
        _block = new MaterialPropertyBlock();
        int order = s.SortingOrder;
        for (int i = 0; i < MaxMarks; i++)
        {
            _marks[i] = new Mark
            {
                Link = Line("Link" + i, order),
                Glow = Quad("MarkGlow" + i, s.GlowMaterial, order + 2),
                Core = Quad("MarkCore" + i, s.GlowMaterial, order + 3),
                Ring = Quad("MarkRing" + i, s.RingMaterial, order + 4),
            };
        }
        _finalLink = Line("FinalLink", order + 1);
        _centerGlow = Quad("CenterGlow", s.GlowMaterial, order + 5);
        _centerCore = Quad("CenterCore", s.GlowMaterial, order + 6);
        _ringA = Quad("BurstRingA", s.RingMaterial, order + 7);
        _ringB = Quad("BurstRingB", s.RingMaterial, order + 7);
        for (int k = 0; k < MaxSparks; k++) _sparks[k] = Line("Spark" + k, order + 8);
        _flash = Quad("Flash", s.GlowMaterial, order + 9);

        var textGo = new GameObject("StackCount", typeof(TextMeshPro));
        textGo.transform.SetParent(transform, false);
        _text = textGo.GetComponent<TextMeshPro>();
        if (s.Font != null) _text.font = s.Font;
        _text.fontSize = s.TextSize;
        _text.fontStyle = FontStyles.Bold;
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.rectTransform.sizeDelta = new Vector2(8f, 4f);
        _text.color = Color.white;
        _text.outlineWidth = .22f;
        _text.outlineColor = new Color32(20, 60, 140, 255);
        var textRenderer = textGo.GetComponent<MeshRenderer>();
        textRenderer.sortingLayerName = s.SortingLayer; textRenderer.sortingOrder = order + 10;
        _text.enabled = false;
    }

    private Renderer Quad(string name, Material material, int order)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = _settings.SortingLayer; renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }

    private LineRenderer Line(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true; line.positionCount = 2;
        line.numCapVertices = 2;
        line.sharedMaterial = _settings.LineMaterial;
        line.sortingLayerName = _settings.SortingLayer; line.sortingOrder = order;
        line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        line.enabled = false;
        return line;
    }
}
