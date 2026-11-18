using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 9105 긴급 교신: 족장 스킬이 보스의 해킹 스택을 전량 소모하는 순간을 크게 보여준다.
/// 보스의 해킹 표식을 바깥에서 안쪽 순서로 터뜨린다.
/// 진입은 <see cref="Play"/> 하나다. 게임에서는 스택을 실제로 소모(Commit)한 직후에 부른다.
/// </summary>
[DefaultExecutionOrder(320)]
public sealed class ChiefStackBurstFx : MonoBehaviour
{
    private const int MaxMarks = 16, ZigzagPoints = 7;
    private const float LinkWidth = .07f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private ChiefStackBurstSettings _settings;

    private sealed class Mark
    {
        public Renderer Glow, Core, Ring;
        public LineRenderer Link;
        public Vector3 Start;
        public float PopAt;
    }

    private readonly Mark[] _marks = new Mark[MaxMarks];
    private readonly Vector3[] _zigzag = new Vector3[ZigzagPoints];
    private readonly List<Vector3> _starts = new List<Vector3>(MaxMarks);
    private MaterialPropertyBlock _block;
    private bool _built;
    private int _count;
    private float _t = -1f, _endAt;

    /// <summary>재생 중 여부.</summary>
    public bool IsPlaying => _t >= 0f;

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
        _starts.Sort((a, b) => (b - impact).sqrMagnitude.CompareTo((a - impact).sqrMagnitude));
        _count = _starts.Count;

        for (int i = 0; i < _count; i++)
        {
            var m = _marks[i];
            m.Start = _starts[i];
            m.PopAt = i * s.ChainSpacing;
        }
        _endAt = _marks[_count - 1].PopAt + s.PopSeconds;
        _t = 0f;
        Render();
    }

    /// <summary>즉시 멈추고 모두 숨긴다.</summary>
    public void Stop()
    {
        _t = -1f;
        if (!_built) return;
        foreach (var m in _marks) HideMark(m);
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
        for (int i = 0; i < _count; i++) RenderMark(_marks[i], i, _t);
        for (int i = _count; i < MaxMarks; i++) HideMark(_marks[i]);
    }

    private void RenderMark(Mark m, int index, float t)
    {
        var s = _settings;
        float pulse = .5f + .5f * Mathf.Sin(t * 40f + index * 2.4f);
        if (t < m.PopAt)
        {
            DrawMarkBody(m, m.Start, 1f + .15f * pulse, s.Tint, 1f);
            m.Link.enabled = false; m.Ring.enabled = false;
            return;
        }
        float q = (t - m.PopAt) / s.PopSeconds;
        PopMark(m, m.Start, q, s.PopRing * (.8f + .7f * index / Mathf.Max(1f, _count - 1)));
        float link = (t - m.PopAt) / Mathf.Max(.01f, s.ChainLinkSeconds);
        if (index > 0 && link < 1f) Zigzag(m.Link, _marks[index - 1].Start, m.Start, LinkWidth, s.CoreColor, s.Tint, 1f - link, t);
        else m.Link.enabled = false;
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
