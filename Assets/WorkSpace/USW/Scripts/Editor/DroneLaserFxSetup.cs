using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 드론 3종(Normal/Buffer/Debuffer)의 색상별 레이저 투사체, 히트 이펙트, ProjectileData를 생성하고
/// 드론 프리팹에 투사체/공격 프레임 스프라이트를 연결한다. 이 도구가 만든 에셋만 다시 덮어쓴다.
/// </summary>
public static class DroneLaserFxSetup
{
    private const string EffectRoot   = "Assets/Imports/Effect_H";
    private const string SpriteRoot   = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs";
    private const string DronePrefabs = "Assets/WorkSpace/USW/Prefab/Unit/DronUnit";
    private const string OutRoot      = "Assets/WorkSpace/USW/Prefab/Effect/DroneLaser";
    private const string DataRoot     = "Assets/WorkSpace/USW/Data/Projectile";
    private const string FxLayer      = "FX";

    private const float HeadWidth      = 0.3f;
    private const float HeadLength     = 0.85f;
    private const float CoreScale      = 0.6f;
    private const float TrailWidth     = 0.45f;
    private const float TrailTime      = 0.08f;
    private const float HitScale       = 1f;
    private const float FlightSeconds  = 0.2f;
    private const float HitLifeSeconds = 1f;
    private const string HitSfx        = "05.Drone_Attack_Hit";

    private struct Variant
    {
        public string Color, Laser, Hit, DronePrefab, AttackSprite;
        public Color Tint;
    }

    private static readonly Variant[] Variants =
    {
        new Variant { Color = "Mint",   Laser = "Laser_1_Mint",   Hit = "Hit_Mint",   DronePrefab = "Drone_Normal_Prefab",   AttackSprite = "Drone_Green_Betang_2",   Tint = new Color(0.25f, 1f, 0.82f) },
        new Variant { Color = "Yellow", Laser = "Laser_2_Yellow", Hit = "Hit_Yellow", DronePrefab = "Drone_Buffer_Prefab",   AttackSprite = "Drone_Yellow_Gammang_2", Tint = new Color(1f, 0.86f, 0.2f) },
        new Variant { Color = "Red",    Laser = "Laser_3_Red",    Hit = "Hit_Red",    DronePrefab = "Drone_Debuffer_Prefab", AttackSprite = "Drone_Red_Deltang_2",    Tint = new Color(1f, 0.3f, 0.28f) },
    };

    [MenuItem("Tools/USW/Drone Laser FX/Build And Assign")]
    public static void BuildAll()
    {
        EnsureFolder(OutRoot);
        EnsureFolder(DataRoot);
        var glow = AssetDatabase.LoadAssetAtPath<Sprite>($"{EffectRoot}/Texture/Glow_1.png");

        foreach (var v in Variants)
        {
            var hit        = BuildHit(v);
            var data       = BuildData(v, hit);
            var projectile = BuildProjectile(v, glow, data);
            AssignDrone(v, projectile);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DroneLaserFxSetup] 드론 레이저 3종 생성/연결 완료");
    }

    private static GameObject BuildHit(Variant v)
    {
        string path = $"{OutRoot}/DroneHit_{v.Color}.prefab";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset($"{EffectRoot}/Prefab/{v.Hit}.prefab", path);

        var root = PrefabUtility.LoadPrefabContents(path);
        root.name = $"DroneHit_{v.Color}";
        root.transform.localScale = Vector3.one * HitScale;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerName = FxLayer;
            r.sortingOrder += 20;
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static ProjectileData BuildData(Variant v, GameObject hit)
    {
        string path = $"{DataRoot}/ProjectileData_DroneLaser_{v.Color}.asset";
        var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<ProjectileData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.movement   = new StraightMovement { duration = FlightSeconds };
        data.fireEffect = null;
        data.hitEffect  = new PrefabProjectileEffect { effectPrefab = hit, duration = HitLifeSeconds, sfxName = HitSfx };
        EditorUtility.SetDirty(data);
        return data;
    }

    private static Projectile BuildProjectile(Variant v, Sprite glow, ProjectileData data)
    {
        string path = $"{OutRoot}/DroneLaser_{v.Color}.prefab";
        var root = new GameObject($"DroneLaser_{v.Color}");

        // 진행 방향 = 로컬 +Y (MovementBase.FaceDirection 기준)
        var head = new GameObject("Head");
        head.transform.SetParent(root.transform, false);
        head.transform.localScale = new Vector3(HeadWidth, HeadLength, 1f);
        var headSr = head.AddComponent<SpriteRenderer>();
        headSr.sprite = glow;
        headSr.color = v.Tint;
        headSr.sortingLayerName = FxLayer;
        headSr.sortingOrder = 32;

        var core = new GameObject("Core");
        core.transform.SetParent(head.transform, false);
        core.transform.localScale = Vector3.one * CoreScale;
        var coreSr = core.AddComponent<SpriteRenderer>();
        coreSr.sprite = glow;
        coreSr.color = Color.white;
        coreSr.sortingLayerName = FxLayer;
        coreSr.sortingOrder = 33;

        var laserSource = AssetDatabase.LoadAssetAtPath<GameObject>($"{EffectRoot}/Prefab/{v.Laser}.prefab");
        var laser = (GameObject)PrefabUtility.InstantiatePrefab(laserSource, root.transform);
        PrefabUtility.UnpackPrefabInstance(laser, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        laser.name = "Trail";
        laser.transform.localPosition = Vector3.zero;
        foreach (var trail in laser.GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.widthMultiplier = TrailWidth;
            trail.time = TrailTime;
            trail.sortingLayerName = FxLayer;
            trail.sortingOrder = 31;
        }

        var projectile = root.AddComponent<Projectile>();
        var so = new SerializedObject(projectile);
        so.FindProperty("_data").objectReferenceValue = data;
        so.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<TrailRendererReset>();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved.GetComponent<Projectile>();
    }

    private static void AssignDrone(Variant v, Projectile projectile)
    {
        string path = $"{DronePrefabs}/{v.DronePrefab}.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var drone = root.GetComponent<DroneUnit>();
        var so = new SerializedObject(drone);
        so.FindProperty("_projectilePrefab").objectReferenceValue = projectile;
        so.FindProperty("_attackSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}/{v.AttackSprite}.png");
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ── 베탕 자폭 드론 (분노 돌격) ─────────────────────────────────

    private const string SelfDestructPrefabPath = "Assets/WorkSpace/USW/Prefab/Unit/DronUnit/ExplodeDronePrefab.prefab";
    private const float  ExplosionFireScale     = 2f;
    private const float  ExplosionCoreScale     = 1.2f;
    private const float  DashTrailWidth         = 0.7f;
    private const float  DashTrailTime          = 0.15f;

    /// <summary>폭발 이펙트(붉은 화염 + 노란 심지 + 연기 퍼프)를 조립하고 자폭 드론에 돌진 트레일과 함께 연결한다.</summary>
    [MenuItem("Tools/USW/Drone Laser FX/Build Self-Destruct FX")]
    public static void BuildSelfDestruct()
    {
        EnsureFolder(OutRoot);
        string path = $"{OutRoot}/DroneSelfDestructExplosion.prefab";
        var root = new GameObject("DroneSelfDestructExplosion");
        AddFxChild(root, $"{EffectRoot}/Prefab/Hit_Red.prefab", "Fire", ExplosionFireScale, 40);
        AddFxChild(root, $"{EffectRoot}/Prefab/Hit_Yellow.prefab", "Core", ExplosionCoreScale, 45);
        PrefabUtility.SaveAsPrefabAsset(root, $"{OutRoot}/DroneSelfDestructExplosion_NoSmoke.prefab");
        AddSmoke(root);
        var explosion = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        var drone = PrefabUtility.LoadPrefabContents(SelfDestructPrefabPath);
        var oldTrail = drone.transform.Find("Trail");
        if (oldTrail != null) Object.DestroyImmediate(oldTrail.gameObject);
        var laser = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>($"{EffectRoot}/Prefab/Laser_3_Red.prefab"), drone.transform);
        PrefabUtility.UnpackPrefabInstance(laser, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        laser.name = "Trail";
        laser.transform.localPosition = Vector3.zero;
        foreach (var trail in laser.GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.widthMultiplier = DashTrailWidth;
            trail.time = DashTrailTime;
            trail.sortingLayerName = FxLayer;
            trail.sortingOrder = 0;
        }
        if (drone.GetComponent<TrailRendererReset>() == null) drone.AddComponent<TrailRendererReset>();
        var sr = drone.GetComponent<SpriteRenderer>();
        if (sr != null) { sr.sortingLayerName = FxLayer; sr.sortingOrder = 10; }

        var so = new SerializedObject(drone.GetComponent<SelfDestructDrone>());
        so.FindProperty("_explosionPrefab").objectReferenceValue = explosion;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(drone, SelfDestructPrefabPath);
        PrefabUtility.UnloadPrefabContents(drone);

        AssetDatabase.SaveAssets();
        Debug.Log("[DroneLaserFxSetup] 자폭 드론 폭발/트레일 생성·연결 완료");
    }

    private const string SmokeMaterialPath = "Assets/Imports/Hovl Studio/HSFiles/Materials/Smoke34bcg.mat";
    private const int    SmokePuffCount    = 6;
    private static readonly Color SmokeColor = new Color(0.2f, 0.17f, 0.16f, 0.9f);

    /// <summary>만화풍 연기 퍼프(Smoke34 4x4 플립북)를 폭발 뒤에 깐다. 파편 없음.</summary>
    private static void AddSmoke(GameObject root)
    {
        var go = new GameObject("Smoke");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startDelay = 0.05f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = SmokeColor;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)SmokePuffCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;

        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = 4;
        sheet.numTilesY = 4;

        var velocity = ps.limitVelocityOverLifetime;
        velocity.enabled = true;
        velocity.limit = 0.3f;
        velocity.dampen = 0.2f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.3f));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
        renderer.sortingLayerName = FxLayer;
        renderer.sortingOrder = 38;
    }

    private static void AddFxChild(GameObject root, string sourcePath, string name, float scale, int sortingOrder)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * scale;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerName = FxLayer;
            r.sortingOrder = sortingOrder + r.sortingOrder;
        }
    }

    private const string PreviewRootName = "# DroneAttackPreview";

    /// <summary>현재 씬에 드론 3마리 + 타겟 + DroneAttackPreview를 배치한다 (기존 프리뷰 루트는 교체).</summary>
    [MenuItem("Tools/USW/Drone Laser FX/Place Preview In Scene")]
    public static void PlacePreview()
    {
        var old = GameObject.Find(PreviewRootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(PreviewRootName);
        var bossPoint = GameObject.Find("Boss_Point");
        var target = new GameObject("Target").transform;
        target.SetParent(root.transform, false);
        Vector3 targetPos = bossPoint != null ? bossPoint.transform.position : new Vector3(0f, 2.5f, 0f);
        targetPos.z = 0f;
        target.position = targetPos;

        var drones = new DroneUnit[Variants.Length];
        for (int i = 0; i < Variants.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{DronePrefabs}/{Variants[i].DronePrefab}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            go.transform.position = new Vector3((i - 1) * 1.3f, -1.6f, 0f);
            drones[i] = go.GetComponent<DroneUnit>();
        }

        var preview = root.AddComponent<DroneAttackPreview>();
        var so = new SerializedObject(preview);
        var list = so.FindProperty("_drones");
        list.arraySize = drones.Length;
        for (int i = 0; i < drones.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = drones[i];
        so.FindProperty("_target").objectReferenceValue = target;

        var origin = new GameObject("SelfDestructOrigin").transform;
        origin.SetParent(root.transform, false);
        origin.position = new Vector3(0f, -2.4f, 0f);
        so.FindProperty("_selfDestructOrigin").objectReferenceValue = origin;
        so.FindProperty("_selfDestructPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<SelfDestructDrone>(SelfDestructPrefabPath);
        so.FindProperty("_explosionWithSmoke").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>($"{OutRoot}/DroneSelfDestructExplosion.prefab");
        so.FindProperty("_explosionNoSmoke").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>($"{OutRoot}/DroneSelfDestructExplosion_NoSmoke.prefab");
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[DroneLaserFxSetup] 프리뷰 배치 완료 — target {targetPos}");
    }

    /// <summary>Game 뷰를 Temp/DroneLaserCapture_*.png 로 저장한다 (플레이 모드 확인용).</summary>
    [MenuItem("Tools/USW/Drone Laser FX/Capture Game View")]
    public static void Capture()
    {
        string path = $"Temp/DroneLaserCapture_{System.DateTime.Now:HHmmss_fff}.png";
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[DroneLaserFxSetup] capture -> {path}");
    }

    private static double _burstEnd, _burstNext;
    private static bool _restoreTimeScale;

    /// <summary>자폭 드론을 즉시 발사하고 0.2배속으로 연속 캡처한다 (연출 확인용, 끝나면 배속 복구).</summary>
    [MenuItem("Tools/USW/Drone Laser FX/Capture Self-Destruct (Slow)")]
    public static void CaptureSelfDestructSlow()
    {
        var preview = Object.FindFirstObjectByType<DroneAttackPreview>();
        if (preview == null || !EditorApplication.isPlaying) return;
        Time.timeScale = 0.2f;
        _restoreTimeScale = true;
        preview.FireSelfDestructNow();
        CaptureBurst();
        _burstEnd = EditorApplication.timeSinceStartup + 9.0;
    }

    /// <summary>플레이 모드에서 3.5초 동안 0.15초 간격으로 Game 뷰를 연속 저장한다.</summary>
    [MenuItem("Tools/USW/Drone Laser FX/Capture Burst")]
    public static void CaptureBurst()
    {
        _burstEnd = EditorApplication.timeSinceStartup + 3.5;
        _burstNext = 0;
        EditorApplication.update -= BurstTick;
        EditorApplication.update += BurstTick;
    }

    private static void BurstTick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now > _burstEnd || !EditorApplication.isPlaying)
        {
            EditorApplication.update -= BurstTick;
            if (_restoreTimeScale) { Time.timeScale = 1f; _restoreTimeScale = false; }
            return;
        }
        if (now < _burstNext) return;
        _burstNext = now + 0.15;
        ScreenCapture.CaptureScreenshot($"Temp/DroneBurst_{System.DateTime.Now:HHmmss_fff}.png");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
