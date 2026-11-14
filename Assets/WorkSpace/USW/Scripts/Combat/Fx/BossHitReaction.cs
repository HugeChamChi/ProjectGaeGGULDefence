using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared hit reaction for every boss: white flash, squash from the feet and a short screen shake on critical hits.
/// One component with two render paths: Spine (main mesh, or the parts of a SkeletonRenderSeparator) and sprite.
/// Flash and squash are applied only while cameras render and restored right after, so Spine material setup,
/// colliders, hit areas and touch math never change.
/// Values come from <see cref="BossHitReactionProfile"/> (draft "지금 방식", outputs/damage-floater-drafts-20261114).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BossBase))]
public sealed class BossHitReaction : MonoBehaviour
{
    private const string StraightAlphaKeyword = "_STRAIGHT_ALPHA_INPUT";
    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int FillPhaseId = Shader.PropertyToID("_FillPhase");
    private static readonly int StraightAlphaId = Shader.PropertyToID("_StraightAlphaInput");
    private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
    private static readonly int StencilCompId = Shader.PropertyToID("_StencilComp");
    private static BossHitReactionProfile _sharedProfile;

    [Tooltip("Optional. Empty resolves the boss body automatically (IBossHitVisual, then Spine, then sprite).")]
    [SerializeField] private SkeletonAnimation _skeleton;
    [Tooltip("Optional. Empty uses Resources/" + BossHitReactionProfile.ResourcePath + ".")]
    [SerializeField] private BossHitReactionProfile _profile;

    private BossBase _boss;
    private bool _bound;
    private float _flash, _squash, _phase;

    private SkeletonRenderer _spine;
    private SpriteRenderer _sprite;
    private Transform _visual;
    private readonly List<Renderer> _candidates = new();
    private readonly List<Renderer> _drawn = new();
    private readonly Dictionary<Material, Material> _fills = new();
    private readonly List<List<Material>> _savedMaterials = new();
    private readonly List<Material> _flashMaterials = new();
    private bool _flashApplied, _squashApplied;
    private Vector3 _savedScale, _savedPosition;

    /// <summary>Current flash level (0..1) for checks and labs.</summary>
    public float Flash => _flash;
    /// <summary>Current squash amount (height loss fraction) for checks and labs.</summary>
    public float Squash => _squash;
    /// <summary>Camera renders drawn with the flash material (diagnostics).</summary>
    public int FlashedRenders { get; private set; }
    /// <summary>Camera renders drawn squashed (diagnostics).</summary>
    public int SquashedRenders { get; private set; }
    /// <summary>"Spine", "Sprite" or "None" once the boss body is resolved.</summary>
    public string VisualKind => _spine != null ? "Spine" : _sprite != null ? "Sprite" : "None";

    private BossHitReactionProfile Profile
    {
        get
        {
            if (_profile != null) return _profile;
            if (_sharedProfile == null) _sharedProfile = Resources.Load<BossHitReactionProfile>(BossHitReactionProfile.ResourcePath);
            return _sharedProfile;
        }
    }

    /// <summary>Adds the shared reaction to a boss once and (re)subscribes it after listener resets.</summary>
    public static BossHitReaction Attach(BossBase boss)
    {
        if (boss == null) return null;
        if (!boss.TryGetComponent(out BossHitReaction reaction)) reaction = boss.gameObject.AddComponent<BossHitReaction>();
        reaction.Bind();
        return reaction;
    }

    private void Awake() => _boss = GetComponent<BossBase>();

    private void OnEnable()
    {
        Bind();
        RenderPipelineManager.beginCameraRendering += BeginRender;
        RenderPipelineManager.endCameraRendering += EndRender;
    }

    private void OnDisable()
    {
        Unbind();
        RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
        Clear();
    }

    private void OnDestroy()
    {
        Clear();
        foreach (var fill in _fills.Values) if (fill != null) Destroy(fill);
        _fills.Clear();
    }

    /// <summary>Subscribes to boss damage. Safe to call repeatedly; BossBase.ClearListeners drops old handlers.</summary>
    public void Bind()
    {
        if (_boss == null) _boss = GetComponent<BossBase>();
        if (_boss == null || !isActiveAndEnabled) return;
        _boss.OnDamageDealt -= OnDamageDealt;
        _boss.OnDamageDealt += OnDamageDealt;
        _boss.OnDeath -= Clear;
        _boss.OnDeath += Clear;
        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound || _boss == null) return;
        _boss.OnDamageDealt -= OnDamageDealt;
        _boss.OnDeath -= Clear;
        _bound = false;
    }

    /// <summary>Critical-strength reaction for damage effects that confirm a heavy hit.</summary>
    public void HeavyImpact(Vector3 hitPosition) => React(true, false);

    private void OnDamageDealt(decimal damage, Vector3? position, BossDamageKind kind)
    {
        if (damage <= 0m || _boss == null || _boss.IsDead) return;
        var profile = Profile;
        if (profile == null || (kind == BossDamageKind.Burn && !profile.ReactToBurn)) return;
        bool strong = kind == BossDamageKind.Critical || kind == BossDamageKind.Rally;
        React(strong, strong);
    }

    private void React(bool strong, bool shake)
    {
        var profile = Profile;
        if (profile == null || !isActiveAndEnabled || _boss == null || _boss.IsDead) return;
        _flash = Mathf.Max(_flash, strong ? profile.CriticalFlash : profile.NormalFlash);
        _squash = Mathf.Max(_squash, strong ? profile.CriticalSquash : profile.NormalSquash);
        if (shake) Shake(profile);
    }

    private static void Shake(BossHitReactionProfile profile)
    {
        var camera = Camera.main;
        if (camera == null || profile.CriticalShakeSeconds <= 0f || profile.CriticalShakeScreenFraction <= 0f) return;
        float screenHeight = camera.orthographic ? camera.orthographicSize * 2f : 10f;
        var shake = camera.GetComponent<BattleCameraShake>() ?? camera.gameObject.AddComponent<BattleCameraShake>();
        shake.Play(profile.CriticalShakeSeconds, profile.CriticalShakeScreenFraction * screenHeight);
    }

    private void LateUpdate()
    {
        var profile = Profile;
        if (profile == null) return;
        float dt = Time.deltaTime;
        _flash = Mathf.Max(0f, _flash - dt * (_flash > profile.FlashDecayThreshold ? profile.FlashFastDecay : profile.FlashSlowDecay));
        _squash = Mathf.Max(0f, _squash - dt * profile.SquashLinearDecay);
        _squash *= Mathf.Pow(profile.SquashRemainPerSecond, dt);
        if (_squash < .00001f) _squash = 0f;
        _phase = profile.FillShader == null || (_boss != null && _boss.IsDead) ? 0f : Mathf.Clamp01(_flash * profile.FlashOpacity);
    }

    // ── Visual resolution ──────────────────────────────────────────
    private bool ResolveVisual()
    {
        if (_visual != null) return true;
        _spine = null; _sprite = null; _candidates.Clear();
        bool hinted = TryGetComponent(out IBossHitVisual hint);
        if (hinted) _spine = hint.HitSkeleton;
        if (_spine == null && _skeleton != null) _spine = _skeleton.GetComponent<SkeletonRenderer>();
        if (_spine == null && !hinted) _spine = GetComponentInChildren<SkeletonRenderer>(true);
        if (_spine != null)
        {
            _visual = _spine.transform;
            if (_spine.TryGetComponent(out MeshRenderer main)) _candidates.Add(main);
            foreach (var part in _spine.GetComponentsInChildren<SkeletonPartsRenderer>(true))
                if (part.TryGetComponent(out MeshRenderer partRenderer)) _candidates.Add(partRenderer);
            return true;
        }
        if (hinted) return false; // body is created later (for example Deertree)
        _sprite = GetComponentInChildren<SpriteRenderer>(true);
        if (_sprite == null) return false;
        _visual = _sprite.transform;
        _candidates.Add(_sprite);
        return true;
    }

    /// <summary>Renderers that actually draw this frame (separator parts replace the main Spine mesh).</summary>
    private bool CollectDrawn()
    {
        _drawn.Clear();
        foreach (var renderer in _candidates)
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) _drawn.Add(renderer);
        return _drawn.Count > 0;
    }

    // ── Render-time flash and squash ───────────────────────────────
    private void BeginRender(ScriptableRenderContext context, Camera camera)
    {
        if ((_squash <= 0f && _phase <= 0f) || _flashApplied || _squashApplied) return;
        var profile = Profile;
        if (profile == null || !ResolveVisual() || !CollectDrawn()) return;
        if (_phase > 0f) BeginFlash(profile);
        if (_squash > 0f) BeginSquash(profile);
    }

    private void EndRender(ScriptableRenderContext context, Camera camera)
    {
        RestoreFlash();
        RestoreSquash();
    }

    private void BeginFlash(BossHitReactionProfile profile)
    {
        while (_savedMaterials.Count < _drawn.Count) _savedMaterials.Add(new List<Material>());
        for (int r = 0; r < _drawn.Count; r++)
        {
            var saved = _savedMaterials[r];
            _drawn[r].GetSharedMaterials(saved);
            _flashMaterials.Clear();
            foreach (var material in saved)
            {
                var fill = material != null ? GetFill(material, profile, _sprite != null) : null;
                if (fill != null)
                {
                    fill.SetColor(FillColorId, profile.FlashColor);
                    fill.SetFloat(FillPhaseId, _phase);
                }
                _flashMaterials.Add(fill != null ? fill : material);
            }
            _drawn[r].SetSharedMaterials(_flashMaterials);
        }
        _flashApplied = true;
        FlashedRenders++;
    }

    private Material GetFill(Material source, BossHitReactionProfile profile, bool straightAlpha)
    {
        if (_fills.TryGetValue(source, out var fill) && fill != null) return fill;
        fill = new Material(profile.FillShader) { name = source.name + " (Hit Flash)", renderQueue = source.renderQueue };
        if (source.HasProperty("_MainTex")) fill.mainTexture = source.mainTexture;
        bool straight = straightAlpha || source.IsKeywordEnabled(StraightAlphaKeyword)
            || (source.HasProperty(StraightAlphaId) && source.GetFloat(StraightAlphaId) > .5f);
        fill.SetFloat(StraightAlphaId, straight ? 1f : 0f);
        if (straight) fill.EnableKeyword(StraightAlphaKeyword); else fill.DisableKeyword(StraightAlphaKeyword);
        if (source.HasProperty(StencilRefId)) fill.SetFloat(StencilRefId, source.GetFloat(StencilRefId));
        if (source.HasProperty(StencilCompId)) fill.SetFloat(StencilCompId, source.GetFloat(StencilCompId));
        _fills[source] = fill;
        return fill;
    }

    private void RestoreFlash()
    {
        if (!_flashApplied) return;
        for (int r = 0; r < _drawn.Count && r < _savedMaterials.Count; r++)
            if (_drawn[r] != null) _drawn[r].SetSharedMaterials(_savedMaterials[r]);
        _flashApplied = false;
    }

    private void BeginSquash(BossHitReactionProfile profile)
    {
        var bounds = _drawn[0].bounds;
        for (int r = 1; r < _drawn.Count; r++) bounds.Encapsulate(_drawn[r].bounds);
        var pivot = new Vector3(bounds.center.x, bounds.min.y, _visual.position.z);
        var factor = new Vector3(1f + _squash * profile.SquashWidthRatio, 1f - _squash, 1f);
        _savedScale = _visual.localScale;
        _savedPosition = _visual.position;
        _visual.localScale = Vector3.Scale(_savedScale, factor);
        _visual.position = pivot + Vector3.Scale(_savedPosition - pivot, factor);
        _squashApplied = true;
        SquashedRenders++;
    }

    private void RestoreSquash()
    {
        if (!_squashApplied) return;
        if (_visual != null)
        {
            _visual.localScale = _savedScale;
            _visual.position = _savedPosition;
        }
        _squashApplied = false;
    }

    private void Clear()
    {
        _flash = _squash = _phase = 0f;
        RestoreFlash();
        RestoreSquash();
    }
}
