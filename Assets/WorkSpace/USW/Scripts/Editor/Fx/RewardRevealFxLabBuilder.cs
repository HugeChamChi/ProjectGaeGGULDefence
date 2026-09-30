using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 보상 공개 이펙트(RewardRevealFx) 머티리얼·프리팹·실험 씬을 만든다.
/// 수치는 design/연출예시1.mp4를 30fps로 쪼개 잰 값 (시간 0 = 상자 착지, 캔버스 1080×2340, 상자 중심 = 위에서 24.5%).
/// 시작 시각은 DOTween이 Play 프레임의 delta를 바로 적용하는 것을 감안해 측정값 +0.033초.
/// 다시 실행하면 머티리얼/프리팹/씬을 이 값으로 덮어쓴다 — 인스펙터에서 튜닝한 값을 보존하려면 실행하지 말 것.
/// </summary>
public static class RewardRevealFxLabBuilder
{
    private const string MaterialDir = "Assets/WorkSpace/USW/Materials/Fx";
    private const string PrefabDir = "Assets/WorkSpace/USW/Prefab/Effect/RewardReveal";
    private const string PrefabPath = PrefabDir + "/RewardRevealFx.prefab";
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_RewardReveal.unity";

    private const float CanvasWidth = 1080f;
    private const float CanvasHeight = 2340f;
    private const float AnchorFromTop = 0.245f;
    private const float ShockwaveMaxRadiusInWidths = 1.8f;

    [MenuItem("Tools/USW/Fx/Build Reward Reveal Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[RewardRevealFxLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Directory.CreateDirectory(MaterialDir);
        Directory.CreateDirectory(PrefabDir);
        var mats = BuildMaterials();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("LabCamera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.transform.position = new Vector3(0f, 0f, -10f);

        var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
        scaler.matchWidthOrHeight = 0f;

        // 위치 확인용 상자 자리 표시 (프리팹에는 포함하지 않음)
        var standIn = NewImage("ChestStandIn", (RectTransform)canvasGo.transform, null, new Vector2(150f, 120f), new Color(0.35f, 0.25f, 0.12f, 1f));
        SetTopAnchor(standIn.rectTransform, AnchorFromTop);

        var fx = BuildFx((RectTransform)canvasGo.transform, mats);

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(fx.gameObject, PrefabPath, InteractionMode.AutomatedAction);

        var capture = camGo.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = fx.GetComponent<RewardRevealFx>();
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[RewardRevealFxLab] 완료: 머티리얼 {MaterialDir}, 프리팹 {AssetDatabase.GetAssetPath(prefab)}, 씬 {ScenePath}");
    }

    private struct Mats
    {
        public Material Rays, Flash, Shockwave, ShockwaveFlash, Glow, CoreGlow, Bokeh, Ember;
    }

    private static Mats BuildMaterials()
    {
        var glowShader = Shader.Find("USW/UI/FxGlow");
        var raysShader = Shader.Find("USW/UI/FxGodRays");

        var rays = LoadOrCreate("FxRays", raysShader);
        rays.SetColor("_Color", Color.white);
        rays.SetFloat("_Intensity", 1f);
        rays.SetFloat("_CountA", 7f);
        rays.SetVector("_WidthA", new Vector4(0.22f, 0.45f));
        rays.SetFloat("_DensityA", 0.65f);
        rays.SetFloat("_SpeedA", 0.012f);
        rays.SetFloat("_SeedA", 3f);
        rays.SetFloat("_CountB", 14f);
        rays.SetVector("_WidthB", new Vector4(0.05f, 0.18f));
        rays.SetFloat("_DensityB", 0.6f);
        rays.SetFloat("_SpeedB", -0.008f);
        rays.SetFloat("_SeedB", 11f);
        rays.SetFloat("_LayerBWeight", 0.15f);
        rays.SetFloat("_BrightnessMin", 0.3f);
        rays.SetFloat("_Softness", 1f);
        rays.SetFloat("_Pulse", 0.25f);
        rays.SetFloat("_PulseSpeed", 1.5f);
        rays.SetFloat("_InnerRadius", 0.04f);
        rays.SetFloat("_RadialFalloff", 1.8f);
        rays.SetFloat("_CenterGlow", 0.35f);
        rays.SetFloat("_LowerDim", 0.92f);

        var flash = LoadOrCreate("FxFlash", glowShader);
        SetGlow(flash, alphaBlend: true, core: 1f, hardness: 1.2f, falloff: 1.6f, whiteCore: 0.15f, whitePower: 1.5f);

        var shock = LoadOrCreate("FxShockwave", glowShader);
        SetGlow(shock, alphaBlend: false, core: 0f, hardness: 1f, falloff: 1f, whiteCore: 0f, whitePower: 4f);
        shock.SetFloat("_InnerAlpha", 0.18f);
        shock.SetFloat("_FresnelIntensity", 0.8f);
        shock.SetFloat("_FresnelPower", 4f);
        shock.SetFloat("_RimIntensity", 0.4f);
        shock.SetFloat("_RimWidth", 0.06f);
        shock.SetColor("_RimColor", new Color(0.85f, 0.5f, 1f, 1f));
        shock.SetFloat("_EdgeSoftness", 0.02f);

        var shockFlash = LoadOrCreate("FxShockwaveFlash", glowShader);
        SetGlow(shockFlash, alphaBlend: false, core: 0.55f, hardness: 3f, falloff: 0.5f, whiteCore: 0.3f, whitePower: 1.5f);
        shockFlash.SetFloat("_RimIntensity", 1f);
        shockFlash.SetFloat("_RimWidth", 0.05f);
        shockFlash.SetColor("_RimColor", new Color(1f, 0.9f, 1f, 1f));
        shockFlash.SetFloat("_EdgeSoftness", 0.02f);

        var glow = LoadOrCreate("FxGlowAdd", glowShader);
        SetGlow(glow, alphaBlend: false, core: 1f, hardness: 1f, falloff: 2f, whiteCore: 0f, whitePower: 4f);

        var coreGlow = LoadOrCreate("FxCoreGlow", glowShader);
        SetGlow(coreGlow, alphaBlend: false, core: 1f, hardness: 1f, falloff: 1.4f, whiteCore: 0.4f, whitePower: 3f);

        var bokeh = LoadOrCreate("FxBokeh", glowShader);
        SetGlow(bokeh, alphaBlend: false, core: 0.8f, hardness: 5f, falloff: 1f, whiteCore: 0f, whitePower: 4f);

        var ember = LoadOrCreate("FxEmber", glowShader);
        SetGlow(ember, alphaBlend: false, core: 1.6f, hardness: 1.5f, falloff: 1.2f, whiteCore: 0.35f, whitePower: 3f);

        AssetDatabase.SaveAssets();
        return new Mats { Rays = rays, Flash = flash, Shockwave = shock, ShockwaveFlash = shockFlash, Glow = glow, CoreGlow = coreGlow, Bokeh = bokeh, Ember = ember };
    }

    private static void SetGlow(Material m, bool alphaBlend, float core, float hardness, float falloff, float whiteCore, float whitePower)
    {
        m.SetColor("_Color", Color.white);
        m.SetFloat("_Intensity", 1f);
        m.SetFloat("_CoreIntensity", core);
        m.SetFloat("_CoreHardness", hardness);
        m.SetFloat("_CoreFalloff", falloff);
        m.SetFloat("_WhiteCore", whiteCore);
        m.SetFloat("_WhiteCorePower", whitePower);
        m.SetFloat("_InnerAlpha", 0f);
        m.SetFloat("_FresnelIntensity", 0f);
        m.SetFloat("_RimIntensity", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)(alphaBlend ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
    }

    private static Material LoadOrCreate(string name, Shader shader)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static RectTransform BuildFx(RectTransform parent, Mats m)
    {
        var root = new GameObject("RewardRevealFx", typeof(RectTransform), typeof(Canvas), typeof(RewardRevealFx), typeof(UnscaledShaderTime));
        var rootRt = (RectTransform)root.transform;
        rootRt.SetParent(parent, false);
        Stretch(rootRt);

        // 뒤 레이어 (상자/카드 뒤): 광선, 중심 글로우, 보케, 불씨 / 앞 레이어 (상자 위): 충격파, 섬광, 코어 글로우
        var back = NewRect("Back", rootRt); Stretch(back);
        var front = NewRect("Front", rootRt); Stretch(front);
        var backAnchor = NewRect("Anchor", back); SetTopAnchor(backAnchor, AnchorFromTop);
        var frontAnchor = NewRect("Anchor", front); SetTopAnchor(frontAnchor, AnchorFromTop);

        var embers = new GameObject("Embers", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiFxParticleEmitter));
        var embersRt = (RectTransform)embers.transform;
        embersRt.SetParent(back, false);
        embersRt.anchorMin = Vector2.zero;
        embersRt.anchorMax = new Vector2(1f, 0.62f);
        embersRt.offsetMin = embersRt.offsetMax = Vector2.zero;
        var emitter = embers.GetComponent<UiFxParticleEmitter>();
        emitter.material = m.Ember;
        ConfigureEmbers(emitter);

        var rays = NewImage("Rays", backAnchor, m.Rays, Vector2.one * 1900f, new Color(0.55f, 0.28f, 1f, 1f));
        var centerGlow = NewImage("CenterGlow", backAnchor, m.Glow, Vector2.one * 480f, new Color(0.75f, 0.2f, 1f, 1f));
        var bokeh = new[]
        {
            NewImage("Bokeh_0", backAnchor, m.Bokeh, Vector2.one * 230f, new Color(0.3f, 0.18f, 0.9f, 1f), new Vector2(0f, 110f)),
            NewImage("Bokeh_1", backAnchor, m.Bokeh, Vector2.one * 200f, new Color(0.3f, 0.18f, 0.9f, 1f), new Vector2(115f, -20f)),
            NewImage("Bokeh_2", backAnchor, m.Bokeh, Vector2.one * 260f, new Color(0.3f, 0.18f, 0.9f, 1f), new Vector2(-95f, -45f)),
        };
        float shockDiameter = CanvasWidth * ShockwaveMaxRadiusInWidths * 2f;
        var shock = NewImage("Shockwave", frontAnchor, m.Shockwave, Vector2.one * shockDiameter, new Color(0.5f, 0.18f, 0.8f, 1f));
        var shockFlash = NewImage("ShockwaveFlash", shock.rectTransform, m.ShockwaveFlash, Vector2.zero, new Color(0.8f, 0.6f, 1f, 1f));
        shockFlash.rectTransform.anchorMin = Vector2.zero;
        shockFlash.rectTransform.anchorMax = Vector2.one;
        shockFlash.rectTransform.offsetMin = shockFlash.rectTransform.offsetMax = Vector2.zero;
        var flash = NewImage("Flash", frontAnchor, m.Flash, Vector2.one * 2200f, new Color(0.55f, 0.33f, 0.95f, 1f));
        var coreGlow = NewImage("CoreGlow", frontAnchor, m.CoreGlow, Vector2.one * 560f, new Color(0.85f, 0.35f, 1f, 1f));

        var so = new SerializedObject(root.GetComponent<RewardRevealFx>());
        SetElement(so, "_flash", flash, start: 0.055f, rise: 0.045f, peak: 0.9f, hold: 0f, fall: 0.03f, rest: 0f,
                   fallEase: DG.Tweening.Ease.OutQuad, scaleFrom: 0.8f, scaleTo: 1.05f, scaleDuration: 0.1f);
        SetElement(so, "_coreGlow", coreGlow, start: 0.09f, rise: 0.035f, peak: 1f, hold: 0f, fall: 0.035f, rest: 0f,
                   fallEase: DG.Tweening.Ease.InQuad, scaleFrom: 0.5f, scaleTo: 1.1f, scaleDuration: 0.12f);
        SetElement(so, "_shockwave", shock, start: 0.163f, rise: 0.02f, peak: 1f, hold: 0.01f, fall: 0.2f, rest: 0f,
                   fallEase: DG.Tweening.Ease.OutCubic, scaleFrom: 0f, scaleTo: 1f, scaleDuration: 0.4f);
        SetElement(so, "_shockwaveFlash", shockFlash, start: 0.163f, rise: 0.01f, peak: 0.8f, hold: 0f, fall: 0.035f, rest: 0f,
                   fallEase: DG.Tweening.Ease.InQuad, scaleFrom: 1f, scaleTo: 1f, scaleDuration: 0f);
        SetElement(so, "_rays", rays, start: 0.14f, rise: 0.13f, peak: 0.75f, hold: 0.03f, fall: 0.25f, rest: 0.38f,
                   fallEase: DG.Tweening.Ease.OutQuad, scaleFrom: 0.5f, scaleTo: 1f, scaleDuration: 0.3f,
                   scaleEase: DG.Tweening.Ease.OutCubic);
        SetElement(so, "_centerGlow", centerGlow, start: 0.14f, rise: 0.1f, peak: 1f, hold: 0.05f, fall: 0.3f, rest: 0.45f,
                   fallEase: DG.Tweening.Ease.OutQuad, scaleFrom: 0.6f, scaleTo: 1f, scaleDuration: 0.2f);

        var bokehProp = so.FindProperty("_bokeh");
        bokehProp.arraySize = bokeh.Length;
        var drifts = new[] { new Vector2(0f, 30f), new Vector2(30f, -5f), new Vector2(-25f, -10f) };
        for (int i = 0; i < bokeh.Length; i++)
        {
            SetElement(so, $"_bokeh.Array.data[{i}]", bokeh[i], start: 0.273f + i * 0.02f, rise: 0.06f, peak: 0.45f, hold: 0.1f,
                       fall: 0.2f, rest: 0f, fallEase: DG.Tweening.Ease.InQuad, scaleFrom: 0.7f, scaleTo: 1f,
                       scaleDuration: 0.3f, drift: drifts[i]);
        }
        so.FindProperty("_embers").objectReferenceValue = emitter;
        so.FindProperty("_embersStart").floatValue = 0.2f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 시작 상태: 모두 숨김 (Play가 켠다)
        foreach (var g in new Graphic[] { rays, centerGlow, shock, shockFlash, flash, coreGlow }) g.gameObject.SetActive(false);
        foreach (var b in bokeh) b.gameObject.SetActive(false);
        return rootRt;
    }

    private static void ConfigureEmbers(UiFxParticleEmitter emitter)
    {
        var so = new SerializedObject(emitter);
        so.FindProperty("_maxParticles").intValue = 80;
        so.FindProperty("_rate").floatValue = 16f;
        so.FindProperty("_burst").intValue = 20;
        so.FindProperty("_prewarmCount").intValue = 10;
        so.FindProperty("_lifetime").vector2Value = new Vector2(1.5f, 3f);
        so.FindProperty("_size").vector2Value = new Vector2(16f, 30f);
        so.FindProperty("_speed").vector2Value = new Vector2(15f, 45f);
        so.FindProperty("_direction").floatValue = 90f;
        so.FindProperty("_spread").floatValue = 25f;
        so.FindProperty("_swayAmplitude").floatValue = 12f;
        so.FindProperty("_twinkle").floatValue = 0.6f;
        so.FindProperty("_twinkleSpeed").vector2Value = new Vector2(5f, 12f);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.35f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        so.FindProperty("_startColor").gradientValue = gradient;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetElement(SerializedObject so, string path, Graphic g, float start, float rise, float peak, float hold,
                                   float fall, float rest, DG.Tweening.Ease fallEase, float scaleFrom, float scaleTo,
                                   float scaleDuration, DG.Tweening.Ease scaleEase = DG.Tweening.Ease.OutQuad,
                                   Vector2 drift = default)
    {
        so.FindProperty($"{path}.Graphic").objectReferenceValue = g;
        so.FindProperty($"{path}.Pulse.Start").floatValue = start;
        so.FindProperty($"{path}.Pulse.Rise").floatValue = rise;
        so.FindProperty($"{path}.Pulse.Peak").floatValue = peak;
        so.FindProperty($"{path}.Pulse.Hold").floatValue = hold;
        so.FindProperty($"{path}.Pulse.Fall").floatValue = fall;
        so.FindProperty($"{path}.Pulse.Rest").floatValue = rest;
        so.FindProperty($"{path}.Pulse.FallEase").intValue = (int)fallEase;
        so.FindProperty($"{path}.Pulse.ScaleFrom").floatValue = scaleFrom;
        so.FindProperty($"{path}.Pulse.ScaleTo").floatValue = scaleTo;
        so.FindProperty($"{path}.Pulse.ScaleDuration").floatValue = scaleDuration;
        so.FindProperty($"{path}.Pulse.ScaleEase").intValue = (int)scaleEase;
        so.FindProperty($"{path}.Pulse.Drift").vector2Value = drift;
    }

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image NewImage(string name, RectTransform parent, Material mat, Vector2 size, Color color, Vector2 pos = default)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.material = mat;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetTopAnchor(RectTransform rt, float fromTop)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f - fromTop);
        rt.anchoredPosition = Vector2.zero;
    }
}
