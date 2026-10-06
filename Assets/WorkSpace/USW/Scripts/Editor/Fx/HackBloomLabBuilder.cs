using System;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// FxLab_HackBloom 실험실 생성 — 감망 순간이동 실험실 무대를 복사해 감망 1명만 남기고, 보스를 인게임 악어 Spine으로 바꾼 뒤
/// 델탕(Spine)·델탕 드론·보스 해킹 표식·스택 표시·연파랑 쿠키런 숫자를 얹는다.
/// 씬은 다시 실행할 때마다 덮어쓰고, 숫자 설정 에셋은 없을 때만 만든다(플레이 중 조정값 보존).
/// 원본 감망 실험실 씬·에셋·인게임 데이터는 바꾸지 않는다.
/// </summary>
public static class HackBloomLabBuilder
{
    /// <summary>실험실 씬.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_HackBloom.unity";
    private const string DataPath = "Assets/WorkSpace/USW/Data/HackBloomLab";
    private const string FloaterPath = DataPath + "/HackBloomFloaterSettings.asset";
    private const string LinePath = DataPath + "/HackBloomLines.mat";
    private const string CookieSource = "Assets/WorkSpace/USW/Data/UI/DamageStyleLab/CookieFloaterLabSettings.asset";
    private const string TakePath = "Assets/WorkSpace/USW/Data/GammanTeleportLab/TeleportTake_";
    private const string LaserMaterials = "Assets/WorkSpace/USW/Materials/Fx/SkyLaser/";
    private const string DroneArt = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/";
    private const string DeltanSkeleton = DroneArt + "Spine_Artwork/SPN_Char_Deltan_SkeletonData.asset";
    // 인게임 Crocus.prefab이 쓰는 악어 Spine과 같은 데이터·자식 배치(위치·크기)를 그대로 쓴다.
    private const string CrocusSkeleton = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Boss/Spine_Artwork/Boss_Crocus/스파인_악어_SkeletonData.asset";
    private static readonly Vector3 CrocusLocalPosition = new Vector3(-.005f, -.46f, 0f);
    private const float CrocusScale = .553f;
    private const string CrocusIdle = "idle_1";
    private static readonly Vector3 CrocusHitPoint = new Vector3(0f, 1.1f, 0f);
    private static readonly Vector3 DeltanPosition = new Vector3(1.35f, -2.2f, -.1f);
    private static readonly Vector3 DeltanDroneOffset = new Vector3(.85f, .31f, -.3f);
    private const float SpineScale = .17f;
    // 인게임 상단 HUD 재현: 실제 보스 HP바 프리팹을 인게임 화면(1080 폭)과 같은 자리·폭에 둔다.
    private const string HpBarPrefab = "Assets/WorkSpace/HSD/Prefab/UI/InGame/BossHpBar.prefab";
    // 인게임 측정: 해골 중심 x129·y140, HP바는 해골 중심 오른쪽 20부터 x1013까지 (UI_BossHpBar 부모 안 오프셋과 동일)
    private static readonly Vector2 HpBarPosition = new Vector2(41f, -90f);
    private static readonly Vector2 HpBarSize = new Vector2(864f, 100f);
    private const string BossPortrait = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/In-game/SPR_UI2000_BossPortraitFrame.png";
    private static readonly Vector2 PortraitOffset = new Vector2(-20f, 0f);
    private const float PortraitSize = 102f;
    private const float DroneScale = 1.6f;

    /// <summary>씬을 새로 만들어 저장하고 연다.</summary>
    [MenuItem("Tools/USW/Fx/Build Hack Bloom Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first: " + SceneManager.GetSceneAt(i).path);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GammanTeleportLabBuilder.ScenePath) == null)
            throw new InvalidOperationException("Missing source lab: " + GammanTeleportLabBuilder.ScenePath);
        if (!AssetDatabase.IsValidFolder(DataPath)) AssetDatabase.CreateFolder("Assets/WorkSpace/USW/Data", "HackBloomLab");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(GammanTeleportLabBuilder.ScenePath, ScenePath)) throw new InvalidOperationException("Scene copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath);

        // 감망은 Actor_1 하나만: 다중 비교 그룹은 끄고(감망 랩이 단독 자동재생·자체 GUI를 하지 않도록 참조는 남긴다) 나머지 배우는 지운다.
        var group = Object.FindFirstObjectByType<GammanTeleportGroupLab>();
        var groupSo = new SerializedObject(group);
        groupSo.FindProperty("_members").arraySize = 1;
        groupSo.FindProperty("_count").intValue = 1;
        groupSo.ApplyModifiedPropertiesWithoutUndo();
        group.enabled = false;
        for (int i = 2; i <= 5; i++) { var actor = GameObject.Find("Actor_" + i); if (actor != null) Object.DestroyImmediate(actor); }
        var gamman = GameObject.Find("Actor_1").GetComponentInChildren<GammanTeleportFxLab>();
        var target = GameObject.Find("Target").transform;
        var camera = Camera.main;

        var deltan = SkeletonAnimation.NewSkeletonAnimationGameObject(Load<SkeletonDataAsset>(DeltanSkeleton)).skeletonAnimation;
        deltan.name = "Deltan_VisualOnly";
        deltan.transform.position = DeltanPosition;
        deltan.transform.localScale = Vector3.one * SpineScale;
        deltan.Initialize(false);
        deltan.AnimationName = "idle";
        deltan.loop = true;
        deltan.GetComponent<MeshRenderer>().sortingOrder = 10;
        var drone = new GameObject("DeltanDrone").AddComponent<SpriteRenderer>();
        drone.sprite = Load<Sprite>(DroneArt + "Character_Designs/Drone_Red_Deltang_1.png");
        drone.sortingLayerName = "FX"; drone.sortingOrder = 33;
        drone.transform.position = DeltanPosition + DeltanDroneOffset;
        drone.transform.localScale = Vector3.one * DroneScale;

        // 보스: 더미 그림을 끄고 악어 Spine을 같은 자리에 둔다.
        var dummy = GameObject.Find("Boss_Dummy");
        if (dummy != null) dummy.SetActive(false);
        var crocus = SkeletonAnimation.NewSkeletonAnimationGameObject(Load<SkeletonDataAsset>(CrocusSkeleton)).skeletonAnimation;
        crocus.name = "Crocus_VisualOnly";
        crocus.transform.SetParent(target, false);
        crocus.transform.localPosition = CrocusLocalPosition;
        crocus.transform.localScale = Vector3.one * CrocusScale;
        crocus.Initialize(false);
        crocus.AnimationName = CrocusIdle;
        crocus.loop = true;
        var crocusRenderer = crocus.GetComponent<MeshRenderer>();
        crocusRenderer.sortingLayerName = "Unit"; crocusRenderer.sortingOrder = 0;
        // 감망 빔이 악어 몸 가운데에 꽂히도록 조준점만 옮긴다 (원본 텔레포트 씬은 그대로).
        var hitPoint = new GameObject("Crocus_HitPoint").transform;
        hitPoint.SetParent(target, false);
        hitPoint.localPosition = CrocusHitPoint;
        var gammanSo = new SerializedObject(gamman);
        Ref(gammanSo, "_target", hitPoint);
        gammanSo.ApplyModifiedPropertiesWithoutUndo();

        var lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(LinePath);
        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(lineMaterial, LinePath);
        }
        var marks = new GameObject("Boss_HackStacks_VisualOnly").AddComponent<HackStackMarks>();
        var marksSo = new SerializedObject(marks);
        Ref(marksSo, "_boss", crocus);
        Ref(marksSo, "_lineMaterial", lineMaterial);
        Ref(marksSo, "_glowMaterial", Load<Material>(LaserMaterials + "M_SkyLaser_Glow.mat"));
        Ref(marksSo, "_ringMaterial", Load<Material>(LaserMaterials + "M_SkyLaser_Ring.mat"));
        marksSo.ApplyModifiedPropertiesWithoutUndo();

        var canvasGo = new GameObject("HackBloomCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        var uiRoot = Stretch(new GameObject("Numbers", typeof(RectTransform)), canvasGo.transform);
        var hud = new GameObject("TopHud_Mock", typeof(RectTransform)).GetComponent<RectTransform>();
        hud.SetParent(canvasGo.transform, false);
        hud.anchorMin = hud.anchorMax = hud.pivot = new Vector2(.5f, 1f);
        hud.anchoredPosition = HpBarPosition; hud.sizeDelta = HpBarSize;
        hud.SetAsFirstSibling();
        var hpBar = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(HpBarPrefab), hud);
        var hpRect = (RectTransform)hpBar.transform;
        hpRect.anchorMin = Vector2.zero; hpRect.anchorMax = Vector2.one; hpRect.pivot = new Vector2(.5f, 1f);
        hpRect.anchoredPosition = Vector2.zero; hpRect.sizeDelta = Vector2.zero;
        var portrait = new GameObject("Boss_Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        portrait.rectTransform.SetParent(hud, false);
        portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = new Vector2(0f, .5f);
        portrait.rectTransform.anchoredPosition = PortraitOffset;
        portrait.rectTransform.sizeDelta = Vector2.one * PortraitSize;
        portrait.sprite = Load<Sprite>(BossPortrait); portrait.raycastTarget = false;
        HudText(hud, "Wave_Mock", "WAVE 1", new Vector2(-100f, 36f), TMPro.TextAlignmentOptions.Left);
        HudText(hud, "Timer_Mock", "90:00", new Vector2(HpBarSize.x * .5f - 41f, 36f), TMPro.TextAlignmentOptions.Center);
        var flash = Stretch(new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image)), canvasGo.transform).GetComponent<Image>();
        flash.color = new Color(.92f, .98f, 1f, 0f);
        flash.raycastTarget = false;

        var lab = new GameObject("HackBloomFxLab").AddComponent<HackBloomFxLab>();
        var so = new SerializedObject(lab);
        Ref(so, "_marks", marks);
        Ref(so, "_gamman", gamman);
        Ref(so, "_deltanDrone", drone);
        Ref(so, "_droneIdle", drone.sprite);
        Ref(so, "_droneFire", Load<Sprite>(DroneArt + "Character_Designs/Drone_Red_Deltang_2.png"));
        Ref(so, "_bossVisual", crocus.transform);
        Ref(so, "_boss", target);
        Ref(so, "_hpBar", hpRect);
        Ref(so, "_camera", camera);
        Ref(so, "_uiRoot", uiRoot);
        Ref(so, "_flash", flash);
        Ref(so, "_floaterSettings", FloaterSettings());
        var takes = so.FindProperty("_takes");
        takes.arraySize = 5;
        for (int i = 0; i < 5; i++)
            takes.GetArrayElementAtIndex(i).objectReferenceValue = Load<GammanTeleportTake>(TakePath + (char)('A' + i) + ".asset");
        so.ApplyModifiedPropertiesWithoutUndo();

        var capture = camera.GetComponent<FxLabCapture>();
        if (capture != null)
        {
            var cap = new SerializedObject(capture);
            Ref(cap, "_target", lab);
            cap.FindProperty("_captureOnStart").boolValue = false;
            cap.FindProperty("_duration").floatValue = 6f;
            cap.FindProperty("_frameRate").intValue = 30;
            cap.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/HackBloom";
            cap.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = lab.gameObject;
        Debug.Log("[HackBloomLab] Saved: " + ScenePath);
    }

    // 인게임 쿠키런 숫자 설정을 복사해 색만 연한 파랑으로 바꾼다. 이미 있으면 그대로 둔다.
    private static CookieFloaterLabSettings FloaterSettings()
    {
        var existing = AssetDatabase.LoadAssetAtPath<CookieFloaterLabSettings>(FloaterPath);
        if (existing != null) return existing;
        if (!AssetDatabase.CopyAsset(CookieSource, FloaterPath)) throw new InvalidOperationException("Cannot copy " + CookieSource);
        var settings = AssetDatabase.LoadAssetAtPath<CookieFloaterLabSettings>(FloaterPath);
        settings.NormalColor = new DamageStyleLabSettings.Gradient2(new Color(.8f, .95f, 1f), new Color(.27f, .63f, 1f));
        settings.CriticalColor = new DamageStyleLabSettings.Gradient2(new Color(.62f, .9f, 1f), new Color(.12f, .42f, .95f));
        EditorUtility.SetDirty(settings);
        return settings;
    }

    // 인게임 상단 WAVE·시간 글자 자리 대역 (HP바 기준 위쪽)
    private static void HudText(RectTransform hud, string name, string text, Vector2 position, TMPro.TextAlignmentOptions align)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
        t.rectTransform.SetParent(hud, false);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
        t.rectTransform.pivot = new Vector2(align == TMPro.TextAlignmentOptions.Left ? 0f : .5f, .5f);
        t.rectTransform.anchoredPosition = position; t.rectTransform.sizeDelta = new Vector2(260f, 60f);
        t.text = text; t.fontSize = align == TMPro.TextAlignmentOptions.Left ? 34f : 48f; t.alignment = align; t.raycastTarget = false;
    }

    private static RectTransform Stretch(GameObject go, Transform parent)
    {
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static T Load<T>(string path) where T : Object
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing: " + path);

    private static void Ref(SerializedObject so, string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
}
