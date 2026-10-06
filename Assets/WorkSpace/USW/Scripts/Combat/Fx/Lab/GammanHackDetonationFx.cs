using UnityEngine;

/// <summary>Visual-only implanted marks and five boss-side rupture motifs for the comparison lab.</summary>
public sealed class GammanHackDetonationFx : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private Material _ringMaterial;
    [SerializeField] private Material _glowMaterial;
    private const int MarkCount = 6;
    private const float ContactTime = .10f;
    private readonly LineRenderer[] _marks = new LineRenderer[MarkCount];
    private readonly LineRenderer[] _links = new LineRenderer[MarkCount];
    private readonly LineRenderer[] _rays = new LineRenderer[36];
    private readonly LineRenderer[] _aim = new LineRenderer[4];
    private readonly Renderer[] _halos = new Renderer[MarkCount];
    private readonly Renderer[] _cores = new Renderer[MarkCount];
    private readonly Renderer[] _waves = new Renderer[3];
    private MaterialPropertyBlock _block;
    private bool _ready;
    private Vector3 Center => _target.position + new Vector3(0, -.05f, -.65f);

    private void Awake() => EnsurePool();
    private void OnDisable() => Clear();

    private void EnsurePool()
    {
        if (_ready) return;
        _ready = true;
        _block = new MaterialPropertyBlock();
        for (int i = 0; i < MarkCount; i++)
        {
            _marks[i] = Line("HackMark_" + i);
            _links[i] = Line("Circuit_" + i);
            _halos[i] = Quad("NodeRupture_" + i, _ringMaterial);
            _cores[i] = Quad("NodeCore_" + i, _glowMaterial);
        }
        for (int i = 0; i < _rays.Length; i++) _rays[i] = Line("Fragment_" + i);
        for (int i = 0; i < _aim.Length; i++) _aim[i] = Line("LockBracket_" + i);
        for (int i = 0; i < _waves.Length; i++) _waves[i] = Quad("BodyShock_" + i, _ringMaterial);
        Clear();
    }

    /// <summary>Hide the complete pool when replaying, switching variants, or disabling the lab.</summary>
    public void Clear()
    {
        if (!_ready) return;
        foreach (var r in _marks) r.enabled = false;
        foreach (var r in _links) r.enabled = false;
        foreach (var r in _rays) r.enabled = false;
        foreach (var r in _aim) r.enabled = false;
        foreach (var r in _halos) r.enabled = false;
        foreach (var r in _cores) r.enabled = false;
        foreach (var r in _waves) r.enabled = false;
    }

    /// <summary>Render a deterministic frame from the lab timeline; never changes combat or time scale.</summary>
    public void Render(GammanBlinkStyle style, int phase, float elapsed)
    {
        EnsurePool();
        Clear();
        int motif = style.DetonationPattern;
        bool shot = phase == 5;
        bool burst = shot && elapsed >= style.BurstDelay;
        float p = burst ? Mathf.Clamp01((elapsed - style.BurstDelay) / style.BurstDuration) : 0;
        float response = shot ? Mathf.Clamp01((elapsed - ContactTime) / Mathf.Max(.01f, style.BurstDelay - ContactTime)) : 0;
        float fade = 1f - p;
        Color markColor = Color.Lerp(style.HackTint, Color.white, response * .85f);
        if (phase >= 6 || (burst && p >= 1f)) return;

        // Marks exist before the signal: the strike activates something already planted in the boss.
        for (int i = 0; i < MarkCount; i++)
        {
            Vector3 node = Node(i);
            if (motif == 1 && shot && !burst) node = Vector3.Lerp(node, Center, response * .8f);
            if (!burst)
            {
                Polygon(_marks[i], node, .105f + response * .065f, motif == 2 ? 4 : 6, i * 30f,
                    markColor, phase == 0 ? .55f : .95f, .024f + response * .02f);
                if (response > 0) Glow(_cores[i], node, Vector2.one * (.3f + response * .28f), markColor, response * .6f);
            }
            if ((motif == 1 || motif == 2) && phase >= 1 && !burst)
            {
                var next = motif == 1 ? Center : Node((i + 1) % MarkCount);
                var corner = new Vector3(next.x, node.y, node.z);
                Path(_links[i], style.HackTint, .22f + response * .65f, .02f + response * .025f, node, corner, next);
            }
        }
        if (phase >= 1 && !burst)
        {
            float lockP = phase == 1 ? Mathf.Clamp01(elapsed / style.CommandTime) : 1f;
            float radius = Mathf.Lerp(1.6f, 1.13f, lockP);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1 : 1;
                float y = i < 2 ? -1 : 1;
                Vector3 corner = Center + new Vector3(x * radius, y * radius, -.02f);
                Path(_aim[i], style.Tint, .75f, .025f, corner - Vector3.right * x * .28f, corner, corner - Vector3.up * y * .28f);
            }
        }
        if (!burst) return;

        // Each motif ruptures simultaneously but has a different silhouette and motion.
        for (int i = 0; i < MarkCount; i++)
        {
            Vector3 node = Node(i);
            Vector3 radial = (node - Center).normalized;
            float eased = 1f - Mathf.Pow(1f - p, 3);
            if (motif == 0) // Six local detonations, clearly rooted at the implanted marks.
            {
                Glow(_halos[i], node, Vector2.one * Mathf.Lerp(.16f, 1.05f, eased), style.HackTint, fade);
                Glow(_cores[i], node, Vector2.one * Mathf.Lerp(.75f, .08f, p), Color.white, fade * fade);
                for (int j = 0; j < 6; j++)
                {
                    Vector3 d = Direction(j * 60f + i * 17f);
                    Path(_rays[i * 6 + j], style.Tint, fade, .035f * fade,
                        node + d * (.14f + eased * .22f), node + d * (.25f + eased * .55f));
                }
            }
            else if (motif == 1) // Inward charge followed by one central outward pressure wave.
            {
                Vector3 d = Direction(i * 60f + 30f);
                Path(_rays[i], style.HackTint, fade, .09f * fade,
                    Center + d * (.12f + eased * .6f), Center + d * (.45f + eased * 1.5f));
                Glow(_cores[i], Center + d * eased * .7f, Vector2.one * (.85f * fade), style.Tint, fade * fade);
            }
            else if (motif == 2) // Rectilinear circuit fragments detach from the body.
            {
                Vector3 displaced = node + radial * eased * .65f;
                Polygon(_marks[i], displaced, .12f + eased * .12f, 4, 45f + p * 100f, style.HackTint, fade, .045f * fade);
                var next = Node((i + 1) % MarkCount);
                var mid = Vector3.Lerp(node, next, .5f) + radial * eased * .6f;
                Path(_links[i], Color.white, fade * fade, .045f, displaced, new Vector3(mid.x, displaced.y, mid.z), mid);
                Glow(_halos[i], node, new Vector2(.7f, .7f) * (1f + eased), style.HackTint, fade * .65f);
                for (int j = 0; j < 4; j++)
                {
                    Vector3 pos = node + Direction(i * 60f + j * 90f) * (.15f + eased * .75f);
                    Path(_rays[i * 6 + j], style.Tint, fade, .04f, pos, pos + Vector3.right * .12f);
                }
            }
            else if (motif == 3) // Jagged fractures open between the implants and burst beyond them.
            {
                Vector3 end = Center + radial * (1.05f + eased * .75f);
                Vector3 side = new Vector3(-radial.y, radial.x) * .18f;
                Path(_links[i], style.HackTint, fade, .055f * fade,
                    Center, Vector3.Lerp(Center, end, .3f) + side, Vector3.Lerp(Center, end, .5f) - side,
                    Vector3.Lerp(Center, end, .72f) + side * .5f, end);
                Glow(_cores[i], node, Vector2.one * (.7f * fade), style.Tint, fade);
                Path(_rays[i], Color.white, fade * fade, .09f * fade, node, end);
            }
            else // Six expanding petal loops, anchored to the hacked body rather than the sky.
            {
                var line = _links[i];
                Setup(line, style.HackTint, fade, .045f * fade, 25);
                Vector3 axis = Direction(i * 60f + p * 16f);
                Vector3 tangent = new Vector3(-axis.y, axis.x);
                float length = .65f + eased * 1.1f;
                for (int j = 0; j < 25; j++)
                {
                    float a = j / 24f * Mathf.PI * 2;
                    line.SetPosition(j, Center + axis * ((1f - Mathf.Cos(a)) * .5f * length)
                        + tangent * (Mathf.Sin(a) * .29f * (1f + eased)));
                }
                Glow(_cores[i], Center + axis * (.5f + eased * .7f), Vector2.one * (.65f * fade), style.Tint, fade * .85f);
            }
        }
        // The broadest motion and brightest flash always originate on the boss.
        float size = motif == 1 ? 3.3f : motif == 3 ? 2.7f : 2.25f;
        Glow(_waves[0], Center, Vector2.one * Mathf.Lerp(.25f, size, 1f - fade * fade), style.HackTint, fade * .8f);
        if (motif == 1 || motif == 4)
            Glow(_waves[1], Center, new Vector2(1f, .42f) * Mathf.Lerp(.3f, 3.6f, 1f - fade * fade), style.Tint, fade);
        if (motif == 3)
            Glow(_waves[2], Center, new Vector2(3.8f, .1f) * (1f - fade * fade), Color.white, fade * fade);
    }

    private Vector3 Node(int i)
    {
        float angle = i * 60f + 30f;
        var d = Direction(angle);
        return Center + new Vector3(d.x * .7f, d.y * .85f, 0);
    }
    private static Vector3 Direction(float degrees) => new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad), 0);
    private LineRenderer Line(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var r = go.AddComponent<LineRenderer>();
        r.sharedMaterial = _lineMaterial; r.useWorldSpace = true; r.numCapVertices = 2;
        r.sortingLayerName = "FX"; r.sortingOrder = 55;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return r;
    }
    private Renderer Quad(string name, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material;
        r.sortingLayerName = "FX"; r.sortingOrder = 54;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return r;
    }
    private static void Setup(LineRenderer line, Color color, float alpha, float width, int count)
    {
        line.enabled = alpha > .005f; color.a = alpha;
        line.startColor = line.endColor = color; line.startWidth = line.endWidth = width; line.positionCount = count;
    }
    private static void Path(LineRenderer line, Color color, float alpha, float width, params Vector3[] points)
    {
        Setup(line, color, alpha, width, points.Length); line.SetPositions(points);
    }
    private static void Polygon(LineRenderer line, Vector3 pos, float radius, int sides, float angle, Color color, float alpha, float width)
    {
        Setup(line, color, alpha, width, sides + 1);
        for (int j = 0; j <= sides; j++) line.SetPosition(j, pos + Direction(angle + j * 360f / sides) * radius);
    }
    private void Glow(Renderer r, Vector3 pos, Vector2 size, Color color, float alpha)
    {
        r.enabled = alpha > .005f; r.transform.position = pos;
        r.transform.localScale = new Vector3(size.x, size.y, 1); color.a = alpha;
        _block.SetColor("_Color", color); r.SetPropertyBlock(_block);
    }
}
