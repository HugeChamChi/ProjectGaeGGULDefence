using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Offline touch harness. Unit portraits and their attacks are stand-ins; all pattern decisions use DeertreeEncounter.</summary>
[DefaultExecutionOrder(1000)]
public sealed class DeertreeBossLab : MonoBehaviour
{
    public DeertreeBossSettings Settings;
    private const int Columns = 4, Rows = 3;
    private readonly Color _gold = new(1f, .77f, .32f);
    private Camera _camera;
    private Canvas _canvas;
    private Font _font;
    private SkeletonAnimation _boss, _branch;
    private readonly SkeletonAnimation[] _roots = new SkeletonAnimation[Columns * Rows];
    private readonly SkeletonAnimation[] _vines = new SkeletonAnimation[Columns * Rows];
    private readonly DeertreeSpineHold[] _rootHolds = new DeertreeSpineHold[Columns * Rows];
    private readonly DeertreeSpineHold[] _vineHolds = new DeertreeSpineHold[Columns * Rows];
    private readonly Text[] _unitLabels = new Text[Columns * Rows];
    private readonly SpriteRenderer[] _tiles = new SpriteRenderer[Columns * Rows];
    private Sprite _tileSprite;
    private readonly Dictionary<Material, Material> _materials = new();
    private readonly Text[] _bindLabels = new Text[Columns * Rows];
    private readonly Button[] _appleButtons = new Button[4];
    private readonly Text[] _appleLabels = new Text[4];
    private readonly Slot[] _appleSlots = new Slot[4];
    private readonly Material[] _appleMaterials = new Material[4];
    private readonly float[] _appleFlash = new float[4];
    private readonly Material[] _vineMaterials = new Material[Columns * Rows];
    private readonly float[] _vineFlash = new float[Columns * Rows];
    private SkeletonRenderer _branchRenderer, _bossRenderer;
    private TrackEntry _bossTrack;
    private DeertreeEncounter.Pattern _bossPattern;
    private float _bossAge;
    private bool _bossReturning;
    private readonly float[] _vertices = new float[2048];
    private Text _status, _hpText, _message;
    private Image _hpFill;
    private Button _sell;
    private int _selected = -1;
    private float _clock, _attackClock, _branchAge;
    private bool _paused, _autoDamage;
    private string _lastMessage = "빈칸의 뿌리는 선택 후 0원 판매";
    /// <summary>Current inspectable encounter, also used by targeted Play checks.</summary>
    public DeertreeEncounter Encounter { get; private set; }

    private void Start()
    {
        _camera = Camera.main;
        _font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 32);
        BuildUI();
        _boss = CreateSpine(Settings.Boss, "Deertree", new Vector3(0, 2.7f, 0), 9f, 0);
        _boss.enabled = false; _bossRenderer = _boss.GetComponent<SkeletonRenderer>();
        SetBossClip("idle", DeertreeEncounter.Pattern.None);
        _branch = CreateSpine(Settings.Branch, "Apple branch", new Vector3(0, .5f, -1), 9f, 20);
        _branch.enabled = false;
        _branchRenderer = _branch.GetComponent<SkeletonRenderer>();
        _branch.AnimationState.SetAnimation(0, "apple_branch", false).TrackTime = Settings.BranchHoldAt;
        _branch.Update(0);
        for (int i = 0; i < 4; i++)
        {
            _appleSlots[i] = _branch.Skeleton.FindSlot("apple_0" + (i + 1));
            var sourceMaterial = _branch.GetComponent<MeshRenderer>().sharedMaterial;
            _appleMaterials[i] = CreateTouchMaterial(sourceMaterial.mainTexture, Settings.AppleGlow, i * 1.7f, "Apple natural highlight " + i);
            _branchRenderer.CustomSlotMaterials[_appleSlots[i]] = _appleMaterials[i];
        }
        _branch.gameObject.SetActive(false);
        Encounter = new DeertreeEncounter(Settings, Columns * Rows, 2711);
        Encounter.OnCast += Cast;
        Encounter.OnImpact += Impact;
        ResetEncounter();
    }

    private SkeletonAnimation CreateSpine(SkeletonDataAsset asset, string label, Vector3 center, float width, int order)
    {
        var spine = SkeletonAnimation.NewSkeletonAnimationGameObject(asset).skeletonAnimation;
        spine.name = label; spine.transform.SetParent(transform, false);
        spine.Initialize(false);
        spine.Update(0);
        var skeletonRenderer = spine.GetComponent<SkeletonRenderer>();
        skeletonRenderer.UpdateMesh();
        foreach (var original in spine.GetComponent<MeshRenderer>().sharedMaterials)
        {
            if (!_materials.TryGetValue(original, out var corrected))
            {
                corrected = new Material(original) { name = original.name + " (Lab Straight Alpha)" };
                corrected.SetFloat("_StraightAlphaInput", 1f); corrected.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                _materials.Add(original, corrected);
            }
            skeletonRenderer.CustomMaterialOverride[original] = corrected;
        }
        skeletonRenderer.UpdateMesh();
        spine.Skeleton.GetBounds(out float x, out float y, out float w, out float h, ref _boundsBuffer);
        float scale = width / Mathf.Max(.01f, w);
        spine.transform.localScale = Vector3.one * scale;
        spine.transform.position = center - new Vector3((x + w / 2) * scale, (y + h / 2) * scale, 0);
        var renderer = spine.GetComponent<MeshRenderer>(); renderer.sortingOrder = order;
        return spine;
    }
    private float[] _boundsBuffer;

    /// <summary>Touch-hint material that keeps the authored colours (apples and curse bindings share it).</summary>
    private Material CreateTouchMaterial(Texture texture, float strength, float phase, string label)
    {
        var material = new Material(Settings.AppleShader) { name = label, mainTexture = texture };
        material.SetColor("_Highlight", Settings.AppleHighlight);
        material.SetFloat("_Strength", strength);
        material.SetFloat("_Phase", phase);
        return material;
    }

    private void ClearVine(int index)
    {
        _vineHolds[index].Clear(); _vineHolds[index] = null; _vines[index] = null;
        if (_vineMaterials[index] != null) Destroy(_vineMaterials[index]);
        _vineMaterials[index] = null; _vineFlash[index] = 0;
    }

    private Vector3 CellPosition(int index) => new Vector3((index % Columns - 1.5f) * 2.05f, -1.65f - index / Columns * 1.9f, -2);

    private void Cast(DeertreeEncounter.Pattern pattern)
    {
        string clip = pattern == DeertreeEncounter.Pattern.Root ? "skill_01" : pattern == DeertreeEncounter.Pattern.Fruit ? "skill_02" : "skill_03";
        SetBossClip(clip, pattern);
        _lastMessage = pattern == DeertreeEncounter.Pattern.Root ? "썩은 나무 뿌리" : pattern == DeertreeEncounter.Pattern.Fruit ? "나무 과실" : "옭아매는 저주";
    }

    private void SetBossClip(string clip, DeertreeEncounter.Pattern pattern)
    {
        _bossPattern = pattern; _bossAge = 0; _bossReturning = false;
        _boss.AnimationState.ClearTracks(); _boss.Skeleton.SetupPose();
        _bossTrack = _boss.AnimationState.SetAnimation(0, clip, pattern == DeertreeEncounter.Pattern.None);
        _bossTrack.MixDuration = 0;
    }

    private void SampleBoss(float delta)
    {
        _bossAge += delta;
        float poseTime = _bossAge;
        bool fruit = _bossPattern == DeertreeEncounter.Pattern.Fruit;
        bool curse = _bossPattern == DeertreeEncounter.Pattern.Curse;
        bool holding = fruit && (Encounter.Casting == DeertreeEncounter.Pattern.Fruit || Encounter.FruitEntering || Encounter.FruitActive)
            || curse && (Encounter.CurseActive || Encounter.Casting == DeertreeEncounter.Pattern.Curse);
        if (fruit || curse)
        {
            float hold = fruit ? Settings.FruitBossHoldAt : Settings.CurseBossHoldAt;
            if (holding && _bossAge >= hold)
            {
                // A cosine ping-pong reverses gently at both ends of each authored interaction segment.
                float period = fruit ? Settings.FruitBossLoopPeriod : Settings.CurseBossLoopPeriod;
                float end = fruit ? Settings.FruitBossLoopEnd : Settings.CurseBossLoopEnd;
                float phase = (_bossAge - hold) / period;
                float blend = .5f - .5f * Mathf.Cos(phase * Mathf.PI * 2);
                poseTime = Mathf.Lerp(hold, end, blend);
            }
            else if (holding) poseTime = _bossAge = Mathf.Min(_bossAge, hold);
            else if (!_bossReturning)
            {
                _bossReturning = true;
                poseTime = _bossAge = fruit ? Settings.FruitBossReturnAt : Settings.CurseBossReturnAt;
                // Mix from the current loop pose, including a fruit cycle that ends between loop boundaries.
                _bossTrack = _boss.AnimationState.SetAnimation(0, _bossTrack.Animation, false);
                _bossTrack.MixDuration = fruit ? Settings.FruitBossReturnBlend : Settings.CurseBossReturnBlend;
            }
            if (fruit && Encounter.Returning)
                poseTime = _bossAge = Mathf.Lerp(Settings.FruitBossReturnAt, _bossTrack.Animation.Duration, 1 - Encounter.ReturnRemaining / Settings.BranchReturnDuration);
        }
        if (!holding && _bossPattern != DeertreeEncounter.Pattern.None && poseTime >= _bossTrack.Animation.Duration)
        {
            SetBossClip("idle", DeertreeEncounter.Pattern.None);
            poseTime = 0;
        }
        _bossTrack.TrackTime = poseTime;
        if (_bossTrack.MixingFrom != null) _bossTrack.MixTime += delta;
        _boss.Update(0); _bossRenderer.UpdateMesh();
    }

    private void TapApple(int index)
    {
        if (Encounter.TapApple(index)) _appleFlash[index] = 1;
    }

    private void Impact(DeertreeEncounter.Pattern pattern)
    {
        if (pattern == DeertreeEncounter.Pattern.Fruit)
        {
            _branchAge = 0; _branch.gameObject.SetActive(true);
            _branch.AnimationState.ClearTracks(); _branch.Skeleton.SetupPose();
            _branch.AnimationState.SetAnimation(0, "apple_branch", false);
        }
    }

    /// <summary>Restores preview HP, one-shots, units and selection without touching account data.</summary>
    public void ResetEncounter()
    {
        Encounter.Reset();
        for (int i = 0; i < Encounter.Cells.Length; i++)
        {
            if (i % 3 != 2) Encounter.PlaceUnit(i, i % 3 + 1);
            if (_rootHolds[i] != null) _rootHolds[i].Clear();
            if (_vineHolds[i] != null) ClearVine(i);
            _rootHolds[i] = null;
            _roots[i] = null;
        }
        _selected = -1; _clock = _attackClock = 0;
        SetBossClip("idle", DeertreeEncounter.Pattern.None);
        _branch.gameObject.SetActive(false);
        _lastMessage = "빈칸의 뿌리는 선택 후 0원 판매";
        Refresh();
    }

    private void Update()
    {
        if (Encounter == null) return;
        float delta = _paused ? 0 : Time.deltaTime;
        _clock += delta;
        Encounter.Advance(delta);
        if (_autoDamage && delta > 0 && Encounter.CombatantsCanAttack)
        {
            int acting = 0; foreach (var cell in Encounter.Cells) if (cell.CanAct) acting++;
            Encounter.SetHp(Encounter.Hp - acting * .002f * delta);
        }
        if (Encounter.CombatantsCanAttack) _attackClock += delta;
        Refresh();
    }

    private void LateUpdate()
    {
        if (Encounter == null) return;
        float delta = _paused ? 0 : Time.deltaTime;
        SampleBoss(delta);
        for (int i = 0; i < _rootHolds.Length; i++)
        {
            if (_rootHolds[i] != null) _rootHolds[i].Advance(delta);
            if (_vineHolds[i] != null) _vineHolds[i].Advance(delta);
            if (_vineMaterials[i] != null)
            {
                _vineFlash[i] = Mathf.MoveTowards(_vineFlash[i], 0, delta * 5);
                _vineMaterials[i].SetFloat("_Clock", _clock);
                _vineMaterials[i].SetFloat("_Hit", _vineFlash[i]);
            }
        }
        if (!Encounter.FruitEntering && !Encounter.FruitActive && !Encounter.Returning) return;
        _branchAge += delta;
        // Keep the authored open pose for the interaction window, then sample its return segment.
        float end = _branch.AnimationState.GetTrack(0).Animation.Duration;
        float time = Encounter.Returning ? Mathf.Lerp(Settings.BranchReturnAt, end, 1 - Encounter.ReturnRemaining / Settings.BranchReturnDuration)
            : Encounter.FruitEntering ? Settings.BranchHoldAt * (1 - Encounter.FruitEntranceRemaining / Settings.BranchGrowDuration) : Settings.BranchHoldAt;
        SampleBranch(time);
        for (int i = 0; i < 4; i++)
        {
            var slot = _appleSlots[i];
            // Native attachment and alpha timelines own appearance and return. Only a destroyed apple overrides them.
            if (Encounter.Apples[i] <= 0) slot.Pose.Attachment = slot.AppliedPose.Attachment = null;
            if (Encounter.Apples[i] <= 0) continue;
            Vector3 position = ApplePosition(slot);
            var screen = _camera.WorldToScreenPoint(position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, screen, null, out Vector2 local);
            ((RectTransform)_appleButtons[i].transform).anchoredPosition = local;
            _appleButtons[i].interactable = Encounter.FruitActive && !_paused;
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
            slot.Bone.Pose.ScaleX *= Settings.AppleScale;
            slot.Bone.Pose.ScaleY *= Settings.AppleScale;
        }
        _branch.Skeleton.UpdateWorldTransform(Spine.Physics.Pose);
    }

    private Vector3 ApplePosition(Slot slot)
    {
        int length = 0;
        if (slot.AppliedPose.Attachment is Spine.RegionAttachment region) { region.ComputeWorldVertices(slot, region.GetOffsets(slot.AppliedPose), _vertices, 0, 2); length = 8; }
        else if (slot.AppliedPose.Attachment is Spine.MeshAttachment mesh) { length = Mathf.Min(mesh.WorldVerticesLength, _vertices.Length); mesh.ComputeWorldVertices(_branch.Skeleton, slot, 0, length, _vertices, 0, 2); }
        if (length == 0) return _branch.transform.TransformPoint(new Vector3(slot.Bone.AppliedPose.WorldX, slot.Bone.AppliedPose.WorldY, 0));
        Vector2 center = Vector2.zero;
        for (int i = 0; i < length; i += 2) center += new Vector2(_vertices[i], _vertices[i + 1]);
        center /= length / 2;
        return _branch.transform.TransformPoint(center);
    }

    private void Refresh()
    {
        _hpFill.fillAmount = Encounter.Hp;
        _hpText.text = $"어려움   DEERTREE                 {Encounter.Hp:P0}";
        _status.text = Encounter.CurseActive || Encounter.CurseRecovering || Encounter.Casting == DeertreeEncounter.Pattern.Curse ? "옭아매는 저주" : Encounter.IsInvulnerable ? "나무 과실" : "썩은 나무 뿌리";
        _message.text = Encounter.Hp <= 0 ? "보스 처치, 다시 시작으로 재실험" : _lastMessage;
        _sell.gameObject.SetActive(_selected >= 0 && Encounter.Cells[_selected].HasRoot);
        for (int i = 0; i < Encounter.Cells.Length; i++)
        {
            var cell = Encounter.Cells[i];
            _tiles[i].color = cell.Tier == 0 ? new Color(.10f, .17f, .16f) : !cell.CanAct ? new Color(.32f, .19f, .32f) : new Color(.18f, .36f, .32f);
            string tier = cell.Tier == 1 ? "노말" : cell.Tier == 2 ? "레어" : "에픽";
            _unitLabels[i].text = cell.Tier == 0 ? "" : $"{tier}\n{(!cell.CanAct ? "행동 불능" : !Encounter.CombatantsCanAttack ? "대기" : _attackClock % 1 < .3f ? "공격!" : "공격 대기")}";
            int taps = Mathf.Max(cell.CurseTapsLeft, cell.RootBindTapsLeft);
            _bindLabels[i].text = cell.HasRoot ? (_selected == i ? "선택됨" : "") : "";
            if (cell.HasRoot && _roots[i] == null)
            {
                _roots[i] = CreateSpine(Settings.Root, "Unbreakable root " + i, CellPosition(i), 1.6f, 5);
                _rootHolds[i] = _roots[i].gameObject.AddComponent<DeertreeSpineHold>();
                _rootHolds[i].Initialize(_roots[i], Settings.RootHoldAt);
            }
            if (!cell.HasRoot && _roots[i] != null) { _rootHolds[i].Clear(); _rootHolds[i] = null; _roots[i] = null; }
            if (taps > 0 && _vines[i] == null)
            {
                _vines[i] = CreateSpine(Settings.Curse, "Unit binding " + i, CellPosition(i), 1.65f, 6);
                // Same touch hint as the apples: the binding itself glows until it is tapped away.
                var original = Settings.Curse.atlasAssets[0].PrimaryMaterial;
                _vineMaterials[i] = CreateTouchMaterial(original.mainTexture, Settings.BindingGlow, i * 1.3f, "Binding touch highlight " + i);
                _vines[i].GetComponent<SkeletonRenderer>().CustomMaterialOverride[original] = _vineMaterials[i];
                _vineHolds[i] = _vines[i].gameObject.AddComponent<DeertreeSpineHold>();
                _vineHolds[i].Initialize(_vines[i], Settings.VineHoldAt);
            }
            if (taps == 0 && _vines[i] != null) ClearVine(i);
        }
        bool branchVisible = Encounter.FruitEntering || Encounter.FruitActive || Encounter.Returning;
        _branch.gameObject.SetActive(branchVisible);
        for (int i = 0; i < 4; i++) { _appleButtons[i].gameObject.SetActive(Encounter.FruitActive && Encounter.Apples[i] > 0); _appleLabels[i].text = ""; }
    }

    private RectTransform Panel(string label, Transform parent, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false); var rect = (RectTransform)go.transform;
        rect.anchoredPosition = center; rect.sizeDelta = size;
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }

    private Text Label(Transform parent, string content, Vector2 center, Vector2 size, int fontSize, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false); var rect = (RectTransform)go.transform; rect.anchoredPosition = center; rect.sizeDelta = size;
        var text = go.GetComponent<Text>(); text.font = _font; text.fontSize = fontSize; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.text = content; text.raycastTarget = false;
        return text;
    }

    private Button Button(string label, Vector2 center, Vector2 size, Action callback, out Text text)
    {
        var rect = Panel(label, _canvas.transform, center, size, new Color(.14f, .25f, .24f, .93f));
        rect.GetComponent<Image>().raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(() => { if (!_paused || label == "일시정지" || label == "다시 시작") callback(); });
        text = Label(rect, label, Vector2.zero, size, 27, Color.white);
        return button;
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("Deertree touch HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false); _canvas = canvasGo.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 1;
        Label(_canvas.transform, "썩은 숲의 수호자", new Vector2(0, 865), new Vector2(940, 70), 48, _gold);
        _hpText = Label(_canvas.transform, "", new Vector2(0, 797), new Vector2(940, 50), 29, Color.white);
        Panel("HP background", _canvas.transform, new Vector2(0, 753), new Vector2(890, 18), new Color(.2f, .1f, .1f));
        var fill = Panel("HP", _canvas.transform, new Vector2(0, 753), new Vector2(890, 18), new Color(.82f, .25f, .25f));
        _hpFill = fill.GetComponent<Image>(); _hpFill.type = Image.Type.Filled; _hpFill.fillMethod = Image.FillMethod.Horizontal;
        _status = Label(_canvas.transform, "", new Vector2(0, 690), new Vector2(970, 50), 30, _gold);
        _message = Label(_canvas.transform, "", new Vector2(0, -665), new Vector2(1010, 70), 30, _gold);
        _tileSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
        _hpFill.sprite = _tileSprite;
        for (int i = 0; i < Columns * Rows; i++)
        {
            int index = i; Vector3 pos = CellPosition(i);
            // A transparent input surface sits above the world-space Spine roots.
            var button = Button("", new Vector2(pos.x * 100, pos.y * 100), new Vector2(185, 170), () => {
                if (Encounter.TapUnit(index)) _vineFlash[index] = 1;
                else _selected = Encounter.Cells[index].HasRoot ? index : -1;
            }, out var label);
            _unitLabels[i] = label;
            // Keep the tile behind Spine, using a world-space quad instead of the UI background.
            button.GetComponent<Image>().color = new Color(0, 0, 0, .01f);
            var tile = new GameObject("Cell " + i); tile.transform.SetParent(transform, false); tile.transform.position = new Vector3(pos.x, pos.y, 1); tile.transform.localScale = new Vector3(1.85f, 1.7f, 1);
            _tiles[i] = tile.AddComponent<SpriteRenderer>(); _tiles[i].sprite = _tileSprite; _tiles[i].sortingOrder = -10;
            _bindLabels[i] = Label(button.transform, "", new Vector2(0, -47), new Vector2(180, 70), 26, _gold);
            label.rectTransform.anchoredPosition = new Vector2(0, 25); label.fontSize = 27;
        }
        // Invisible hit targets only. Feedback is rendered on the apple's original Spine slots.
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            _appleButtons[i] = Button("", Vector2.zero, new Vector2(132, 132), () => TapApple(index), out _appleLabels[i]);
            _appleButtons[i].transition = Selectable.Transition.None;
            _appleButtons[i].GetComponent<Image>().color = Color.clear;
        }
        Button("체력 50%", new Vector2(-340, -760), new Vector2(300, 80), () => Encounter.SetHp(.5f), out _);
        Button("체력 10%", new Vector2(0, -760), new Vector2(300, 80), () => Encounter.SetHp(.1f), out _);
        Button("액티브 해제", new Vector2(340, -760), new Vector2(300, 80), () => Encounter.UseActiveSkill(), out _);
        Button("빈칸 채우기", new Vector2(-340, -855), new Vector2(300, 75), () => { for (int i = 0; i < Encounter.Cells.Length; i++) Encounter.PlaceUnit(i, 1); }, out _);
        Button("일시정지", new Vector2(0, -855), new Vector2(300, 75), () => _paused = !_paused, out _);
        Button("다시 시작", new Vector2(340, -855), new Vector2(300, 75), ResetEncounter, out _);
        _sell = Button("뿌리 판매 0원", new Vector2(0, -600), new Vector2(320, 70), () => { if (_selected >= 0) Encounter.SellRoot(_selected); _selected = -1; }, out _);
        Button("자동 공격", new Vector2(365, 622), new Vector2(230, 65), () => _autoDamage = !_autoDamage, out _);
        Label(_canvas.transform, "보스 패턴 실험씬", new Vector2(-315, 622), new Vector2(350, 55), 26, new Color(.6f, .73f, .65f));
        new GameObject("Touch input", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void OnDestroy()
    {
        if (_font != null) Destroy(_font);
        if (_tileSprite != null) Destroy(_tileSprite);
        foreach (var material in _appleMaterials) if (material != null) Destroy(material);
        foreach (var material in _vineMaterials) if (material != null) Destroy(material);
        foreach (var material in _materials.Values) Destroy(material);
    }
}
