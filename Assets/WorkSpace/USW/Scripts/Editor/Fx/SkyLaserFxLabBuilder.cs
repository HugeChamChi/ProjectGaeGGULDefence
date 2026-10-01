using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 하늘 레이저(SkyLaserFx) 머티리얼·프리팹 2종(Pulse/Sustain)과 실험실 씬(FxLab_SkyLaser)을 만든다.
/// 레퍼런스: design/레이저연출예시1.mp4 (뚝뚝 끊어 쏘기), design/레이저연출예시2.gif (찌이잉 유지).
/// 이 도구가 만든 에셋만 다시 덮어쓴다. 인게임 씬·기존 프리팹은 건드리지 않는다.
/// 실험실 카메라는 인게임 Main Camera와 같은 원근(위치 0,3,-13 / 아래로 7도 / FOV 60), 타겟은 인게임 Boss_Point 위치.
/// </summary>
public static class SkyLaserFxLabBuilder
{
    private const string TexRoot     = "Assets/Imports/Hovl Studio/HSFiles/Textures";
    private const string MatRoot     = "Assets/WorkSpace/USW/Materials/Fx/SkyLaser";
    private const string PrefabRoot  = "Assets/WorkSpace/USW/Prefab/Effect/SkyLaser";
    private const string ScenePath   = "Assets/WorkSpace/USW/Scene/FxLab_SkyLaser.unity";
    private const string PulsePath   = PrefabRoot + "/FxSkyLaser_Pulse.prefab";
    private const string SustainPath = PrefabRoot + "/FxSkyLaser_Sustain.prefab";
    private const string PulseDronePath   = PrefabRoot + "/FxSkyLaser_Pulse_Gammang.prefab";
    private const string SustainDronePath = PrefabRoot + "/FxSkyLaser_Sustain_Gammang.prefab";

    // 드론 발사체 (임시) — 감망 선택지 구상용 노란 감망 드론 (사용자 요청 2026-10-01). 원본 그림은 렌즈가 왼쪽(180도)을 본다
    private const string DroneSpriteRoot = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs";
    private const string DroneIdleSprite = "Drone_Yellow_Gammang_1";
    private const string DroneFireSprite = "Drone_Yellow_Gammang_2";
    // 감망 노랑 — DroneLaserFxSetup의 Yellow 드론 레이저와 같은 색
    private static readonly Color GammangYellow = new Color(1f, 0.86f, 0.2f, 1f);
    private const float DroneScale    = 1.6f;  // 294px / 687ppu ≈ 0.43 → 약 0.69 월드
    private const float DroneFacing   = 180f;
    private const float DroneMuzzle   = 0.3f;  // 드론 그림 중심 → 렌즈 앞
    private const float DroneGlowSize = 1.3f;

    // 2.5D 궤도 (드론 버전만) — 음수 깊이 = 카메라 쪽
    private const float PulseDepthLeft    = -1.4f;
    private const float PulseDepthRight   = 1.1f;
    private const float PulseDepthCenter  = -0.4f;
    private const float PulseOrbitSpeed   = 28f;
    private static readonly Vector2 SustainDroneOffset = new Vector2(1.5f, 2.9f);
    private const float SustainDroneDepth = -0.6f;
    private const float SustainOrbitSpeed = -75f;
    private const string BgPath      = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string BossPath    = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";
    private const string BeamShader  = "USW/Fx/LaserBeam";
    private const string AddShader   = "USW/Fx/AdditiveWorld";
    private const string FxLayer     = "FX";

    // 인게임 IngameScene 기준값
    private static readonly Vector3 CameraPos     = new Vector3(0f, 3f, -13f);
    private const float CameraPitch               = 7f;
    private const float CameraFov                 = 60f;
    private static readonly Vector3 BgPos         = new Vector3(0f, 0.88f, 0f);
    private const float BgScale                   = 0.8f;
    private static readonly Vector3 BossPos       = new Vector3(0f, 3.28f, 0f);
    private const float BossScale                 = 0.56f; // 보스 프리팹 0.7 × bg 0.8

    private static readonly Color Mint = new Color(0.25f, 1f, 0.85f, 1f);

    // 정렬 순서 (FX 레이어)
    private const int OrderBeam = 30, OrderPadGlow = 31, OrderRing = 32, OrderPad = 33, OrderLens = 34, OrderCharge = 35;
    private const int OrderImpactGlow = 36, OrderImpactRing = 37, OrderImpactFlash = 38, OrderImpactFlare = 39, OrderSparks = 40;

    private struct Mats
    {
        public Material Beam, Glow, Pad, Lens, Flare, Ring, Point, Spark;
    }

    [MenuItem("Tools/USW/Fx/Build Sky Laser (Prefabs + Lab Scene)")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildPrefabs();
        BuildScene();
        Debug.Log($"[SkyLaserFxLab] 프리팹 2종 + 실험실 씬 생성 완료 → {ScenePath}");
    }

    [MenuItem("Tools/USW/Fx/Build Sky Laser Prefabs Only")]
    public static void BuildPrefabs()
    {
        EnsureFolder(MatRoot);
        EnsureFolder(PrefabRoot);
        var mats = BuildMaterials();
        BuildPulse(mats, null, PulsePath);
        BuildSustain(mats, null, SustainPath);
        var drone = LoadDroneArt();
        if (drone.HasValue)
        {
            BuildPulse(mats, drone, PulseDronePath);
            BuildSustain(mats, drone, SustainDronePath);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ── 머티리얼 ───────────────────────────────────────────

    private static Mats BuildMaterials()
    {
        var beam = GetOrCreateMaterial("M_SkyLaser_Beam", BeamShader);
        beam.SetTexture("_FlowTex", Tex("Trail58"));
        beam.SetColor("_Color", Mint);
        beam.SetFloat("_Intensity", 1.6f);
        beam.SetFloat("_GlowOpacity", 0.75f);
        beam.SetFloat("_GlowPower", 1.6f);
        beam.SetFloat("_CoreWidth", 0.32f);
        EditorUtility.SetDirty(beam);

        return new Mats
        {
            Beam  = beam,
            Glow  = AddMaterial("M_SkyLaser_Glow", "Glow1", 1.2f, 0.35f),
            Pad   = AddMaterial("M_SkyLaser_Pad", "Quad2", 1.6f, 0.7f),
            Lens  = AddMaterial("M_SkyLaser_Lens", "Romb3", 1.4f, 0.75f),
            Flare = AddMaterial("M_SkyLaser_Flare", "Flare17", 1.6f, 0.4f),
            Ring  = AddMaterial("M_SkyLaser_Ring", "Circle17", 1.4f, 0.55f),
            Point = AddMaterial("M_SkyLaser_Point", "Point12", 1.6f, 0.5f),
            Spark = AddMaterial("M_SkyLaser_Spark", "Glow1", 2.2f, 0.6f),
        };
    }

    /// <param name="opacity">밝은 배경을 덮는 정도 (0 = 순수 Additive)</param>
    private static Material AddMaterial(string name, string texture, float intensity, float opacity)
    {
        var mat = GetOrCreateMaterial(name, AddShader);
        mat.SetTexture("_MainTex", Tex(texture));
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_Intensity", intensity);
        mat.SetFloat("_Opacity", opacity);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetOrCreateMaterial(string name, string shaderName)
    {
        string path = $"{MatRoot}/{name}.mat";
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new System.InvalidOperationException($"셰이더 없음: {shaderName}");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        return mat;
    }

    private static Texture2D Tex(string name)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexRoot}/{name}.png");
        if (tex == null) Debug.LogWarning($"[SkyLaserFxLab] 텍스처 없음: {name}");
        return tex;
    }

    // ── 프리팹 ─────────────────────────────────────────────

    private static void BuildPulse(Mats m, DroneArt? drone, string path)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        var fx = root.AddComponent<SkyLaserFx>();

        // 좌/우 비스듬 → 가운데 수직 (마지막 굵은 발은 가운데, 레퍼런스1 끝 장면)
        var left   = BuildRig(root.transform, "Emitter_L", new Vector2(-1.5f, 2.7f), m, new Vector2(0.95f, 0.4f), false, drone);
        var right  = BuildRig(root.transform, "Emitter_R", new Vector2(1.5f, 3.0f), m, new Vector2(0.95f, 0.4f), false, drone);
        var center = BuildRig(root.transform, "Emitter_C", new Vector2(0f, 3.3f), m, new Vector2(1.15f, 0.48f), false, drone);
        var impact = BuildImpact(root.transform, m);
        if (drone.HasValue)
        {
            // 2.5D: 앞-왼쪽 / 뒤-오른쪽 / 살짝 앞-가운데에 띄우고 천천히 돈다 (원근으로 크기·빔 각도가 달라짐)
            left.Depth = PulseDepthLeft;
            right.Depth = PulseDepthRight;
            center.Depth = PulseDepthCenter;
        }

        var so = new SerializedObject(fx);
        so.FindProperty("_mode").enumValueIndex = (int)SkyLaserMode.Pulse;
        so.FindProperty("_orbitSpeed").floatValue = drone.HasValue ? PulseOrbitSpeed : 0f;
        so.FindProperty("_tint").colorValue = drone?.Tint ?? Mint;
        WriteRigs(so, left, right, center);
        WriteImpact(so, impact);
        so.FindProperty("_pulseCount").intValue = 6;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void BuildSustain(Mats m, DroneArt? drone, string path)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        var fx = root.AddComponent<SkyLaserFx>();

        // 드론 버전은 오른쪽-앞에서 시작해 카메라 앞쪽을 지나 왼쪽으로 돌며 쏜다 (가까워질 때 커짐)
        var rig = drone.HasValue
            ? BuildRig(root.transform, "Emitter", SustainDroneOffset, m, new Vector2(1.3f, 0.55f), true, drone)
            : BuildRig(root.transform, "Emitter", new Vector2(0.25f, 3.1f), m, new Vector2(1.3f, 0.55f), true, drone);
        if (drone.HasValue) rig.Depth = SustainDroneDepth;
        var impact = BuildImpact(root.transform, m);

        var so = new SerializedObject(fx);
        so.FindProperty("_mode").enumValueIndex = (int)SkyLaserMode.Sustain;
        so.FindProperty("_orbitSpeed").floatValue = drone.HasValue ? SustainOrbitSpeed : 0f;
        so.FindProperty("_tint").colorValue = drone?.Tint ?? Mint;
        WriteRigs(so, rig);
        WriteImpact(so, impact);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    /// <summary>발사판 대신 붙이는 드론 그림 (임시 — 사용자 요청 2026-10-01 "드론이 아래를 쳐다보는 느낌").</summary>
    private struct DroneArt
    {
        public Sprite Idle, Fire;
        public Color Tint;
    }

    private static DroneArt? LoadDroneArt()
    {
        var idle = AssetDatabase.LoadAssetAtPath<Sprite>($"{DroneSpriteRoot}/{DroneIdleSprite}.png");
        var fire = AssetDatabase.LoadAssetAtPath<Sprite>($"{DroneSpriteRoot}/{DroneFireSprite}.png");
        if (idle == null)
        {
            Debug.LogWarning($"[SkyLaserFxLab] 드론 스프라이트 없음: {DroneIdleSprite} — 드론 버전 생략");
            return null;
        }
        return new DroneArt { Idle = idle, Fire = fire, Tint = GammangYellow };
    }

    private struct Rig
    {
        public Transform Root;
        public Vector2 Offset;
        public Renderer Pad, PadCore, PadGlow, Charge, ChargeRing, Beam;
        public SpriteRenderer Drone;
        public DroneArt? Art;
        public float Depth;
    }

    private struct Impact
    {
        public Renderer Flash, Ring, Flare, Glow;
        public ParticleSystem Sparks;
    }

    private static Rig BuildRig(Transform parent, string name, Vector2 offset, Mats m, Vector2 padSize, bool chargeRing, DroneArt? drone)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;

        var rig = new Rig { Root = go.transform, Offset = offset, Art = drone };
        if (drone.HasValue)
        {
            // 드론 몸체 + 뒤 은은한 번짐 (발사판 프레임/렌즈 없음)
            rig.PadGlow = Quad(go.transform, "DroneGlow", m.Glow, Vector3.one * DroneGlowSize, OrderPadGlow);
            var body = new GameObject("Drone", typeof(SpriteRenderer));
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = Vector3.one * DroneScale;
            rig.Drone = body.GetComponent<SpriteRenderer>();
            rig.Drone.sprite = drone.Value.Idle;
            rig.Drone.sortingLayerName = FxLayer;
            rig.Drone.sortingOrder = OrderPad;
            rig.Drone.enabled = false;
        }
        else
        {
            rig.PadGlow = Quad(go.transform, "PadGlow", m.Glow, new Vector3(padSize.x * 1.7f, padSize.y * 2.2f, 1f), OrderPadGlow);
            rig.Pad     = Quad(go.transform, "Pad", m.Pad, new Vector3(padSize.x, padSize.y, 1f), OrderPad);
            rig.PadCore = Quad(go.transform, "Lens", m.Lens, new Vector3(padSize.x * 0.55f, padSize.y * 0.8f, 1f), OrderLens);
        }
        rig.Charge  = Quad(go.transform, "Charge", chargeRing ? m.Point : m.Flare,
                           chargeRing ? Vector3.one * 0.9f : new Vector3(1.8f, 1.1f, 1f), OrderCharge);
        if (chargeRing) rig.ChargeRing = Quad(go.transform, "ChargeRing", m.Ring, Vector3.one * 1.1f, OrderRing);

        // 빔은 발사판 밖(루트 바로 아래)에 둔다 — 위치/회전을 월드로 직접 잡는다
        rig.Beam = Quad(parent, $"Beam_{name}", m.Beam, Vector3.one, OrderBeam);
        return rig;
    }

    private static Impact BuildImpact(Transform parent, Mats m)
    {
        var go = new GameObject("Impact");
        go.transform.SetParent(parent, false);

        var impact = new Impact
        {
            Glow  = Quad(go.transform, "Glow", m.Glow, new Vector3(3f, 2f, 1f), OrderImpactGlow),
            Ring  = Quad(go.transform, "Ring", m.Ring, new Vector3(1.6f, 0.65f, 1f), OrderImpactRing),
            Flash = Quad(go.transform, "Flash", m.Point, Vector3.one * 2.2f, OrderImpactFlash),
            Flare = Quad(go.transform, "Flare", m.Flare, new Vector3(3.2f, 2.2f, 1f), OrderImpactFlare),
        };
        impact.Sparks = Sparks(go.transform, m.Spark);
        return impact;
    }

    private static void WriteRigs(SerializedObject so, params Rig[] rigs)
    {
        var list = so.FindProperty("_emitters");
        list.arraySize = rigs.Length;
        for (int i = 0; i < rigs.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root").objectReferenceValue       = rigs[i].Root;
            e.FindPropertyRelative("Offset").vector2Value             = rigs[i].Offset;
            e.FindPropertyRelative("Depth").floatValue                = rigs[i].Depth;
            e.FindPropertyRelative("Pad").objectReferenceValue        = rigs[i].Pad;
            e.FindPropertyRelative("PadCore").objectReferenceValue    = rigs[i].PadCore;
            e.FindPropertyRelative("PadGlow").objectReferenceValue    = rigs[i].PadGlow;
            e.FindPropertyRelative("Charge").objectReferenceValue     = rigs[i].Charge;
            e.FindPropertyRelative("ChargeRing").objectReferenceValue = rigs[i].ChargeRing;
            e.FindPropertyRelative("Beam").objectReferenceValue       = rigs[i].Beam;
            e.FindPropertyRelative("Drone").objectReferenceValue      = rigs[i].Drone;
            e.FindPropertyRelative("DroneIdle").objectReferenceValue  = rigs[i].Art?.Idle;
            e.FindPropertyRelative("DroneFire").objectReferenceValue  = rigs[i].Art?.Fire;
            e.FindPropertyRelative("DroneFacing").floatValue          = DroneFacing;
            e.FindPropertyRelative("Muzzle").floatValue               = rigs[i].Drone != null ? DroneMuzzle : 0f;
        }
    }

    private static void WriteImpact(SerializedObject so, Impact impact)
    {
        so.FindProperty("_impactFlash").objectReferenceValue = impact.Flash;
        so.FindProperty("_impactRing").objectReferenceValue  = impact.Ring;
        so.FindProperty("_impactFlare").objectReferenceValue = impact.Flare;
        so.FindProperty("_impactGlow").objectReferenceValue  = impact.Glow;
        so.FindProperty("_sparks").objectReferenceValue      = impact.Sparks;
    }

    private static Renderer Quad(Transform parent, string name, Material mat, Vector3 scale, int order)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.sortingLayerName = FxLayer;
        r.sortingOrder = order;
        r.enabled = false;
        return r;
    }

    /// <summary>착탄 스파크 — 위쪽 부채꼴로 튀어 늘어진 빛줄기, 중력으로 떨어지며 줄어든다. 방출은 SkyLaserFx가 Emit으로 한다.</summary>
    private static ParticleSystem Sparks(Transform parent, Material mat)
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.34f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.24f);
        main.gravityModifier = 2.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 72f;
        shape.radius = 0.08f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 콘 +Z → 월드 위쪽

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 3f;
        limit.dampen = 0.12f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.06f;
        renderer.lengthScale = 1.6f;
        renderer.sharedMaterial = mat;
        renderer.sortingLayerName = FxLayer;
        renderer.sortingOrder = OrderSparks;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    /// <summary>플레이 모드의 실험실 씬에서 처음부터 재생하며 프레임을 Temp/FxCapture/SkyLaser에 저장한다.</summary>
    [MenuItem("Tools/USW/Fx/Capture Sky Laser Lab (Play Mode)")]
    public static void CaptureLab()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[SkyLaserFxLab] 플레이 모드에서 실행"); return; }
        var capture = Object.FindFirstObjectByType<FxLabCapture>();
        if (capture == null) { Debug.LogWarning("[SkyLaserFxLab] FxLabCapture 없음 — FxLab_SkyLaser 씬을 여세요"); return; }
        capture.Capture();
    }

    // ── 실험실 씬 ──────────────────────────────────────────

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬(Single)을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 fileID 0으로 풀림 — PenaltyFxLabBuilder 참고)
        // 실험실은 드론 버전을 우선 보여 준다 (없으면 발사판 버전)
        var pulsePrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(PulseDronePath)
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>(PulsePath);
        var sustainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SustainDronePath)
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>(SustainPath);
        var bgSprite      = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        var bossSprite    = AssetDatabase.LoadAssetAtPath<Sprite>(BossPath);

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = false;
        cam.fieldOfView = CameraFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.SetPositionAndRotation(CameraPos, Quaternion.Euler(CameraPitch, 0f, 0f));

        if (bgSprite != null) NewSprite("Battlefield_BG", bgSprite, BgPos, BgScale, "Default", -100);
        else Debug.LogWarning($"[SkyLaserFxLab] 배경 없음: {BgPath}");

        var target = new GameObject("Target").transform;
        target.position = BossPos;
        if (bossSprite != null) NewSprite("Boss_Dummy", bossSprite, BossPos, BossScale, "Default", 0).transform.SetParent(target, true);
        else Debug.LogWarning($"[SkyLaserFxLab] 보스 스프라이트 없음: {BossPath}");

        var pulse   = Instance(pulsePrefab, target);
        var sustain = Instance(sustainPrefab, target);

        var lab = new GameObject("SkyLaserFxLab").AddComponent<SkyLaserFxLab>();
        var labSo = new SerializedObject(lab);
        labSo.FindProperty("_pulse").objectReferenceValue = pulse;
        labSo.FindProperty("_sustain").objectReferenceValue = sustain;
        labSo.ApplyModifiedPropertiesWithoutUndo();

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = lab;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = false;
        capSo.FindProperty("_frameRate").intValue = 30;
        capSo.FindProperty("_duration").floatValue = 5.5f;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 1168);
        capSo.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/SkyLaser";
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static SkyLaserFx Instance(GameObject prefab, Transform target)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = Vector3.zero;
        var fx = go.GetComponent<SkyLaserFx>();
        var so = new SerializedObject(fx);
        so.FindProperty("_target").objectReferenceValue = target;
        so.ApplyModifiedPropertiesWithoutUndo();
        return fx;
    }

    private static GameObject NewSprite(string name, Sprite sprite, Vector3 pos, float scale, string layer, int order)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return go;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
