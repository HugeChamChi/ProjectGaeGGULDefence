using System.IO;
using DG.Tweening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 레벨업 연출 이펙트 프리팹을 만든다 (요구사항 ① 경험치 MAX 버스트 ~ ④ 카드 선택).
/// 화풍: 레퍼런스 1(design/연출예시1.mp4)식 빛 번짐 — 사용자가 ①의 A안(만화풍 도형 FxShape)과 B안(빛 번짐 FxGlow)을 비교해
/// B안을 선택 (2026-09-29). 이후 ②~④도 빛 번짐 기준. A안 프리팹은 비교용으로 유지.
/// 흐름은 레퍼런스 3(흰 판 공개 → 선택 → 아이콘 수집), 카드 등급 테두리는 레퍼런스 4에서 차용.
/// 다시 실행하면 머티리얼/프리팹을 이 값으로 덮어쓴다 — 인스펙터에서 튜닝한 뒤에는 이 파일 값도 갱신할 것.
/// </summary>
public static class LevelUpFxPrefabBuilder
{
    public const string MaterialDir = "Assets/WorkSpace/USW/Materials/Fx/LevelUp";
    public const string PrefabDir = "Assets/WorkSpace/USW/Prefab/Effect/LevelUp";
    public const string GaugeBurstPath = PrefabDir + "/FxLevelUp_GaugeBurst.prefab";
    /// <summary>① B안: 레퍼런스 1(연출예시1.mp4) 방식의 빛 번짐 버전 (사용자 요청 2026-09-29, A안과 비교용).</summary>
    public const string GaugeBurstGlowPath = PrefabDir + "/FxLevelUp_GaugeBurst_Glow.prefab";
    /// <summary>② 등급 연출 (레벨업 패널 안 카드 뒤, TierRevealFx).</summary>
    public const string TierRevealPath = PrefabDir + "/FxLevelUp_TierReveal.prefab";
    /// <summary>③ 카드 출력 이펙트 (LevelUpRevealSequence 슬롯 B, CardRevealFx).</summary>
    public const string CardRevealPath = PrefabDir + "/FxLevelUp_CardReveal.prefab";
    /// <summary>③ B안: 카드 테두리 스프라이트 모양을 따라 빛나는 버전 (A안 = 위 일직선 버전, 사용자가 비교용으로 둘 다 요청 2026-09-30).</summary>
    public const string CardRevealShapePath = PrefabDir + "/FxLevelUp_CardReveal_Shape.prefab";
    /// <summary>④ 카드 선택 이펙트 (LevelUpSelectSequence 선택 슬롯, CardRevealFx + 섬광·파동).</summary>
    public const string CardSelectPath = PrefabDir + "/FxLevelUp_CardSelect.prefab";

    // 등급색 (사용자 지정 2026-09-29): 레어 파랑 / 에픽 보라 / 레전더리 빨강
    private static readonly Color RareBlue = new Color(0.3f, 0.62f, 1f);
    private static readonly Color EpicPurple = new Color(0.66f, 0.35f, 1f);
    private static readonly Color LegendRed = new Color(1f, 0.25f, 0.28f);

    // 우리 게임 UI 외곽선(짙은 갈색) / 경험치 금색
    private static readonly Color Outline = new Color(0.22f, 0.11f, 0.04f, 1f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.16f, 1f);
    private static readonly Color PaleGold = new Color(1f, 0.95f, 0.6f, 1f);

    // 임시 오브젝트는 미리보기 씬에 만들어 열린 씬을 더럽히지 않는다.
    private static Scene _stage;

    [MenuItem("Tools/USW/Fx/Build LevelUp Fx Prefabs")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(MaterialDir);
        Directory.CreateDirectory(PrefabDir);
        _stage = EditorSceneManager.NewPreviewScene();
        try
        {
            BuildGaugeBurst();
            BuildGaugeBurstGlow();
            BuildTierReveal();
            BuildCardReveal();
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(_stage);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelUpFxPrefabs] 완료: {GaugeBurstPath}, {GaugeBurstGlowPath}, {TierRevealPath}, {CardRevealPath}, {CardRevealShapePath}, {CardSelectPath}");
    }

    // ── ① 경험치 MAX 버스트 (LevelUpRevealSequence 슬롯 A, 소환 버튼 위치에서 생성) ──────────────

    private static void BuildGaugeBurst()
    {
        var starburst = Shape("LU_Starburst", 0f, m =>
        {
            m.SetFloat("_Spikes", 10f);
            m.SetFloat("_InnerRadius", 0.58f);
            m.SetFloat("_SpikeMin", 0.7f);
            m.SetFloat("_Seed", 3f);
            m.SetFloat("_OutlineWidth", 0.05f);
            m.SetFloat("_CoreSize", 0.62f);
        });
        var ring = Shape("LU_Ring", 1f, m =>
        {
            m.SetFloat("_RingRadius", 0.88f);
            m.SetFloat("_Thickness", 0.16f);
            m.SetFloat("_OutlineWidth", 0.02f);
            m.SetFloat("_CoreSize", 0.35f);
        });
        var sparkle = Shape("LU_Sparkle", 2f, m =>
        {
            m.SetFloat("_StarPinch", 0.42f);
            m.SetFloat("_OutlineWidth", 0.09f);
            m.SetFloat("_CoreSize", 0.45f);
        });
        var flash = Glow("LU_Flash", core: 1f, hardness: 4f, falloff: 1f, whiteCore: 0f);
        var mote = Glow("LU_Mote", core: 1.6f, hardness: 2f, falloff: 1f, whiteCore: 1f);
        var lines = SpeedLines("LU_SpeedLines");

        var root = NewRoot("FxLevelUp_GaugeBurst");
        var rt = (RectTransform)root.transform;
        var speedLines = NewImage("SpeedLines", rt, lines, 1300f, Color.white);
        var ringImg = NewImage("Ring", rt, ring, 1000f, Gold);
        var starImg = NewImage("Starburst", rt, starburst, 640f, Gold);
        var flashImg = NewImage("Flash", rt, flash, 460f, Color.white);
        var motes = NewEmitter("Motes", rt, mote, new Vector2(160f, 60f), new Vector2(0f, 40f));
        var sparkles = NewEmitter("Sparkles", rt, sparkle, new Vector2(120f, 60f), Vector2.zero);

        ConfigureEmitter(motes, burst: 30, max: 40, lifetime: new Vector2(0.55f, 0.9f), size: new Vector2(16f, 28f),
            speed: new Vector2(2200f, 3400f), direction: 90f, spread: 16f, drag: 0.6f, angular: Vector2.zero, twinkle: 0.3f,
            colorA: Gold, colorB: Color.white,
            alpha: new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)),
            sizeCurve: new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f)), stretch: 0.04f);
        ConfigureEmitter(sparkles, burst: 12, max: 20, lifetime: new Vector2(0.45f, 0.8f), size: new Vector2(44f, 84f),
            speed: new Vector2(500f, 1100f), direction: 90f, spread: 75f, drag: 4f, angular: new Vector2(-360f, 360f), twinkle: 0f,
            colorA: PaleGold, colorB: Gold,
            alpha: new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)),
            sizeCurve: new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.6f)));

        var so = new SerializedObject(root.GetComponent<UiFxTimeline>());
        var tracks = so.FindProperty("_tracks");
        tracks.arraySize = 4;
        SetTrack(tracks.GetArrayElementAtIndex(0), flashImg, start: 0f, rise: 0.03f, peak: 1f, hold: 0.02f, fall: 0.12f,
                 fallEase: Ease.OutQuad, scaleFrom: 0.5f, scaleTo: 1.1f, scaleDuration: 0.1f, scaleEase: Ease.OutQuad);
        SetTrack(tracks.GetArrayElementAtIndex(1), starImg, start: 0f, rise: 0.02f, peak: 1f, hold: 0.1f, fall: 0.14f,
                 fallEase: Ease.InQuad, scaleFrom: 0.25f, scaleTo: 1f, scaleDuration: 0.14f, scaleEase: Ease.OutBack, rotate: 20f);
        SetTrack(tracks.GetArrayElementAtIndex(2), ringImg, start: 0.03f, rise: 0.02f, peak: 1f, hold: 0.1f, fall: 0.2f,
                 fallEase: Ease.InQuad, scaleFrom: 0.35f, scaleTo: 1f, scaleDuration: 0.36f, scaleEase: Ease.OutCubic,
                 floatName: "_Thickness", floatFrom: 0.2f, floatTo: 0.02f);
        SetTrack(tracks.GetArrayElementAtIndex(3), speedLines, start: 0.02f, rise: 0.03f, peak: 0.9f, hold: 0.06f, fall: 0.18f,
                 fallEase: Ease.InQuad, scaleFrom: 0.55f, scaleTo: 1.15f, scaleDuration: 0.3f, scaleEase: Ease.OutQuad);
        var cues = so.FindProperty("_emitters");
        cues.arraySize = 2;
        cues.GetArrayElementAtIndex(0).FindPropertyRelative("Emitter").objectReferenceValue = sparkles;
        cues.GetArrayElementAtIndex(0).FindPropertyRelative("Start").floatValue = 0.02f;
        cues.GetArrayElementAtIndex(1).FindPropertyRelative("Emitter").objectReferenceValue = motes;
        cues.GetArrayElementAtIndex(1).FindPropertyRelative("Start").floatValue = 0.04f;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var g in new Graphic[] { speedLines, ringImg, starImg, flashImg }) g.gameObject.SetActive(false);
        SavePrefab(root, GaugeBurstPath);
    }

    // ── ① B안: 빛 번짐 버스트 (레퍼런스 1의 섬광·후광·빛 구체·부드러운 광선·보케·불씨) ─────────────
    //   노란 게이지 위에서 터지므로 후광/광선은 주황빛 금색, 코어만 흰색에 가깝게.

    private static readonly Color WarmGold = new Color(1f, 0.66f, 0.2f, 1f);
    private static readonly Color HotWhite = new Color(1f, 0.92f, 0.7f, 1f);

    private static void BuildGaugeBurstGlow()
    {
        var coreMat = Glow("LUG_Core", core: 1.4f, hardness: 1f, falloff: 1.6f, whiteCore: 1.2f);
        coreMat.SetFloat("_WhiteCorePower", 2f);
        var haloMat = Glow("LUG_Halo", core: 1f, hardness: 1f, falloff: 2.6f, whiteCore: 0f);
        var bubbleMat = Glow("LUG_Bubble", core: 0f, hardness: 1f, falloff: 1f, whiteCore: 0f);
        bubbleMat.SetFloat("_InnerAlpha", 0.12f);
        bubbleMat.SetFloat("_FresnelIntensity", 0.8f);
        bubbleMat.SetFloat("_FresnelPower", 3f);
        bubbleMat.SetFloat("_RimIntensity", 0.6f);
        bubbleMat.SetFloat("_RimWidth", 0.06f);
        bubbleMat.SetColor("_RimColor", new Color(1f, 0.85f, 0.5f, 1f));
        bubbleMat.SetFloat("_EdgeSoftness", 0.02f);
        var bokehMat = Glow("LUG_Bokeh", core: 0.8f, hardness: 5f, falloff: 1f, whiteCore: 0f);
        AssetDatabase.DeleteAsset($"{MaterialDir}/LUG_Ember.mat"); // 불씨 제외 전 버전이 만든 머티리얼 정리
        var raysMat = SoftRays("LUG_Rays");

        var root = NewRoot("FxLevelUp_GaugeBurst_Glow");
        root.AddComponent<UnscaledShaderTime>(); // 광선 회전이 레벨업 일시정지(timeScale 0) 중에도 흐르도록
        var rt = (RectTransform)root.transform;
        var halo = NewImage("Halo", rt, haloMat, 1500f, WarmGold);
        var rays = NewImage("Rays", rt, raysMat, 1600f, new Color(1f, 0.72f, 0.3f, 1f));
        var bubble = NewImage("Bubble", rt, bubbleMat, 1400f, WarmGold);
        var bokehPos = new[] { new Vector2(0f, 160f), new Vector2(150f, 60f), new Vector2(-140f, 90f) };
        var bokehSize = new[] { 220f, 180f, 240f };
        var bokeh = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            bokeh[i] = NewImage($"Bokeh_{i}", rt, bokehMat, bokehSize[i], new Color(1f, 0.58f, 0.2f, 1f));
            bokeh[i].rectTransform.anchoredPosition = bokehPos[i];
        }
        // 입자는 모두 제외 (사용자 결정): 사방 불씨(2026-09-29, 촌스러움), 위로 솟는 빛줄기(2026-09-30, ② 화면까지 불똥처럼 보임)
        var core = NewImage("Core", rt, coreMat, 700f, HotWhite);

        var so = new SerializedObject(root.GetComponent<UiFxTimeline>());
        var tracks = so.FindProperty("_tracks");
        tracks.arraySize = 7;
        SetTrack(tracks.GetArrayElementAtIndex(0), halo, start: 0f, rise: 0.06f, peak: 0.75f, hold: 0.05f, fall: 0.45f,
                 fallEase: Ease.OutQuad, scaleFrom: 0.5f, scaleTo: 1.1f, scaleDuration: 0.3f, scaleEase: Ease.OutQuad);
        SetTrack(tracks.GetArrayElementAtIndex(1), rays, start: 0.02f, rise: 0.08f, peak: 0.9f, hold: 0.05f, fall: 0.4f,
                 fallEase: Ease.OutQuad, scaleFrom: 0.4f, scaleTo: 1.05f, scaleDuration: 0.35f, scaleEase: Ease.OutCubic, rotate: 12f);
        SetTrack(tracks.GetArrayElementAtIndex(2), bubble, start: 0.03f, rise: 0.02f, peak: 1f, hold: 0.02f, fall: 0.28f,
                 fallEase: Ease.OutCubic, scaleFrom: 0.1f, scaleTo: 1f, scaleDuration: 0.38f, scaleEase: Ease.OutQuad);
        var drift = new[] { new Vector2(0f, 60f), new Vector2(60f, 20f), new Vector2(-60f, 30f) };
        for (int i = 0; i < 3; i++)
            SetTrack(tracks.GetArrayElementAtIndex(3 + i), bokeh[i], start: 0.08f + i * 0.02f, rise: 0.06f, peak: 0.6f, hold: 0.1f,
                     fall: 0.3f, fallEase: Ease.InQuad, scaleFrom: 0.6f, scaleTo: 1f, scaleDuration: 0.3f, scaleEase: Ease.OutQuad,
                     drift: drift[i]);
        SetTrack(tracks.GetArrayElementAtIndex(6), core, start: 0f, rise: 0.03f, peak: 1f, hold: 0.03f, fall: 0.25f,
                 fallEase: Ease.OutQuad, scaleFrom: 0.4f, scaleTo: 1.1f, scaleDuration: 0.12f, scaleEase: Ease.OutQuad);
        so.FindProperty("_emitters").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var g in new Graphic[] { halo, rays, bubble, core }) g.gameObject.SetActive(false);
        foreach (var b in bokeh) b.gameObject.SetActive(false);
        SavePrefab(root, GaugeBurstGlowPath);
    }

    // ── ② 등급 연출 (TierRevealFx) — 레어 파랑 / 에픽 보라 / 레전더리 빨강(에픽에서 올라감) ──────────
    //   루트는 레벨업 패널을 꽉 채우고, Anchor를 카드 컨테이너 중심에 맞춘다 (실험실 빌더·게임 연결 시).

    private static void BuildTierReveal()
    {
        var backGlowMat = Glow("LUT_BackGlow", core: 1f, hardness: 1f, falloff: 2.2f, whiteCore: 0f);
        var bubbleMat = Glow("LUT_Bubble", core: 0f, hardness: 1f, falloff: 1f, whiteCore: 0f);
        bubbleMat.SetFloat("_InnerAlpha", 0.1f);
        bubbleMat.SetFloat("_FresnelIntensity", 0.8f);
        bubbleMat.SetFloat("_FresnelPower", 3f);
        bubbleMat.SetFloat("_RimIntensity", 0.7f);
        bubbleMat.SetFloat("_RimWidth", 0.05f);
        bubbleMat.SetColor("_RimColor", Color.white);
        bubbleMat.SetFloat("_RimTint", 0.55f);
        bubbleMat.SetFloat("_EdgeSoftness", 0.02f);
        var raysMat = SoftRays("LUT_Rays");
        raysMat.SetFloat("_CountA", 9f);
        raysMat.SetVector("_WidthA", new Vector4(0.14f, 0.38f));
        raysMat.SetFloat("_SpeedA", 0.02f);
        raysMat.SetFloat("_SpeedB", -0.012f);
        raysMat.SetFloat("_CenterGlow", 0.3f);
        raysMat.SetFloat("_RadialFalloff", 1.4f);
        var columnsMat = LoadOrCreate("LUT_Columns", "USW/UI/FxLightColumns");
        columnsMat.SetColor("_Color", Color.white);
        columnsMat.SetFloat("_Intensity", 1f);
        // 고른 간격 (사용자 피드백: 간격이 들쭉날쭉하고 오른쪽이 비어 보임) — 모든 칸에 같은 폭, 밝기만 살짝 차이
        columnsMat.SetFloat("_Lanes", 10f);
        columnsMat.SetFloat("_Density", 1f);
        columnsMat.SetFloat("_Jitter", 0.12f);
        columnsMat.SetFloat("_WidthMin", 0.45f);
        columnsMat.SetFloat("_WidthMax", 0.6f);
        columnsMat.SetFloat("_BrightnessMin", 0.7f);
        columnsMat.SetFloat("_Softness", 0.7f);
        columnsMat.SetFloat("_VerticalFalloff", 1.2f);
        columnsMat.SetFloat("_Rise", 1f);
        columnsMat.SetFloat("_FlowSpeed", 0.8f);
        columnsMat.SetFloat("_FlowAmount", 0.35f);
        columnsMat.SetFloat("_Seed", 4f);
        EditorUtility.SetDirty(columnsMat);
        var bokehMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/LUG_Bokeh.mat");
        var coreMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/LUG_Core.mat");

        var root = NewRoot("FxLevelUp_TierReveal");
        // 패널 안에 들어가는 요소라 별도 Canvas/타임라인이 필요 없다 — 패널이 투명(알파 0)일 때 켜진 하위 Canvas가
        // 몇 프레임 늦게 그려져 패널 배경이 비치던 문제를 피한다.
        Object.DestroyImmediate(root.GetComponent<UiFxTimeline>());
        Object.DestroyImmediate(root.GetComponent<Canvas>());
        root.AddComponent<UnscaledShaderTime>();
        var fx = root.AddComponent<TierRevealFx>();
        var rt = (RectTransform)root.transform;
        StretchFull(rt);

        var backdrop = NewImage("Backdrop", rt, null, 100f, Color.black);
        StretchFull(backdrop.rectTransform);
        var columns = NewImage("Columns", rt, columnsMat, 100f, Color.white);
        StretchFull(columns.rectTransform);
        columns.rectTransform.pivot = new Vector2(0.5f, 0f); // 아래에서 위로 차오르도록 (scaleY)

        var anchor = new GameObject("Anchor", typeof(RectTransform)).GetComponent<RectTransform>();
        anchor.SetParent(rt, false);
        var backGlow = NewImage("BackGlow", anchor, backGlowMat, 1900f, Color.white);
        var rays = NewImage("Rays", anchor, raysMat, 2400f, Color.white);
        var bubble = NewImage("Bubble", anchor, bubbleMat, 2000f, Color.white);
        var bokehPos = new[] { new Vector2(-260f, 240f), new Vector2(280f, 180f), new Vector2(-230f, -260f), new Vector2(250f, -220f) };
        var bokehSize = new[] { 260f, 220f, 300f, 240f };
        var bokeh = new Image[bokehPos.Length];
        for (int i = 0; i < bokeh.Length; i++)
        {
            bokeh[i] = NewImage($"Bokeh_{i}", anchor, bokehMat, bokehSize[i], Color.white);
            bokeh[i].rectTransform.anchoredPosition = bokehPos[i];
        }
        // 중심으로 빨려 드는 빛줄기(Gather)는 사용자 결정으로 제외 ("불똥" 같음, 2026-09-30) — 후광이 차오르는 예고만 남긴다.
        //   다시 쓰려면 UiFxParticleEmitter(_converge 켬, LU_Mote 머티리얼)를 Anchor 아래 만들고 _gather에 연결.
        var core = NewImage("Core", anchor, coreMat, 800f, Color.white);

        // 빛 알갱이 (사용자 요청 2026-09-30): 글로우가 꺼진 뒤 선택 내내, 흰 중심 + 등급색 번짐의 작은 점이
        // 패널 아래 절반에서 반짝이며 천천히 떠올라 패널 중간쯤에서 녹아 사라진다 (design/수정제안1.png, 연출예시1.mp4 0~1초).
        // 색은 TierRevealFx가 등급색으로 칠한다(곱). 매 프레임 메시를 다시 만드므로 자체 Canvas로 패널 리빌드와 분리.
        var moteMat = Glow("LUT_Mote", core: 2.2f, hardness: 1f, falloff: 2.2f, whiteCore: 1.5f);
        moteMat.SetFloat("_WhiteCorePower", 5f);
        var motes = NewEmitter("Motes", rt, moteMat, Vector2.zero, Vector2.zero);
        motes.gameObject.AddComponent<Canvas>();
        var motesRt = motes.rectTransform;
        motesRt.anchorMin = Vector2.zero;
        motesRt.anchorMax = new Vector2(1f, 0.5f);
        motesRt.offsetMin = motesRt.offsetMax = Vector2.zero;
        ConfigureEmitter(motes, burst: 0, max: 84, lifetime: new Vector2(3f, 5f), size: new Vector2(21f, 40f),
            speed: new Vector2(25f, 70f), direction: 90f, spread: 12f, drag: 0f, angular: Vector2.zero, twinkle: 0.6f,
            colorA: Color.white, colorB: new Color(1f, 1f, 1f, 0.55f),
            alpha: new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)),
            sizeCurve: AnimationCurve.Constant(0f, 1f, 1f));
        var mso = new SerializedObject(motes);
        mso.FindProperty("_duration").floatValue = 0f; // Stop(패널 닫힘) 전까지 계속
        mso.FindProperty("_rate").floatValue = 12f;
        mso.FindProperty("_prewarmCount").intValue = 23; // 처음부터 퍼져 있게 (전체 알파는 TierRevealFx가 서서히 올림)
        mso.FindProperty("_swayAmplitude").floatValue = 14f;
        mso.FindProperty("_swayFrequency").vector2Value = new Vector2(0.15f, 0.4f);
        mso.FindProperty("_twinkleSpeed").vector2Value = new Vector2(1.5f, 4f);
        mso.FindProperty("_topFade").floatValue = 0.5f;
        var colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        var alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 1f) };
        var moteGradient = new Gradient();
        moteGradient.SetKeys(colorKeys, alphaKeys);
        mso.FindProperty("_startColor").gradientValue = moteGradient; // 알갱이마다 밝기 차이
        mso.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(fx);
        so.FindProperty("_backdrop").objectReferenceValue = backdrop;
        so.FindProperty("_columns").objectReferenceValue = columns;
        so.FindProperty("_backGlow").objectReferenceValue = backGlow;
        so.FindProperty("_rays").objectReferenceValue = rays;
        so.FindProperty("_bubble").objectReferenceValue = bubble;
        so.FindProperty("_gather").objectReferenceValue = null;
        so.FindProperty("_core").objectReferenceValue = core;
        so.FindProperty("_motes").objectReferenceValue = motes;
        so.FindProperty("_upgradeBurstScale").floatValue = 1.15f;
        // 글로우는 잠깐 보여 주고 완전히 끈다 (사용자 결정 2026-09-30 — 먼저 완전히 꺼 보고 이후 조정)
        so.FindProperty("_glowLinger").floatValue = 1f;
        so.FindProperty("_glowFadeOut").floatValue = 0.7f;
        so.FindProperty("_glowEndRatio").floatValue = 0f;
        so.FindProperty("_motesDelay").floatValue = 0.6f;
        so.FindProperty("_motesFadeIn").floatValue = 1f;
        so.FindProperty("_motesWhiten").floatValue = 0.2f;
        var bokehProp = so.FindProperty("_bokeh");
        bokehProp.arraySize = bokeh.Length;
        for (int i = 0; i < bokeh.Length; i++) bokehProp.GetArrayElementAtIndex(i).objectReferenceValue = bokeh[i];

        SetStyle(so.FindProperty("_rare"), main: new Color(0.3f, 0.62f, 1f), accent: new Color(0.75f, 0.9f, 1f),
            backdrop: new Color(0.02f, 0.05f, 0.12f), glowPeak: 0.6f, glowRest: 0.3f, rayPeak: 0.6f, rayRest: 0.3f,
            flash: 0.8f, bubble: false, bokehCount: 2, gatherRate: 60f, columns: false, cardsDelay: 0.05f);
        SetStyle(so.FindProperty("_epic"), main: new Color(0.62f, 0.3f, 1f), accent: new Color(0.88f, 0.7f, 1f),
            backdrop: new Color(0.07f, 0.03f, 0.13f), glowPeak: 0.8f, glowRest: 0.4f, rayPeak: 0.9f, rayRest: 0.45f,
            flash: 1f, bubble: true, bokehCount: 3, gatherRate: 90f, columns: false, cardsDelay: 0.1f);
        // 등급색 (사용자 지정 2026-09-29): 레어 파랑 / 에픽 보라 / 레전더리 빨강
        SetStyle(so.FindProperty("_legend"), main: new Color(1f, 0.24f, 0.26f), accent: new Color(1f, 0.78f, 0.7f),
            backdrop: new Color(0.14f, 0.02f, 0.04f), glowPeak: 1f, glowRest: 0.5f, rayPeak: 1f, rayRest: 0.55f,
            flash: 1.3f, bubble: true, bokehCount: 4, gatherRate: 120f, columns: true, cardsDelay: 0.1f);
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var g in new Graphic[] { backdrop, columns, backGlow, rays, bubble, core }) g.gameObject.SetActive(false);
        foreach (var b in bokeh) b.gameObject.SetActive(false);
        SavePrefab(root, TierRevealPath);
    }

    // ── ③ 카드 출력 이펙트 (CardRevealFx) — 등급색 테두리 번쩍임 + 빛 쓸기 + (레전더리) 가로 플레어 ──────
    //   크기는 재생 시 카드 크기로 정해진다. 패널 안에 생성되므로 별도 Canvas 없음.

    private static void BuildCardReveal()
    {
        // 카드 테두리 스프라이트 실루엣을 따라 빛남 (사각형 근사 FxRectGlow는 카드 그림 여백과 어긋나 교체)
        var glowMat = LoadOrCreate("LUC_CardGlow", "USW/UI/FxSpriteGlow");
        glowMat.SetFloat("_GlowIntensity", 1.1f);
        glowMat.SetFloat("_GlowGain", 2.5f);
        glowMat.SetFloat("_BorderIntensity", 1.4f);
        glowMat.SetFloat("_InnerFill", 0.08f);
        glowMat.SetFloat("_SweepIntensity", 0f);
        EditorUtility.SetDirty(glowMat);
        var sweepMat = LoadOrCreate("LUC_CardSweep", "USW/UI/FxSpriteGlow");
        sweepMat.SetFloat("_GlowIntensity", 0f);
        sweepMat.SetFloat("_BorderIntensity", 0f);
        sweepMat.SetFloat("_InnerFill", 0f);
        sweepMat.SetFloat("_SweepIntensity", 1.3f);
        sweepMat.SetFloat("_SweepWidth", 0.07f);
        sweepMat.SetFloat("_SweepSlant", 0.25f);
        sweepMat.SetFloat("_SweepWhite", 0.85f);
        sweepMat.SetFloat("_SweepPos", -1f);
        EditorUtility.SetDirty(sweepMat);
        var flareMat = Glow("LUC_Flare", core: 1.2f, hardness: 1f, falloff: 1.5f, whiteCore: 1f);
        flareMat.SetFloat("_WhiteCorePower", 4f);

        // ③ A안 (공개용 기본): 카드 이미지 영역 사각형으로 빛남 — 카드 폭을 따라 일직선으로 길게 뻗는 느낌.
        //   사용자가 실루엣 방식보다 이 버전을 선택 (2026-09-30). 당시 값 그대로.
        var rectGlowMat = LoadOrCreate("LUC_CardGlowRect", "USW/UI/FxRectGlow");
        rectGlowMat.SetFloat("_Radius", 24f);
        rectGlowMat.SetFloat("_BorderWidth", 10f);
        rectGlowMat.SetFloat("_BorderIntensity", 1.6f);
        // 등장 시 그라데이션(바깥 번짐·안쪽 채움)을 줄임 — 사용자 요청 2026-09-30 (이전 70 / 0.9 / 0.12)
        rectGlowMat.SetFloat("_GlowWidth", 35f);
        rectGlowMat.SetFloat("_GlowIntensity", 0.25f);
        rectGlowMat.SetFloat("_InnerFill", 0f);
        rectGlowMat.SetFloat("_SweepIntensity", 0f);
        EditorUtility.SetDirty(rectGlowMat);
        var rectSweepMat = LoadOrCreate("LUC_CardSweepRect", "USW/UI/FxRectGlow");
        rectSweepMat.SetFloat("_Radius", 24f);
        rectSweepMat.SetFloat("_BorderIntensity", 0f);
        rectSweepMat.SetFloat("_GlowIntensity", 0f);
        rectSweepMat.SetFloat("_InnerFill", 0f);
        rectSweepMat.SetFloat("_SweepIntensity", 1.4f);
        rectSweepMat.SetFloat("_SweepWidth", 0.07f);
        rectSweepMat.SetFloat("_SweepSlant", 0.6f);
        rectSweepMat.SetFloat("_SweepWhite", 0.85f);
        rectSweepMat.SetFloat("_SweepPos", -1f);
        EditorUtility.SetDirty(rectSweepMat);
        BuildCardFx("FxLevelUp_CardReveal", CardRevealPath, rectGlowMat, rectSweepMat, flareMat, flashMat: null, pulseMat: null,
                    intensityScale: 1f, configure: so =>
                    {
                        so.FindProperty("_rectShape").boolValue = true;
                        so.FindProperty("_padding").floatValue = 140f;
                        so.FindProperty("_flareWidth").floatValue = 1800f;
                        so.FindProperty("_flareHeight").floatValue = 90f;
                    });

        // ③ B안: 카드 테두리 스프라이트 실루엣을 따라 빛남 (카드 모양 버전)
        BuildCardFx("FxLevelUp_CardReveal_Shape", CardRevealShapePath, glowMat, sweepMat, flareMat, flashMat: null, pulseMat: null,
                    intensityScale: 1f, configure: null);

        // ④ 선택용: + 카드 전체 흰 섬광 + 카드 모양 파동, 더 밝고 빠르게 (레퍼런스 3의 선택 강조)
        var flashMat = LoadOrCreate("LUC_CardFlash", "USW/UI/FxSpriteGlow");
        flashMat.SetFloat("_GlowIntensity", 0f);
        flashMat.SetFloat("_BorderIntensity", 0f);
        flashMat.SetFloat("_InnerFill", 1f);
        flashMat.SetFloat("_SweepIntensity", 0f);
        EditorUtility.SetDirty(flashMat);
        var pulseMat = LoadOrCreate("LUC_CardPulse", "USW/UI/FxSpriteGlow");
        pulseMat.SetFloat("_GlowIntensity", 1.2f);
        pulseMat.SetFloat("_GlowGain", 2f);
        pulseMat.SetFloat("_BorderIntensity", 1.6f);
        pulseMat.SetFloat("_InnerFill", 0f);
        pulseMat.SetFloat("_SweepIntensity", 0f);
        EditorUtility.SetDirty(pulseMat);
        // 중심 빛: 카드 가운데서 터져 속을 채우며 번짐 (사용자 피드백 2026-09-30: 테두리만 빛나 속이 비어 보임)
        // 밝은 크림색 카드 위에서는 더하기(additive) 빛이 안 보여서 등급색을 덧칠하는 알파 블렌딩 (premultiplied)
        var bloomMat = Glow("LUC_CardBloom", core: 1f, hardness: 1f, falloff: 1.4f, whiteCore: 0f);
        bloomMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        BuildCardFx("FxLevelUp_CardSelect", CardSelectPath, glowMat, sweepMat, flareMat, flashMat, pulseMat, bloomMat: bloomMat,
                    intensityScale: 1.3f, configure: so =>
                    {
                        so.FindProperty("_glowRise").floatValue = 0.08f;
                        so.FindProperty("_glowHold").floatValue = 0.05f;
                        so.FindProperty("_glowFall").floatValue = 0.35f;
                        so.FindProperty("_sweepDelay").floatValue = 0.02f;
                        so.FindProperty("_sweepDuration").floatValue = 0.22f;
                        so.FindProperty("_pulseScale").floatValue = 1.15f;
                        so.FindProperty("_pulseDuration").floatValue = 0.42f;
                        // 안→밖 확산 (사용자 요청 2026-09-30: 선택 시 너무 바깥에서부터 보임) — 카드 85% 크기에서 시작
                        so.FindProperty("_growFrom").floatValue = 0.85f;
                        so.FindProperty("_growDuration").floatValue = 0.16f;
                        so.FindProperty("_bloomSize").vector2Value = new Vector2(1.3f, 1.6f);
                        so.FindProperty("_bloomFrom").floatValue = 0.05f;
                        so.FindProperty("_bloomPeak").floatValue = 0.55f;
                        so.FindProperty("_bloomWhiten").floatValue = 0.1f;
                        so.FindProperty("_radiusFrom").floatValue = 0.15f; // 바깥 번짐은 테두리에 붙어 시작해 넓어짐
                        so.FindProperty("_bloomGrow").floatValue = 0.2f;
                        so.FindProperty("_bloomFall").floatValue = 0.4f;
                        // 중심 빛이 먼저 차오른 뒤 가장자리 (첫 프레임에 흰 섬광·테두리가 카드 전체를 덮어 중심 빛이 가려지던 문제)
                        so.FindProperty("_edgeDelay").floatValue = 0.08f;
                        so.FindProperty("_flashPeak").floatValue = 0.4f;
                        so.FindProperty("_flashDuration").floatValue = 0.18f;
                    });
    }

    private static void BuildCardFx(string name, string path, Material glowMat, Material sweepMat, Material flareMat,
        Material flashMat, Material pulseMat, float intensityScale, System.Action<SerializedObject> configure, Material bloomMat = null)
    {
        var root = NewRoot(name);
        Object.DestroyImmediate(root.GetComponent<UiFxTimeline>());
        Object.DestroyImmediate(root.GetComponent<Canvas>());
        var fx = root.AddComponent<CardRevealFx>();
        var rt = (RectTransform)root.transform;
        var flare = NewImage("Flare", rt, flareMat, 100f, Color.white);
        var pulse = pulseMat != null ? NewImage("Pulse", rt, pulseMat, 100f, Color.white) : null;
        var bloom = bloomMat != null ? NewImage("Bloom", rt, bloomMat, 100f, Color.white) : null;
        var glow = NewImage("Glow", rt, glowMat, 100f, Color.white);
        var flash = flashMat != null ? NewImage("Flash", rt, flashMat, 100f, Color.white) : null;
        var sweep = NewImage("Sweep", rt, sweepMat, 100f, Color.white);

        var so = new SerializedObject(fx);
        so.FindProperty("_glow").objectReferenceValue = glow;
        so.FindProperty("_sweep").objectReferenceValue = sweep;
        so.FindProperty("_flare").objectReferenceValue = flare;
        so.FindProperty("_flash").objectReferenceValue = flash;
        so.FindProperty("_pulse").objectReferenceValue = pulse;
        so.FindProperty("_bloom").objectReferenceValue = bloom;
        SetLook(so.FindProperty("_rare"), RareBlue, 0.8f * intensityScale, flare: false);
        SetLook(so.FindProperty("_epic"), EpicPurple, 1f * intensityScale, flare: false);
        SetLook(so.FindProperty("_legend"), LegendRed, 1.3f * intensityScale, flare: true);
        configure?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var g in new Graphic[] { flare, pulse, bloom, glow, flash, sweep }) if (g != null) g.gameObject.SetActive(false);
        SavePrefab(root, path);
    }

    private static void SetLook(SerializedProperty p, Color color, float intensity, bool flare)
    {
        p.FindPropertyRelative("Color").colorValue = color;
        p.FindPropertyRelative("Intensity").floatValue = intensity;
        p.FindPropertyRelative("Flare").boolValue = flare;
    }

    private static void SetStyle(SerializedProperty p, Color main, Color accent, Color backdrop, float glowPeak, float glowRest,
        float rayPeak, float rayRest, float flash, bool bubble, int bokehCount, float gatherRate, bool columns, float cardsDelay)
    {
        p.FindPropertyRelative("Main").colorValue = main;
        p.FindPropertyRelative("Accent").colorValue = accent;
        p.FindPropertyRelative("Backdrop").colorValue = backdrop;
        p.FindPropertyRelative("BackdropAlpha").floatValue = 1f; // 기존 주황 배경은 쓰지 않음 (사용자 결정)
        p.FindPropertyRelative("GlowPeak").floatValue = glowPeak;
        p.FindPropertyRelative("GlowRest").floatValue = glowRest;
        p.FindPropertyRelative("RayPeak").floatValue = rayPeak;
        p.FindPropertyRelative("RayRest").floatValue = rayRest;
        p.FindPropertyRelative("FlashScale").floatValue = flash;
        p.FindPropertyRelative("Bubble").boolValue = bubble;
        p.FindPropertyRelative("Bokeh").intValue = bokehCount;
        p.FindPropertyRelative("GatherRate").floatValue = gatherRate;
        p.FindPropertyRelative("Columns").boolValue = columns;
        p.FindPropertyRelative("UpgradeFromEpic").boolValue = false;
        // 보라 → 빨강 반전은 레전더리(빛기둥 등급)에서 10% 확률로만 (사용자 결정 2026-09-29)
        p.FindPropertyRelative("UpgradeChance").floatValue = columns ? 0.1f : 0f;
        p.FindPropertyRelative("CardsDelayAfterBurst").floatValue = cardsDelay;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 레퍼런스 1의 부드러운 광선 (넓고 흐린 쐐기, 천천히 회전·깜빡임)
    private static Material SoftRays(string name)
    {
        var m = LoadOrCreate(name, "USW/UI/FxGodRays");
        m.SetColor("_Color", Color.white);
        m.SetFloat("_Intensity", 1f);
        m.SetFloat("_CountA", 8f);
        m.SetVector("_WidthA", new Vector4(0.15f, 0.4f));
        m.SetFloat("_DensityA", 0.8f);
        m.SetFloat("_SpeedA", 0.03f);
        m.SetFloat("_SeedA", 7f);
        m.SetFloat("_CountB", 13f);
        m.SetVector("_WidthB", new Vector4(0.05f, 0.18f));
        m.SetFloat("_DensityB", 0.6f);
        m.SetFloat("_SpeedB", -0.02f);
        m.SetFloat("_SeedB", 13f);
        m.SetFloat("_LayerBWeight", 0.3f);
        m.SetFloat("_BrightnessMin", 0.35f);
        m.SetFloat("_Softness", 1f);
        m.SetFloat("_Pulse", 0.2f);
        m.SetFloat("_PulseSpeed", 2f);
        m.SetFloat("_InnerRadius", 0.05f);
        m.SetFloat("_RadialFalloff", 1.6f);
        m.SetFloat("_CenterGlow", 0.4f);
        m.SetFloat("_LowerDim", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ── 머티리얼 ────────────────────────────────────────────────

    private static Material Shape(string name, float shape, System.Action<Material> configure)
    {
        var m = LoadOrCreate(name, "USW/UI/FxShape");
        m.SetColor("_Color", Color.white);
        m.SetFloat("_Shape", shape);
        m.SetColor("_OutlineColor", Outline);
        m.SetColor("_CoreColor", Color.white);
        configure(m);
        EditorUtility.SetDirty(m);
        return m;
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

    // 만화풍 집중선: 가장자리가 딱딱한 가는 쐐기, 안쪽은 비우고 바깥으로 갈수록 옅어짐 (회전·깜빡임 없음)
    private static Material SpeedLines(string name)
    {
        var m = LoadOrCreate(name, "USW/UI/FxGodRays");
        m.SetColor("_Color", Color.white);
        m.SetFloat("_Intensity", 1.3f);
        m.SetFloat("_CountA", 22f);
        m.SetVector("_WidthA", new Vector4(0.03f, 0.09f));
        m.SetFloat("_DensityA", 0.8f);
        m.SetFloat("_SpeedA", 0f);
        m.SetFloat("_SeedA", 5f);
        m.SetFloat("_LayerBWeight", 0f);
        m.SetFloat("_BrightnessMin", 0.6f);
        m.SetFloat("_Softness", 0.15f);
        m.SetFloat("_Pulse", 0f);
        m.SetFloat("_InnerRadius", 0.3f);
        m.SetFloat("_RadialFalloff", 0.6f);
        m.SetFloat("_CenterGlow", 0f);
        m.SetFloat("_LowerDim", 0f);
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

    // ── 오브젝트 구성 ────────────────────────────────────────────

    private static GameObject NewRoot(string name)
    {
        // 파티클이 매 프레임 메시를 다시 만들므로 자체 Canvas로 리빌드를 분리한다 (정렬은 부모를 따름).
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UiFxTimeline));
        SceneManager.MoveGameObjectToScene(go, _stage);
        ((RectTransform)go.transform).sizeDelta = new Vector2(100f, 100f);
        return go;
    }

    private static Image NewImage(string name, RectTransform parent, Material mat, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.material = mat;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static UiFxParticleEmitter NewEmitter(string name, RectTransform parent, Material mat, Vector2 area, Vector2 offset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UiFxParticleEmitter));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = area;
        rt.anchoredPosition = offset;
        var emitter = go.GetComponent<UiFxParticleEmitter>();
        emitter.material = mat;
        emitter.raycastTarget = false;
        return emitter;
    }

    private static void ConfigureEmitter(UiFxParticleEmitter emitter, int burst, int max, Vector2 lifetime, Vector2 size,
        Vector2 speed, float direction, float spread, float drag, Vector2 angular, float twinkle, Color colorA, Color colorB,
        AnimationCurve alpha, AnimationCurve sizeCurve, float stretch = 0f)
    {
        var so = new SerializedObject(emitter);
        so.FindProperty("_playOnEnable").boolValue = false;
        so.FindProperty("_duration").floatValue = 0.01f;
        so.FindProperty("_rate").floatValue = 0f;
        so.FindProperty("_burst").intValue = burst;
        so.FindProperty("_prewarmCount").intValue = 0;
        so.FindProperty("_maxParticles").intValue = max;
        so.FindProperty("_lifetime").vector2Value = lifetime;
        so.FindProperty("_size").vector2Value = size;
        so.FindProperty("_speed").vector2Value = speed;
        so.FindProperty("_direction").floatValue = direction;
        so.FindProperty("_spread").floatValue = spread;
        so.FindProperty("_drag").floatValue = drag;
        so.FindProperty("_angularVelocity").vector2Value = angular;
        so.FindProperty("_twinkle").floatValue = twinkle;
        so.FindProperty("_velocityStretch").floatValue = stretch;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(colorA, 0f), new GradientColorKey(colorB, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        so.FindProperty("_startColor").gradientValue = g;
        so.FindProperty("_alphaOverLife").animationCurveValue = alpha;
        so.FindProperty("_sizeOverLife").animationCurveValue = sizeCurve;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetTrack(SerializedProperty track, Graphic g, float start, float rise, float peak, float hold, float fall,
        Ease fallEase, float scaleFrom, float scaleTo, float scaleDuration, Ease scaleEase, float rotate = 0f,
        string floatName = "", float floatFrom = 0f, float floatTo = 0f, Vector2 drift = default)
    {
        track.FindPropertyRelative("Graphic").objectReferenceValue = g;
        var p = track.FindPropertyRelative("Pulse");
        p.FindPropertyRelative("Start").floatValue = start;
        p.FindPropertyRelative("Rise").floatValue = rise;
        p.FindPropertyRelative("Peak").floatValue = peak;
        p.FindPropertyRelative("Hold").floatValue = hold;
        p.FindPropertyRelative("Fall").floatValue = fall;
        p.FindPropertyRelative("Rest").floatValue = 0f;
        p.FindPropertyRelative("FallEase").intValue = (int)fallEase;
        p.FindPropertyRelative("ScaleFrom").floatValue = scaleFrom;
        p.FindPropertyRelative("ScaleTo").floatValue = scaleTo;
        p.FindPropertyRelative("ScaleDuration").floatValue = scaleDuration;
        p.FindPropertyRelative("ScaleEase").intValue = (int)scaleEase;
        p.FindPropertyRelative("Drift").vector2Value = drift;
        track.FindPropertyRelative("Rotate").floatValue = rotate;
        track.FindPropertyRelative("FloatName").stringValue = floatName;
        track.FindPropertyRelative("FloatFrom").floatValue = floatFrom;
        track.FindPropertyRelative("FloatTo").floatValue = floatTo;
        track.FindPropertyRelative("FloatEase").intValue = (int)Ease.OutQuad;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = uiLayer;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }
}
