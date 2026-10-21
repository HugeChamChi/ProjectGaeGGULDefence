using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Samples authored Spine poses on the pattern clock and batches the ground/heal/slow feedback into one mesh.</summary>
public sealed class LizardBossPresentation : MonoBehaviour, IPauseIdleVisual
{
    [SerializeField] private SkeletonAnimation _skeleton;
    [SerializeField] private SkeletonRenderSeparator _separator;
    [SerializeField] private Material _effectsMaterial;
    [SerializeField] private Vector3 _groundOffset;
    [SerializeField] private string _idleAnimation = "idle";
    private const int RingSegments = 40;
    private const int HealingCrosses = 7;
    private const int DustPieces = 12;
    private const float EffectLifetime = 1.15f;
    private const float CrossSize = 0.12f;
    private static readonly Color ThreatColor = new Color(1f, 0.32f, 0.16f, 0.8f);
    private static readonly Color HealColor = new Color(0.35f, 1f, 0.55f, 0.95f);
    private static readonly Color DirtColor = new Color(0.68f, 0.5f, 0.3f, 0.85f);
    private readonly List<Vector3> _vertices = new(512);
    private readonly List<Color> _colors = new(512);
    private readonly List<int> _triangles = new(768);
    private readonly List<Vector2> _uv = new(512);
    private TrackEntry _track;
    private LizardPatternData _pattern;
    private SkeletonRenderer _renderer;
    private Spine.Slot[] _spikeSlots;
    private Mesh _mesh;
    private bool _pauseIdle;
    private float _idleTime;

    /// <summary>Current native Spine time, exposed for synchronization diagnostics.</summary>
    public float PoseTime => _track?.TrackTime ?? 0f;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (_renderer != null || _skeleton == null) return;
        _skeleton.Initialize(false);
        _skeleton.enabled = false; // Sampling is owned by the combat clock, including pose holds.
        _renderer = _skeleton.GetComponent<SkeletonRenderer>();
        _spikeSlots = new[] {
            _skeleton.Skeleton.FindSlot("groundspike_02_L"), _skeleton.Skeleton.FindSlot("groundspike_02_R"),
            _skeleton.Skeleton.FindSlot("groundspike_01_L"), _skeleton.Skeleton.FindSlot("groundspike_01_R"),
            _skeleton.Skeleton.FindSlot("groundspike_M") };
        var go = new GameObject("Lizard pattern feedback");
        go.transform.SetParent(transform, false);
        _mesh = new Mesh { name = "Lizard batched feedback" };
        _mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _effectsMaterial;
        renderer.sortingLayerName = "FX";
        renderer.sortingOrder = 0;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Play(_idleAnimation, true);
    }

    private void Play(string clip, bool loop)
    {
        _skeleton.AnimationState.ClearTracks();
        _skeleton.Skeleton.SetupPose();
        _track = _skeleton.AnimationState.SetAnimation(0, clip, loop);
        _track.MixDuration = 0f;
    }

    /// <summary>Starts one authored Spine clip without an independent animation timer.</summary>
    public void Begin(LizardPatternData pattern)
    {
        Initialize();
        _pattern = pattern;
        if (_skeleton == null) return;
        Play(pattern.SpineAnimation, false);
        Sample(pattern, 0f);
    }

    /// <summary>Holds the native middle pose, then resumes the original exit frames at their authored speed.</summary>
    public void Sample(LizardPatternData pattern, float elapsed)
    {
        if (_skeleton == null || _track == null) return;
        float time = elapsed;
        bool holding = pattern.ReleaseAt > pattern.HoldAt && elapsed >= pattern.HoldAt && elapsed < pattern.ReleaseAt;
        if (pattern.ReleaseAt > pattern.HoldAt && elapsed >= pattern.HoldAt)
            time = holding ? pattern.HoldAt : pattern.ResumeAt + elapsed - pattern.ReleaseAt;
        _track.TrackTime = Mathf.Clamp(time, 0f, _track.Animation.Duration);
        ApplyPose(pattern.HideWhileHeld && holding,
            pattern.patternType == BossPatternType.LizardThornBarrier,
            pattern.patternType == BossPatternType.LizardBurrow);
    }

    private void ApplyPose(bool hidden, bool spikes, bool burrowing)
    {
        _skeleton.Update(0f);
        _skeleton.Skeleton.SetColor(1f, 1f, 1f, hidden ? 0f : 1f);
        if (!spikes)
            foreach (var slot in _spikeSlots)
                if (slot != null) { slot.Pose.Attachment = null; slot.AppliedPose.Attachment = null; }
        if (_separator != null && _separator.partsRenderers.Count >= 2)
        {
            // The body sinks behind the board; hands/spikes normally remain in front.
            _separator.partsRenderers[1].MeshRenderer.sortingLayerName = burrowing ? "Default" : "Unit";
            _separator.partsRenderers[1].MeshRenderer.sortingOrder = burrowing ? -10 : 0;
        }
        _renderer.UpdateMesh();
    }

    /// <summary>Returns to the installed idle, with temporary spike attachments and hidden color cleared.</summary>
    public void End()
    {
        _pattern = null;
        if (_renderer == null) return;
        _idleTime = 0f;
        Play(_idleAnimation, true);
        ApplyPose(false, false, false);
    }

    /// <inheritdoc/>
    public void SetPauseIdle(bool on) => _pauseIdle = on;

    private void Update()
    {
        if (_renderer == null || _pattern != null || _track == null) return;
        _idleTime += _pauseIdle ? Time.unscaledDeltaTime : Time.deltaTime;
        _track.TrackTime = _idleTime;
        ApplyPose(false, false, false);
    }

    /// <summary>Builds entry dust, a burrow opening, rising recovery crosses and persistent unit slow markers.</summary>
    public void RenderEffects(LizardPatternData pattern, float elapsed, IReadOnlyList<UnitBase> slowed, float slowRatio)
    {
        if (_mesh == null) return;
        _vertices.Clear(); _colors.Clear(); _triangles.Clear(); _uv.Clear();
        Vector3 ground = transform.TransformPoint(_groundOffset);
        if (pattern != null)
        {
            if (pattern.patternType == BossPatternType.LizardThreat)
            {
                float pulse = (elapsed - pattern.ImpactTimeSec) / EffectLifetime;
                if (pulse >= 0f && pulse < 1f)
                    Ring(ground + Vector3.up * 2.6f, new Vector2(1f + pulse * 2.4f, .5f + pulse), .045f,
                        Fade(ThreatColor, 1f-pulse));
            }
            else
            {
                Dust(ground, elapsed);
                if (pattern.patternType == BossPatternType.LizardBurrow)
                {
                    if (elapsed < pattern.ReleaseAt)
                    {
                        Ring(ground, new Vector2(1.05f,.24f), .16f, new Color(.21f,.15f,.12f,.95f));
                        Ring(ground, new Vector2(1.15f,.3f), .065f, DirtColor);
                    }
                    float recovery = elapsed - pattern.HealDelay;
                    if (recovery >= 0f && recovery < EffectLifetime)
                        for (int i=0;i<HealingCrosses;i++)
                        {
                            float t = Mathf.Clamp01(recovery/EffectLifetime);
                            float x = Mathf.Sin(i * 2.4f) * .85f;
                            var p = ground + new Vector3(x, .35f + t * (1.3f + (i%3)*.35f), 0f);
                            Color color=Fade(HealColor, Mathf.Sin(t*Mathf.PI));
                            Quad(p,new Vector2(CrossSize*.45f,CrossSize*1.4f),color);
                            Quad(p,new Vector2(CrossSize*1.4f,CrossSize*.45f),color);
                        }
                }
                if (elapsed >= pattern.ReleaseAt) Dust(ground, elapsed-pattern.ReleaseAt);
            }
        }
        if (slowRatio > 0f && slowed != null)
            foreach (var unit in slowed)
            {
                if (unit == null || unit.currentCell == null) continue;
                Vector3 p=unit.transform.position + Vector3.up*.7f;
                Color color=Fade(ThreatColor,Mathf.Clamp01(slowRatio*5f));
                Quad(p,new Vector2(.045f,.14f),color);
                Triangle(p+new Vector3(-.14f,-.05f),p+new Vector3(.14f,-.05f),p+new Vector3(0f,-.25f),color);
            }
        _mesh.Clear();
        _mesh.SetVertices(_vertices);_mesh.SetColors(_colors);_mesh.SetUVs(0,_uv);
        _mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();
    }

    private void Dust(Vector3 ground,float elapsed)
    {
        if (elapsed < 0f || elapsed >= EffectLifetime) return;
        float t=elapsed/EffectLifetime;
        for(int i=0;i<DustPieces;i++)
        {
            float sign=i%2==0?-1f:1f;
            Vector3 p=ground+new Vector3(sign*(.2f+(i%6)*.14f+t*.8f),Mathf.Sin(t*Mathf.PI)*(.25f+(i%3)*.2f),0f);
            Quad(p,Vector2.one*(.055f+(i%3)*.02f),Fade(DirtColor,1f-t));
        }
    }

    private static Color Fade(Color color,float alpha) { color.a*=alpha;return color; }
    private void Quad(Vector3 p,Vector2 half,Color c)
    {
        Vector3 a=p+new Vector3(-half.x,-half.y),b=p+new Vector3(-half.x,half.y),d=p+new Vector3(half.x,-half.y),e=p+new Vector3(half.x,half.y);
        Triangle(a,b,e,c);Triangle(a,e,d,c);
    }
    private void Ring(Vector3 p,Vector2 radius,float width,Color c)
    {
        for(int i=0;i<RingSegments;i++)
        {
            float a=i*Mathf.PI*2f/RingSegments,b=(i+1)*Mathf.PI*2f/RingSegments;
            Vector3 v1=p+new Vector3(Mathf.Cos(a)*radius.x,Mathf.Sin(a)*radius.y),v2=p+new Vector3(Mathf.Cos(b)*radius.x,Mathf.Sin(b)*radius.y);
            Vector3 v3=p+new Vector3(Mathf.Cos(a)*(radius.x-width),Mathf.Sin(a)*(radius.y-width*.4f)),v4=p+new Vector3(Mathf.Cos(b)*(radius.x-width),Mathf.Sin(b)*(radius.y-width*.4f));
            Triangle(v1,v2,v4,c);Triangle(v1,v4,v3,c);
        }
    }
    private void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
    {
        int first=_vertices.Count;
        _vertices.Add(transform.InverseTransformPoint(a));_vertices.Add(transform.InverseTransformPoint(b));_vertices.Add(transform.InverseTransformPoint(c));
        for(int i=0;i<3;i++){_colors.Add(color);_uv.Add(Vector2.zero);_triangles.Add(first+i);}
    }

    /// <summary>Releases feedback geometry while leaving the authored materials owned by assets.</summary>
    public void ClearEffects() { if (_mesh != null) _mesh.Clear(); }
    private void OnDestroy()
    {
        if (_mesh == null) return;
        if (Application.isPlaying) Destroy(_mesh);
        else DestroyImmediate(_mesh);
    }
}
