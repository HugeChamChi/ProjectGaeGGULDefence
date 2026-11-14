using System.Collections.Generic;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Deertree visuals and field taps in the real battle scene: boss pose, apple branch, roots, bindings and the root sale badge.
/// Poses are sampled on the combat clock by <see cref="BossDeertree"/>, the same way the approved lab samples them.
/// </summary>
public sealed class DeertreeBossPresentation : MonoBehaviour, IBossHitVisual
{
    private const string BossLayer = "Default", EffectLayer = "FX"; // FX draws above every unit row
    private const int BossOrder = -10, RootOrder = 40, VineOrder = 41, BranchOrder = 60, SellOrder = 70;
    private const int AppleCount = 4;
    private static readonly Plane GroundPlane = new Plane(Vector3.forward, Vector3.zero);

    [SerializeField] private DeertreeBossSettings _settings;
    private BossDeertree _host;
    private DeertreeEncounter _encounter;
    private Camera _camera;
    private Transform _effects;
    private SkeletonAnimation _boss, _branch;
    private SkeletonRenderer _bossRenderer, _branchRenderer;
    /// <inheritdoc />
    public SkeletonRenderer HitSkeleton => _bossRenderer;
    private TrackEntry _bossTrack;
    private DeertreeEncounter.Pattern _bossPattern;
    private float _bossAge, _clock;
    private bool _bossReturning;
    private SkeletonAnimation[] _roots, _vines;
    private DeertreeSpineHold[] _rootHolds, _vineHolds;
    private Material[] _vineMaterials;
    private float[] _vineFlash;
    private readonly Slot[] _appleSlots = new Slot[AppleCount];
    private readonly Material[] _appleMaterials = new Material[AppleCount];
    private readonly float[] _appleFlash = new float[AppleCount];
    private readonly Dictionary<Material, Material> _materials = new();
    private readonly float[] _vertices = new float[2048];
    private float[] _boundsBuffer;
    private GameObject _sellBadge;
    private Sprite _badgeSprite;
    private int _lastSampleFrame = -10;
    private PointerEventData _uiPointer;
    private EventSystem _uiEventSystem;
    private readonly List<RaycastResult> _uiHits = new();

    private void Awake()
    {
        if (_settings == null) return;
        _effects = new GameObject("Deertree effects").transform;
        Vector3 center = transform.position + (Vector3)_settings.BossCenter;
        _boss = CreateSpine(_settings.Boss, "Deertree", transform, center, _settings.BossWidth, BossLayer, BossOrder);
        _bossRenderer = _boss.GetComponent<SkeletonRenderer>();
        SetBossClip("idle", DeertreeEncounter.Pattern.None);
    }

    /// <summary>Connects one registered encounter. Cell effects are created as its state asks for them.</summary>
    public void Begin(BossDeertree host, DeertreeBossSettings settings, DeertreeEncounter encounter)
    {
        End();
        if (_boss == null) return;
        _host = host; _encounter = encounter; _settings = settings;
        _camera = Camera.main;
        int count = host.Cells.Count;
        _roots = new SkeletonAnimation[count]; _vines = new SkeletonAnimation[count];
        _rootHolds = new DeertreeSpineHold[count]; _vineHolds = new DeertreeSpineHold[count];
        _vineMaterials = new Material[count]; _vineFlash = new float[count];
        if (_branch == null) CreateBranch();
    }

    private void CreateBranch()
    {
        Vector3 center = transform.position + (Vector3)_settings.BranchCenter;
        _branch = CreateSpine(_settings.Branch, "Apple branch", _effects, center, _settings.BranchWidth, EffectLayer, BranchOrder);
        _branch.enabled = false;
        _branchRenderer = _branch.GetComponent<SkeletonRenderer>();
        _branch.AnimationState.SetAnimation(0, "apple_branch", false).TrackTime = _settings.BranchHoldAt;
        _branch.Update(0);
        var source = _branch.GetComponent<MeshRenderer>().sharedMaterial;
        for (int i = 0; i < AppleCount; i++)
        {
            _appleSlots[i] = _branch.Skeleton.FindSlot("apple_0" + (i + 1));
            _appleMaterials[i] = CreateTouchMaterial(source.mainTexture, _settings.AppleGlow, i * 1.7f, "Apple natural highlight " + i);
            _branchRenderer.CustomSlotMaterials[_appleSlots[i]] = _appleMaterials[i];
        }
        _branch.gameObject.SetActive(false);
    }

    private SkeletonAnimation CreateSpine(SkeletonDataAsset asset, string label, Transform parent, Vector3 center, float width, string layer, int order)
    {
        var spine = SkeletonAnimation.NewSkeletonAnimationGameObject(asset).skeletonAnimation;
        spine.name = label; spine.transform.SetParent(parent, false);
        spine.Initialize(false);
        spine.Update(0);
        var skeletonRenderer = spine.GetComponent<SkeletonRenderer>();
        skeletonRenderer.UpdateMesh();
        var meshRenderer = spine.GetComponent<MeshRenderer>();
        foreach (var original in meshRenderer.sharedMaterials)
        {
            // The installed textures are straight alpha; the shared import materials are left untouched.
            if (!_materials.TryGetValue(original, out var corrected))
            {
                corrected = new Material(original) { name = original.name + " (Straight Alpha)" };
                corrected.SetFloat("_StraightAlphaInput", 1f); corrected.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                _materials.Add(original, corrected);
            }
            skeletonRenderer.CustomMaterialOverride[original] = corrected;
        }
        skeletonRenderer.UpdateMesh();
        spine.Skeleton.GetBounds(out float x, out float y, out float w, out float h, ref _boundsBuffer);
        float scale = width / Mathf.Max(.01f, w);
        float parentScale = Mathf.Max(.0001f, Mathf.Abs(parent.lossyScale.x));
        spine.transform.localScale = Vector3.one * (scale / parentScale);
        spine.transform.position = center - new Vector3((x + w / 2) * scale, (y + h / 2) * scale, 0);
        meshRenderer.sortingLayerName = layer; meshRenderer.sortingOrder = order;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; meshRenderer.receiveShadows = false;
        return spine;
    }

    /// <summary>Touch-hint material that keeps the authored colours (apples and bindings share it).</summary>
    private Material CreateTouchMaterial(Texture texture, float strength, float phase, string label)
    {
        var material = new Material(_settings.AppleShader) { name = label, mainTexture = texture };
        material.SetColor("_Highlight", _settings.AppleHighlight);
        material.SetFloat("_Strength", strength);
        material.SetFloat("_Phase", phase);
        return material;
    }

    /// <summary>Encounter cast start: the boss plays the matching skill clip from its first frame.</summary>
    public void Cast(DeertreeEncounter.Pattern pattern)
    {
        if (_boss == null) return;
        SetBossClip(pattern == DeertreeEncounter.Pattern.Root ? "skill_01" : pattern == DeertreeEncounter.Pattern.Fruit ? "skill_02" : "skill_03", pattern);
    }

    /// <summary>Encounter impact: the apple branch starts its authored entrance.</summary>
    public void Impact(DeertreeEncounter.Pattern pattern)
    {
        if (pattern != DeertreeEncounter.Pattern.Fruit || _branch == null) return;
        _branch.gameObject.SetActive(true);
        _branch.AnimationState.ClearTracks(); _branch.Skeleton.SetupPose();
        _branch.AnimationState.SetAnimation(0, "apple_branch", false);
    }

    private void SetBossClip(string clip, DeertreeEncounter.Pattern pattern)
    {
        _bossPattern = pattern; _bossAge = 0; _bossReturning = false;
        _boss.AnimationState.ClearTracks(); _boss.Skeleton.SetupPose();
        _bossTrack = _boss.AnimationState.SetAnimation(0, clip, pattern == DeertreeEncounter.Pattern.None);
        _bossTrack.MixDuration = 0;
        // Idle runs by itself; skill clips are sampled on the combat clock so pauses hold the pose.
        _boss.enabled = pattern == DeertreeEncounter.Pattern.None;
    }

    private void SampleBoss(float delta)
    {
        if (_bossPattern == DeertreeEncounter.Pattern.None) return;
        _bossAge += delta;
        float poseTime = _bossAge;
        bool fruit = _bossPattern == DeertreeEncounter.Pattern.Fruit;
        bool curse = _bossPattern == DeertreeEncounter.Pattern.Curse;
        bool holding = fruit && (_encounter.Casting == DeertreeEncounter.Pattern.Fruit || _encounter.FruitEntering || _encounter.FruitActive)
            || curse && (_encounter.CurseActive || _encounter.Casting == DeertreeEncounter.Pattern.Curse);
        if (fruit || curse)
        {
            float hold = fruit ? _settings.FruitBossHoldAt : _settings.CurseBossHoldAt;
            if (holding && _bossAge >= hold)
            {
                // A cosine ping-pong reverses gently at both ends of each authored interaction segment.
                float period = fruit ? _settings.FruitBossLoopPeriod : _settings.CurseBossLoopPeriod;
                float end = fruit ? _settings.FruitBossLoopEnd : _settings.CurseBossLoopEnd;
                float phase = (_bossAge - hold) / period;
                float blend = .5f - .5f * Mathf.Cos(phase * Mathf.PI * 2);
                poseTime = Mathf.Lerp(hold, end, blend);
            }
            else if (holding) poseTime = _bossAge = Mathf.Min(_bossAge, hold);
            else if (!_bossReturning)
            {
                _bossReturning = true;
                poseTime = _bossAge = fruit ? _settings.FruitBossReturnAt : _settings.CurseBossReturnAt;
                // Mix from the current loop pose, including a cycle that ends between loop boundaries.
                _bossTrack = _boss.AnimationState.SetAnimation(0, _bossTrack.Animation, false);
                _bossTrack.MixDuration = fruit ? _settings.FruitBossReturnBlend : _settings.CurseBossReturnBlend;
            }
            if (fruit && _encounter.Returning)
                poseTime = _bossAge = Mathf.Lerp(_settings.FruitBossReturnAt, _bossTrack.Animation.Duration, 1 - _encounter.ReturnRemaining / _settings.BranchReturnDuration);
        }
        if (!holding && poseTime >= _bossTrack.Animation.Duration)
        {
            SetBossClip("idle", DeertreeEncounter.Pattern.None);
            return;
        }
        _bossTrack.TrackTime = poseTime;
        if (_bossTrack.MixingFrom != null) _bossTrack.MixTime += delta;
        _boss.Update(0); _bossRenderer.UpdateMesh();
    }

    /// <summary>Advances every Deertree visual by one combat-clock step and matches cell effects to the encounter.</summary>
    public void Sample(float delta)
    {
        if (_encounter == null || _boss == null) return;
        _lastSampleFrame = Time.frameCount;
        _clock += delta;
        SampleBoss(delta);
        SyncCells();
        for (int i = 0; i < _rootHolds.Length; i++)
        {
            if (_rootHolds[i] != null) _rootHolds[i].Advance(delta);
            if (_vineHolds[i] != null) _vineHolds[i].Advance(delta);
            if (_vineMaterials[i] == null) continue;
            _vineFlash[i] = Mathf.MoveTowards(_vineFlash[i], 0, delta * 5);
            _vineMaterials[i].SetFloat("_Clock", _clock);
            _vineMaterials[i].SetFloat("_Hit", _vineFlash[i]);
        }
        bool branchVisible = _encounter.FruitEntering || _encounter.FruitActive || _encounter.Returning;
        if (_branch.gameObject.activeSelf != branchVisible) _branch.gameObject.SetActive(branchVisible);
        if (!branchVisible) return;
        // Keep the authored open pose for the interaction window, then sample its return segment.
        float end = _branch.AnimationState.GetTrack(0).Animation.Duration;
        float time = _encounter.Returning ? Mathf.Lerp(_settings.BranchReturnAt, end, 1 - _encounter.ReturnRemaining / _settings.BranchReturnDuration)
            : _encounter.FruitEntering ? _settings.BranchHoldAt * (1 - _encounter.FruitEntranceRemaining / _settings.BranchGrowDuration) : _settings.BranchHoldAt;
        SampleBranch(time);
        for (int i = 0; i < AppleCount; i++)
        {
            var slot = _appleSlots[i];
            // Native attachment and alpha timelines own appearance and return. Only a destroyed apple overrides them.
            if (_encounter.Apples[i] <= 0) { slot.Pose.Attachment = slot.AppliedPose.Attachment = null; continue; }
            _appleFlash[i] = Mathf.MoveTowards(_appleFlash[i], 0, delta * 5);
            _appleMaterials[i].SetFloat("_Clock", _clock);
            _appleMaterials[i].SetFloat("_Hit", _appleFlash[i]);
        }
        _branchRenderer.UpdateMesh();
    }

    private void SampleBranch(float time)
    {
        // Rebuild from the authored pose so the size multiplier never accumulates between frames.
        _branch.Skeleton.SetupPose();
        _branch.AnimationState.GetTrack(0).TrackTime = time;
        _branch.Update(0);
        foreach (var slot in _appleSlots)
        {
            slot.Bone.Pose.ScaleX *= _settings.AppleScale;
            slot.Bone.Pose.ScaleY *= _settings.AppleScale;
        }
        _branch.Skeleton.UpdateWorldTransform(Spine.Physics.Pose);
    }

    private void SyncCells()
    {
        var cells = _host.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            var state = _encounter.Cells[i];
            bool bound = state.CurseTapsLeft > 0 || state.RootBindTapsLeft > 0;
            if (state.HasRoot && _roots[i] == null && CellFrame(i, out var center, out float width))
            {
                _roots[i] = CreateSpine(_settings.Root, "Unbreakable root " + i, _effects, center, width * _settings.RootWidthPerCell, EffectLayer, RootOrder);
                _rootHolds[i] = _roots[i].gameObject.AddComponent<DeertreeSpineHold>();
                _rootHolds[i].Initialize(_roots[i], _settings.RootHoldAt);
            }
            if (!state.HasRoot && _roots[i] != null) { _rootHolds[i].Clear(); _rootHolds[i] = null; _roots[i] = null; }
            if (bound && _vines[i] == null && CellFrame(i, out center, out width))
            {
                _vines[i] = CreateSpine(_settings.Curse, "Unit binding " + i, _effects, center, width * _settings.VineWidthPerCell, EffectLayer, VineOrder);
                // Same touch hint as the apples: the binding itself glows until it is tapped away.
                var original = _settings.Curse.atlasAssets[0].PrimaryMaterial;
                _vineMaterials[i] = CreateTouchMaterial(original.mainTexture, _settings.BindingGlow, i * 1.3f, "Binding touch highlight " + i);
                _vines[i].GetComponent<SkeletonRenderer>().CustomMaterialOverride[original] = _vineMaterials[i];
                _vineHolds[i] = _vines[i].gameObject.AddComponent<DeertreeSpineHold>();
                _vineHolds[i].Initialize(_vines[i], _settings.VineHoldAt);
            }
            if (!bound && _vines[i] != null) ClearVine(i);
        }
        UpdateSellBadge();
    }

    private bool CellFrame(int index, out Vector3 center, out float width)
    {
        var cell = _host.Cells[index];
        center = default; width = 0;
        if (cell == null) return false;
        if (cell.TryGetVisualBounds(out var bounds)) { center = bounds.center; width = bounds.size.x; }
        else { center = cell.transform.position; width = 1f; }
        center.z = 0;
        return true;
    }

    private void ClearVine(int index)
    {
        _vineHolds[index].Clear(); _vineHolds[index] = null; _vines[index] = null;
        if (_vineMaterials[index] != null) Destroy(_vineMaterials[index]);
        _vineMaterials[index] = null; _vineFlash[index] = 0;
    }

    private void UpdateSellBadge()
    {
        int selected = _host.SelectedRoot;
        bool show = selected >= 0 && CellFrame(selected, out _, out _);
        if (!show) { if (_sellBadge != null && _sellBadge.activeSelf) _sellBadge.SetActive(false); return; }
        CellFrame(selected, out var center, out float width);
        if (_sellBadge == null) CreateSellBadge(width);
        _sellBadge.transform.position = center + new Vector3(0, width * .62f, 0);
        if (!_sellBadge.activeSelf) _sellBadge.SetActive(true);
    }

    private void CreateSellBadge(float cellWidth)
    {
        _sellBadge = new GameObject("Root sale badge");
        _sellBadge.transform.SetParent(_effects, false);
        _badgeSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
        var back = _sellBadge.AddComponent<SpriteRenderer>();
        back.sprite = _badgeSprite; back.color = _settings.SellBackground;
        back.sortingLayerName = EffectLayer; back.sortingOrder = SellOrder;
        _sellBadge.transform.localScale = new Vector3(cellWidth * 1.05f, cellWidth * .42f, 1f);
        var textGo = new GameObject("Label");
        textGo.transform.SetParent(_sellBadge.transform, false);
        // The text keeps a uniform world size although the background quad is stretched.
        textGo.transform.localScale = new Vector3(1f / (cellWidth * 1.05f), 1f / (cellWidth * .42f), 1f);
        var text = textGo.AddComponent<TextMeshPro>();
        if (_settings.SellFont != null) text.font = _settings.SellFont;
        text.text = _settings.SellLabel; text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = false; text.fontSize = cellWidth * 2.6f; text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(cellWidth * 1.05f, cellWidth * .42f);
        var textRenderer = text.GetComponent<MeshRenderer>();
        textRenderer.sortingLayerName = EffectLayer; textRenderer.sortingOrder = SellOrder + 1;
    }

    /// <summary>Whether a world point lies on the visible sale badge.</summary>
    public bool SellBadgeContains(Vector2 world)
    {
        if (_sellBadge == null || !_sellBadge.activeSelf) return false;
        Vector3 center = _sellBadge.transform.position, size = _sellBadge.transform.localScale;
        return Mathf.Abs(world.x - center.x) <= size.x * .5f && Mathf.Abs(world.y - center.y) <= size.y * .5f;
    }

    /// <summary>World centre of a living apple on its actual, enlarged Spine slot.</summary>
    public Vector3 ApplePosition(int index)
    {
        var slot = _appleSlots[index];
        int length = 0;
        if (slot.AppliedPose.Attachment is RegionAttachment region) { region.ComputeWorldVertices(slot, region.GetOffsets(slot.AppliedPose), _vertices, 0, 2); length = 8; }
        else if (slot.AppliedPose.Attachment is MeshAttachment mesh) { length = Mathf.Min(mesh.WorldVerticesLength, _vertices.Length); mesh.ComputeWorldVertices(_branch.Skeleton, slot, 0, length, _vertices, 0, 2); }
        if (length == 0) return _branch.transform.TransformPoint(new Vector3(slot.Bone.AppliedPose.WorldX, slot.Bone.AppliedPose.WorldY, 0));
        Vector2 center = Vector2.zero;
        for (int i = 0; i < length; i += 2) center += new Vector2(_vertices[i], _vertices[i + 1]);
        center /= length / 2;
        return _branch.transform.TransformPoint(center);
    }

    /// <summary>Nearest living apple within the touch radius of a screen point.</summary>
    public bool TryFindApple(Vector2 screen, out int index)
    {
        index = -1;
        if (_encounter == null || _branch == null || !_branch.gameObject.activeSelf || _camera == null) return false;
        float radius = _settings.AppleTouchPixels * Screen.height / 1920f, best = radius * radius;
        for (int i = 0; i < AppleCount; i++)
        {
            if (_encounter.Apples[i] <= 0) continue;
            float distance = ((Vector2)_camera.WorldToScreenPoint(ApplePosition(i)) - screen).sqrMagnitude;
            if (distance > best) continue;
            best = distance; index = i;
        }
        return index >= 0;
    }

    /// <summary>Short brightness pop on a tapped apple.</summary>
    public void FlashApple(int index) => _appleFlash[index] = 1;
    /// <summary>Short brightness pop on a tapped binding.</summary>
    public void FlashBinding(int index) { if (_vineFlash != null) _vineFlash[index] = 1; }

    // Field taps are read here because BossBase owns the boss object's Update.
    private void Update()
    {
        if (_host == null || _encounter == null || _camera == null || Time.frameCount - _lastSampleFrame > 1) return;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && !IsOverUI(touch.position, touch.fingerId)) Tap(touch.position);
            }
        }
        else if (Input.GetMouseButtonDown(0) && !IsOverUI(Input.mousePosition, -1)) Tap(Input.mousePosition);
    }

    private void Tap(Vector2 screen)
    {
        var ray = _camera.ScreenPointToRay(screen);
        Vector2 world = GroundPlane.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : Vector2.zero;
        _host.HandlePointerDown(screen, world);
    }

    private bool IsOverUI(Vector2 screen, int pointerId)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        if (_uiEventSystem != eventSystem) { _uiEventSystem = eventSystem; _uiPointer = new PointerEventData(eventSystem); }
        _uiPointer.Reset(); _uiPointer.position = screen; _uiPointer.pointerId = pointerId;
        _uiHits.Clear();
        eventSystem.RaycastAll(_uiPointer, _uiHits);
        foreach (var hit in _uiHits) if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    /// <summary>Removes every cell effect and the branch, and returns the boss to idle. Safe to call repeatedly.</summary>
    public void End()
    {
        if (_rootHolds != null)
            for (int i = 0; i < _rootHolds.Length; i++)
            {
                if (_rootHolds[i] != null) { _rootHolds[i].Clear(); _rootHolds[i] = null; _roots[i] = null; }
                if (_vineHolds[i] != null) ClearVine(i);
            }
        if (_branch != null) _branch.gameObject.SetActive(false);
        if (_sellBadge != null) _sellBadge.SetActive(false);
        if (_boss != null && _bossPattern != DeertreeEncounter.Pattern.None) SetBossClip("idle", DeertreeEncounter.Pattern.None);
        _encounter = null; _host = null;
    }

    private void OnDestroy()
    {
        foreach (var material in _appleMaterials) if (material != null) Destroy(material);
        if (_vineMaterials != null) foreach (var material in _vineMaterials) if (material != null) Destroy(material);
        foreach (var material in _materials.Values) if (material != null) Destroy(material);
        if (_badgeSprite != null) Destroy(_badgeSprite);
        if (_effects != null) Destroy(_effects.gameObject);
    }
}
