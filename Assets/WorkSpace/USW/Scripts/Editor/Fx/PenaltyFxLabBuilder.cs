using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 패널티 연출(PenaltyRevealFx) 머티리얼·프리팹과 실험실 씬(FxLab_Penalty)을 만든다 (사용자 요청 2026-09-30, 레퍼런스 design/패널티연출.gif).
///   배너 무늬: SPR_UI1000_ButtonArrow_Off 삼각형을 SPR_UI2100_CardFrame_Rare 머리띠처럼 비스듬한 줄로, 조금 간격을 두고, 줄 방향으로 흐르게.
///   실험실: 어두운 필드 대역 + 패널티별 버튼(누를수록 누적 ×N) + 무작위 + 누적 초기화 + 실시간 캡처(FxLabCapture).
/// 다시 실행하면 머티리얼/프리팹/씬을 이 값으로 덮어쓴다 — 인스펙터에서 튜닝한 뒤에는 이 파일 값도 갱신할 것.
/// </summary>
public static class PenaltyFxLabBuilder
{
    public const string MaterialDir = "Assets/WorkSpace/USW/Materials/Fx/Penalty";
    public const string PrefabDir = "Assets/WorkSpace/USW/Prefab/Effect/Penalty";
    public const string PrefabPath = PrefabDir + "/FxPenaltyReveal.prefab";
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_Penalty.unity";
    private const string PenaltyFolder = "Assets/WorkSpace/USW/Data/Endless/Penalties";
    private const string TriangleTexturePath = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/In-game/SPR_UI1000_ButtonArrow_Off.png";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly Vector2 BannerSize = new Vector2(920f, 150f);
    private const float BannerY = 380f; // 화면 중앙 위쪽 (레퍼런스: 보드 위 배너)
    private const float CaptureDuration = 3.9f;

    // 레퍼런스 색: 어두운 남색 판 + 연보라 테두리, 섬광은 노랑
    private static readonly Color BannerBg = new Color(0.13f, 0.12f, 0.23f, 0.96f);
    private static readonly Color BannerTriangle = new Color(0.22f, 0.2f, 0.38f, 1f);
    private static readonly Color BannerBorder = new Color(0.62f, 0.5f, 1f, 1f);
    private static readonly Color FlashYellow = new Color(1f, 0.93f, 0.45f, 1f);
    private static readonly Color SparkOrange = new Color(1f, 0.62f, 0.2f, 1f);

    private static Scene _stage;

    [MenuItem("Tools/USW/Fx/Build Penalty Fx Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[PenaltyFxLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        var triangle = AssetDatabase.LoadAssetAtPath<Texture2D>(TriangleTexturePath);
        if (font == null || triangle == null)
        {
            Debug.LogError($"[PenaltyFxLab] 폰트({font != null}) / 삼각형 텍스처({triangle != null}) 없음 — Assets/Imports 확인");
            return;
        }
        var penalties = LoadPenalties();

        Directory.CreateDirectory(MaterialDir);
        Directory.CreateDirectory(PrefabDir);
        _stage = EditorSceneManager.NewPreviewScene();
        try { BuildPrefab(font, triangle, penalties); }
        finally { EditorSceneManager.ClosePreviewScene(_stage); }
        AssetDatabase.SaveAssets();
        BuildScene(font);
        Debug.Log($"[PenaltyFxLab] 완료: {PrefabPath}, {ScenePath} (패널티 {penalties.Length}종)");
    }

    private static RunPenaltyData[] LoadPenalties() =>
        AssetDatabase.FindAssets("t:RunPenaltyData", new[] { PenaltyFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<RunPenaltyData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null).OrderBy(p => p.Key).ToArray();

    // ── 프리팹 ────────────────────────────────────────────────

    private static void BuildPrefab(TMP_FontAsset font, Texture2D triangle, RunPenaltyData[] penalties)
    {
        var bannerMat = LoadOrCreate("LUP_Banner", "USW/UI/FxPenaltyBanner");
        bannerMat.SetTexture("_IconTex", triangle);
        bannerMat.SetColor("_BgColor", BannerBg);
        bannerMat.SetColor("_BorderColor", BannerBorder);
        bannerMat.SetFloat("_BorderWidth", 6f);
        bannerMat.SetColor("_RimColor", new Color(0.06f, 0.05f, 0.1f, 1f));
        bannerMat.SetFloat("_RimWidth", 4f);
        bannerMat.SetFloat("_Radius", 26f);
        bannerMat.SetColor("_IconColor", BannerTriangle);
        bannerMat.SetFloat("_IconSize", 46f);
        bannerMat.SetVector("_IconGap", new Vector4(16f, 8f)); // 카드 프레임 예시처럼 조금 간격 (사용자 요청)
        bannerMat.SetFloat("_Stagger", 1f);
        bannerMat.SetFloat("_PatternAngle", -18f);            // 카드 프레임 머리띠처럼 오른쪽 아래로 비스듬한 줄
        bannerMat.SetFloat("_IconRotation", 0f);              // 삼각형 자체 회전 (줄 기울기에 더해짐)
        bannerMat.SetFloat("_ScrollSpeed", 40f);
        bannerMat.SetVector("_ScrollDir", new Vector4(1f, 1f)); // 좌하단 → 우상단으로 흐름 (사용자 지정 2026-09-30)
        bannerMat.SetColor("_GrayTint", new Color(0.62f, 0.62f, 0.66f, 1f));
        bannerMat.SetColor("_FlashColor", FlashYellow);
        bannerMat.SetFloat("_GlowWidth", 30f); // 여백(_glowPadding 140) 끝에서 거의 0 — 네모 잘림 방지
        bannerMat.SetFloat("_GlowIntensity", 1.2f);
        EditorUtility.SetDirty(bannerMat);
        var flareMat = Glow("LUP_Flare", core: 1.2f, hardness: 1f, falloff: 1.5f, whiteCore: 1f);
        var ringMat = Glow("LUP_Ring", core: 0f, hardness: 1f, falloff: 1f, whiteCore: 0f);
        ringMat.SetFloat("_InnerAlpha", 0.1f);
        ringMat.SetFloat("_FresnelIntensity", 0.8f);
        ringMat.SetFloat("_FresnelPower", 3f);
        ringMat.SetFloat("_RimIntensity", 0.8f);
        ringMat.SetFloat("_RimWidth", 0.05f);
        ringMat.SetColor("_RimColor", Color.white);
        ringMat.SetFloat("_EdgeSoftness", 0.02f);
        var sparkMat = Glow("LUP_Spark", core: 2f, hardness: 1f, falloff: 2.2f, whiteCore: 1.5f);
        sparkMat.SetFloat("_WhiteCorePower", 5f);
        var debrisMat = LoadOrCreate("LUP_Debris", "USW/UI/FxAlphaMask");
        EditorUtility.SetDirty(debrisMat);

        var root = new GameObject("FxPenaltyReveal", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(UnscaledShaderTime));
        SceneManager.MoveGameObjectToScene(root, _stage);
        var rootRt = (RectTransform)root.transform;
        Stretch(rootRt);
        var group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var fx = root.AddComponent<PenaltyRevealFx>();

        var bannerRoot = NewRect("BannerRoot", rootRt, BannerSize);
        bannerRoot.anchoredPosition = new Vector2(0f, BannerY);
        var flare = NewImage("Flare", bannerRoot, flareMat, new Vector2(1500f, 120f), FlashYellow);
        var ring = NewImage("Ring", bannerRoot, ringMat, new Vector2(1000f, 1000f), FlashYellow);
        var banner = NewImage("Banner", bannerRoot, bannerMat, BannerSize, Color.white);

        var viewport = NewRect("RollViewport", bannerRoot, BannerSize);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = NewRect("RollContent", viewport, new Vector2(BannerSize.x, BannerSize.y));
        var row = NewText("Row_0", content, font, 58f, Color.white, new Vector2(BannerSize.x - 40f, BannerSize.y));

        var result = NewText("Result", bannerRoot, font, 54f, Color.white, new Vector2(BannerSize.x - 40f, BannerSize.y));
        result.lineSpacing = -12f;
        // 배너 위 노란 문구('패널티!')는 사용자 요청으로 제외 (2026-09-30) — PenaltyRevealFx._tag는 비워 둠

        var debris = NewEmitter("Debris", bannerRoot, debrisMat, BannerSize * 0.9f);
        ConfigureEmitter(debris, burst: 16, rate: 0f, duration: 0.01f, max: 24, lifetime: new Vector2(0.6f, 1f), size: new Vector2(26f, 48f),
            speed: new Vector2(500f, 1000f), direction: 90f, spread: 180f, drag: 1.2f, gravity: -1600f, angular: new Vector2(-540f, 540f),
            sway: 0f, twinkle: 0f, colorA: Color.white, colorB: new Color(1f, 0.95f, 0.7f, 1f),
            alpha: new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
        var debrisSo = new SerializedObject(debris);
        debrisSo.FindProperty("_texture").objectReferenceValue = triangle; // 삼각형 조각 (레퍼런스의 주사위 자리)
        debrisSo.ApplyModifiedPropertiesWithoutUndo();
        var sparks = NewEmitter("Sparks", bannerRoot, sparkMat, new Vector2(BannerSize.x * 1.1f, BannerSize.y * 1.6f));
        ConfigureEmitter(sparks, burst: 14, rate: 14f, duration: 0f, max: 60, lifetime: new Vector2(0.8f, 1.6f), size: new Vector2(14f, 30f),
            speed: new Vector2(20f, 90f), direction: 90f, spread: 70f, drag: 0.5f, gravity: 0f, angular: Vector2.zero,
            sway: 20f, twinkle: 0.5f, colorA: SparkOrange, colorB: FlashYellow,
            alpha: new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

        var so = new SerializedObject(fx);
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_bannerRoot").objectReferenceValue = bannerRoot;
        so.FindProperty("_banner").objectReferenceValue = banner;
        so.FindProperty("_rollContent").objectReferenceValue = content;
        so.FindProperty("_rollRowTemplate").objectReferenceValue = row;
        so.FindProperty("_result").objectReferenceValue = result;
        so.FindProperty("_tag").objectReferenceValue = null;
        so.FindProperty("_flare").objectReferenceValue = flare;
        so.FindProperty("_ring").objectReferenceValue = ring;
        so.FindProperty("_sparks").objectReferenceValue = sparks;
        so.FindProperty("_debris").objectReferenceValue = debris;
        so.FindProperty("_bannerSize").vector2Value = BannerSize;
        so.FindProperty("_glowPadding").floatValue = 140f;
        so.FindProperty("_labPenalty").objectReferenceValue = penalties.Length > 1 ? penalties[1] : penalties.FirstOrDefault();
        // 슬롯 후보: 실제 패널티 이름 + 임시 예시 문구 (사용자 요청: 되도록 많은 예시, 없으면 '패널티1패널티1패널티1' 식으로)
        var candidates = penalties.Select(p => p.DisplayName)
            .Concat(Enumerable.Range(1, 12).Select(i => string.Concat(Enumerable.Repeat($"패널티{i}", 3))))
            .ToArray();
        var cand = so.FindProperty("_rollCandidates");
        cand.arraySize = candidates.Length;
        for (int i = 0; i < candidates.Length; i++) cand.GetArrayElementAtIndex(i).stringValue = candidates[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        flare.gameObject.SetActive(false);
        ring.gameObject.SetActive(false);
        int uiLayer = LayerMask.NameToLayer("UI");
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = uiLayer;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    // ── 실험실 씬 ─────────────────────────────────────────────

    private static void BuildScene(TMP_FontAsset font)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬(Single)을 열면 먼저 불러 둔 에셋 참조가 풀려 저장 시 비므로(fileID 0) 씬을 연 뒤 다시 불러온다
        var penalties = LoadPenalties();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        var canvasRt = (RectTransform)canvasGo.transform;

        // 전투 화면 대역: 어두운 필드 + 4×6 칸 (배너가 보드 위에서 어떻게 보이는지 판단용)
        var field = NewImage("FieldStandIn", canvasRt, null, Vector2.zero, new Color(0.2f, 0.13f, 0.19f, 1f));
        Stretch(field.rectTransform);
        var board = NewRect("BoardStandIn", canvasRt, new Vector2(4 * 170f, 6 * 170f));
        board.anchoredPosition = new Vector2(0f, -260f);
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 4; x++)
            {
                var cell = NewImage($"Cell_{x}_{y}", board, null, new Vector2(150f, 150f), new Color(1f, 1f, 1f, 0.08f));
                cell.rectTransform.anchoredPosition = new Vector2((x - 1.5f) * 170f, (y - 2.5f) * 170f);
            }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var fxGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var fxRt = (RectTransform)fxGo.transform;
        fxRt.SetParent(canvasRt, false);
        Stretch(fxRt);
        var fx = fxGo.GetComponent<PenaltyRevealFx>();

        var driver = new GameObject("PenaltyFxLab", typeof(PenaltyFxLab), typeof(UnscaledShaderTime)).GetComponent<PenaltyFxLab>();
        var dSo = new SerializedObject(driver);
        dSo.FindProperty("_fx").objectReferenceValue = fx;
        var list = dSo.FindProperty("_penalties");
        list.arraySize = penalties.Length;
        for (int i = 0; i < penalties.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = penalties[i];
        dSo.ApplyModifiedPropertiesWithoutUndo();

        // 버튼: 패널티별(이름) / 무작위 / 누적 초기화
        var rowA = NewButtonRow(canvasRt, "LabPenalties", -20f);
        for (int i = 0; i < penalties.Length; i++)
            UnityEventTools.AddIntPersistentListener(NewButton(rowA, penalties[i].DisplayName, font).onClick, driver.PlayIndex, i);
        var rowB = NewButtonRow(canvasRt, "LabControls", -122f);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowB, "무작위", font).onClick, driver.PlayRandom);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowB, "누적 초기화", font).onClick, driver.ResetStacks);

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = driver;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = true;
        capSo.FindProperty("_duration").floatValue = CaptureDuration;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 960);
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ── 도우미 ────────────────────────────────────────────────

    private static RectTransform NewRect(string name, RectTransform parent, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }

    private static Image NewImage(string name, RectTransform parent, Material mat, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.material = mat;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color, Vector2 box)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.sizeDelta = box;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static UiFxParticleEmitter NewEmitter(string name, RectTransform parent, Material mat, Vector2 area)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UiFxParticleEmitter));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = area;
        var emitter = go.GetComponent<UiFxParticleEmitter>();
        emitter.material = mat;
        emitter.raycastTarget = false;
        return emitter;
    }

    private static void ConfigureEmitter(UiFxParticleEmitter emitter, int burst, float rate, float duration, int max, Vector2 lifetime,
        Vector2 size, Vector2 speed, float direction, float spread, float drag, float gravity, Vector2 angular, float sway, float twinkle,
        Color colorA, Color colorB, AnimationCurve alpha)
    {
        var so = new SerializedObject(emitter);
        so.FindProperty("_playOnEnable").boolValue = false;
        so.FindProperty("_duration").floatValue = duration;
        so.FindProperty("_rate").floatValue = rate;
        so.FindProperty("_burst").intValue = burst;
        so.FindProperty("_prewarmCount").intValue = 0;
        so.FindProperty("_maxParticles").intValue = max;
        so.FindProperty("_lifetime").vector2Value = lifetime;
        so.FindProperty("_size").vector2Value = size;
        so.FindProperty("_speed").vector2Value = speed;
        so.FindProperty("_direction").floatValue = direction;
        so.FindProperty("_spread").floatValue = spread;
        so.FindProperty("_drag").floatValue = drag;
        so.FindProperty("_acceleration").vector2Value = new Vector2(0f, gravity);
        so.FindProperty("_angularVelocity").vector2Value = angular;
        so.FindProperty("_swayAmplitude").floatValue = sway;
        so.FindProperty("_twinkle").floatValue = twinkle;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(colorA, 0f), new GradientColorKey(colorB, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        so.FindProperty("_startColor").gradientValue = g;
        so.FindProperty("_alphaOverLife").animationCurveValue = alpha;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform NewButtonRow(RectTransform canvasRt, string name, float y)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        row.SetParent(canvasRt, false);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-40f, 90f);
        row.anchoredPosition = new Vector2(0f, y);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = layout.childControlHeight = true;
        return row;
    }

    private static Button NewButton(RectTransform parent, string label, TMP_FontAsset font)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        var text = NewText("Label", (RectTransform)go.transform, font, 34f, Color.white, Vector2.zero);
        Stretch(text.rectTransform);
        return go.GetComponent<Button>();
    }

    private static Material Glow(string name, float core, float hardness, float falloff, float whiteCore)
    {
        var m = LoadOrCreate(name, "USW/UI/FxGlow");
        m.SetColor("_Color", Color.white);
        m.SetFloat("_Intensity", 1f);
        m.SetFloat("_CoreIntensity", core);
        m.SetFloat("_CoreHardness", hardness);
        m.SetFloat("_CoreFalloff", falloff);
        m.SetFloat("_WhiteCore", whiteCore);
        m.SetFloat("_WhiteCorePower", 3f);
        m.SetFloat("_InnerAlpha", 0f);
        m.SetFloat("_FresnelIntensity", 0f);
        m.SetFloat("_RimIntensity", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material LoadOrCreate(string name, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        string path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        return mat;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
