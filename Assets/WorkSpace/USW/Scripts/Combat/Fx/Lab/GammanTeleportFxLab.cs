using Spine.Unity;
using UnityEngine;

/// <summary>Five visual-only teleport attack choreographies, without boss hacking decorations.</summary>
public sealed class GammanTeleportFxLab : MonoBehaviour, IFxLabPlayable
{
    [SerializeField] private GammanTeleportTake[] _takes;
    [SerializeField] private SpriteRenderer _drone;
    [SerializeField] private Sprite _fireSprite;
    [SerializeField] private SkeletonAnimation _gamman;
    [SerializeField] private Transform _target;
    [SerializeField] private Material _beamMaterial;
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private Vector3 _home = new Vector3(-.913f, -1.894f, -.4f);
    [SerializeField] private bool _auto = true;
    [SerializeField] private GammanTeleportGroupLab _group;
    [SerializeField] private Vector3 _airOffset;
    private Sprite _idleSprite;
    private SpriteRenderer _ghost;
    private Renderer _beam, _charge, _hit;
    private readonly LineRenderer[] _streaks = new LineRenderer[8];
    private MaterialPropertyBlock _block;
    private int _take, _beat;
    private float _elapsed;
    private bool _running;
    private GammanTeleportTake Take => _takes[_take];
    private GammanTeleportTake.Beat Beat => Take.Beats[_beat];

    /// <inheritdoc/>
    public bool UseUnscaledTime { get; set; }
    /// <summary>Current choreography index.</summary>
    public int CurrentTake => _take;
    /// <summary>Current motion beat.</summary>
    public int CurrentBeat => _beat;
    /// <summary>Current beat label for reproducible capture.</summary>
    public string CurrentPhase => Beat.Label;
    /// <summary>Current attack beam visibility.</summary>
    public bool IsFiring => _beam != null && _beam.enabled;
    /// <summary>True after the complete take, including returning home.</summary>
    public bool Finished => !_running;
    /// <summary>Horizontal travel multiplier used by the crowd comparison to remain in the portrait frame.</summary>
    public float AirMotionScale { get; set; } = 1f;

    private void Awake()
    {
        _idleSprite = _drone.sprite;
        _block = new MaterialPropertyBlock();
        _beam = Quad("AttackBeam", _beamMaterial, 30);
        _charge = Quad("CarriedCharge", _glowMaterial, 35);
        _hit = Quad("SmallContact", _glowMaterial, 36);
        _ghost = new GameObject("HomeAfterimage").AddComponent<SpriteRenderer>();
        _ghost.transform.SetParent(transform, false);
        _ghost.sprite = _idleSprite; _ghost.sortingLayerName = "FX"; _ghost.sortingOrder = 32;
        for (int i = 0; i < _streaks.Length; i++)
        {
            var go = new GameObject("TeleportSpeedLine_" + i); go.transform.SetParent(transform, false);
            var r = go.AddComponent<LineRenderer>();
            r.sharedMaterial = _lineMaterial; r.useWorldSpace = true; r.positionCount = 2;
            r.sortingLayerName = "FX"; r.sortingOrder = 38;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _streaks[i] = r;
        }
        Stop();
    }
    private void Start() { if (_group == null) Play(); }
    private void OnDisable() => Stop();
    /// <inheritdoc/>
    public void Play() => Select(_take);
    /// <summary>Restart one take and immediately clear every previous attack visual.</summary>
    public void Select(int index)
    {
        if (_group != null) { _group.Select(index); return; }
        BeginTake(index, 0);
    }
    /// <summary>Start an individual visual timeline with a comparison-only delay.</summary>
    public void BeginTake(int index, float delay)
    {
        Stop(); _take = Mathf.Clamp(index, 0, _takes.Length - 1); _beat = 0; _elapsed = -delay;
        _gamman.Initialize(false); _gamman.AnimationState.SetAnimation(0, "idle", true);
        _running = true; Draw();
    }
    /// <summary>Stop presentation and restore the drone beside Gamman.</summary>
    public void Stop()
    {
        _running = false;
        if (_beam == null) return;
        _beam.enabled = _charge.enabled = _hit.enabled = _ghost.enabled = false;
        foreach (var line in _streaks) line.enabled = false;
        _drone.sprite = _idleSprite;
        Pose(_drone, _home, false, 1f, 0);
    }
    private void Update()
    {
        if (!_running) return;
        _elapsed += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (_elapsed >= Beat.Duration)
        {
            _elapsed = 0; _beat++;
            if (_beat >= Take.Beats.Length)
            {
                if (_group != null) { _beat = Take.Beats.Length - 1; Stop(); }
                else Select(_auto ? (_take + 1) % _takes.Length : _take);
                return;
            }
            if (_beat == 1)
            {
                _gamman.AnimationState.SetAnimation(0, "skill", false);
                _gamman.AnimationState.AddAnimation(0, "idle", true, 0);
            }
        }
        Draw();
    }
    private void Draw()
    {
        var b = Beat;
        float p = Mathf.Clamp01(_elapsed / Mathf.Max(.01f, b.Duration));
        Vector3 motion = Vector3.Lerp(b.From, b.To, 1f - Mathf.Pow(1f - p, 2));
        motion.x *= AirMotionScale;
        Vector3 pos = b.AtHome ? _home : _target.position + _airOffset + motion;
        _drone.sprite = b.Fire ? _fireSprite : _idleSprite;
        Pose(_drone, pos, !b.AtHome, 1f, b.Squeeze * p);
        _drone.enabled = b.Body;
        float ghostAlpha = .4f * (b.Fire ? 1f - p : 1f);
        Pose(_ghost, _home, false, ghostAlpha, 0);
        _ghost.color = new Color(.85f, .94f, 1f, ghostAlpha); _ghost.enabled = b.HomeGhost;
        Vector3 aim = (_target.position - pos).normalized;
        Vector3 muzzle = pos + (b.AtHome ? Vector3.left : aim) * .29f;
        float charge = b.Fire ? .3f : b.Charge * (.3f + .7f * p);
        Glow(_charge, muzzle, .24f + charge * .34f, charge, Take.Tint);
        _charge.enabled &= b.Body;
        _beam.enabled = b.Fire;
        _hit.enabled = b.Fire;
        if (b.Fire)
        {
            Vector3 end = Vector3.Lerp(muzzle, _target.position, Mathf.Clamp01(_elapsed / .025f));
            Vector3 d = muzzle - end;
            _beam.transform.position = (muzzle + end) * .5f;
            _beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            _beam.transform.localScale = new Vector3(Take.BeamWidth * (1f - .65f * Mathf.Pow(p, 8)), d.magnitude, 1);
            _block.Clear(); _block.SetColor("_Color", Take.Tint); _block.SetFloat("_Alpha", 1f);
            _block.SetFloat("_Length", d.magnitude); _block.SetFloat("_Seed", 0); _beam.SetPropertyBlock(_block);
            Glow(_hit, _target.position + Vector3.back * .1f, .42f, .8f, Take.Tint);
        }
        for (int i = 0; i < _streaks.Length; i++)
        {
            float snapP = Mathf.Clamp01(_elapsed / .085f);
            var r = _streaks[i]; r.enabled = b.Snap && snapP < 1f;
            float x = (i - 3.5f) * .09f;
            float length = .4f + (i % 3) * .25f;
            Vector3 center = pos + new Vector3(x, (i % 2 == 0 ? 1 : -1) * snapP * .35f, -.08f);
            r.SetPosition(0, center - Vector3.up * length * .5f);
            r.SetPosition(1, center + Vector3.up * length * .5f);
            r.startWidth = .024f * (1f - snapP); r.endWidth = .003f;
            Color c = Color.Lerp(Take.Tint, Color.white, .75f); c.a = 1f - snapP;
            r.startColor = r.endColor = c;
        }
    }
    private void Pose(SpriteRenderer r, Vector3 center, bool aim, float alpha, float squeeze)
    {
        Vector3 d = _target.position - center;
        var rotation = aim ? Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 180f) : Quaternion.identity;
        var scale = new Vector3(1.6f * (1f - squeeze * .75f), 1.6f * (1f + squeeze * .25f), 1.6f);
        r.transform.SetPositionAndRotation(center - rotation * Vector3.Scale(r.sprite.bounds.center, scale), rotation);
        r.transform.localScale = scale; r.color = new Color(1, 1, 1, alpha); r.enabled = true;
    }
    private Renderer Quad(string name, Material material, int order)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.sortingLayerName = "FX"; r.sortingOrder = order;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return r;
    }
    private void Glow(Renderer r, Vector3 pos, float size, float alpha, Color color)
    {
        r.enabled = alpha > .01f; r.transform.position = pos; r.transform.localScale = Vector3.one * size;
        _block.Clear(); color.a = alpha; _block.SetColor("_Color", color); r.SetPropertyBlock(_block);
    }
    private void OnGUI()
    {
        if (_group != null) return;
        float s = Mathf.Max(.6f, Screen.width / 540f); var old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float w = Screen.width / s;
        GUI.Box(new Rect(10, 10, w - 20, 177), "GAMMAN / TELEPORT CHOREOGRAPHY");
        GUI.Label(new Rect(24, 37, w - 48, 24), Take.Label + " / " + CurrentPhase);
        GUI.Label(new Rect(24, 61, w - 48, 24), Take.Description);
        float bw = (w - 56) / 3;
        for (int i = 0; i < _takes.Length; i++)
            if (GUI.Button(new Rect(24 + i % 3 * (bw + 4), 94 + i / 3 * 40, bw, 34), _takes[i].Label)) { _auto = false; Select(i); }
        if (GUI.Button(new Rect(24 + 2 * (bw + 4), 134, bw, 34), _auto ? "AUTO: ON" : "AUTO: OFF")) _auto = !_auto;
        GUI.matrix = old;
    }
}
