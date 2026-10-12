using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 디시그망 스킬 → 타이머 글리치 연결 시안 실험실 씬(FxLab_DisigmanGlitchLink)을 만든다 (사용자 요청 2026-10-12).
/// 카메라·배경·보스·레일건 템플릿은 레일건 실험실(DisigmanRailgunFxLabBuilder, 인게임 기준값)과 같고,
/// HUD 타이머는 IngameScene Main_Topbar_TimerText와 같은 자리·글꼴·크기다. 타이머 연출은 TimerBonusLab 인게임 바인딩(_runtimeTimer)으로
/// 붙여 실제 인게임과 같은 E 해킹 + J 중첩을 그린다. 씬은 매번 덮어쓴다. 설정 에셋(TimerBonusLabSettings)은 읽기만 한다.
/// </summary>
public static class DisigmanGlitchLinkLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_DisigmanGlitchLink.unity";
    private const string SettingsPath = "Assets/WorkSpace/USW/Data/UI/TimerBonusLab/TimerBonusLabSettings.asset";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)
    private const string UnitSpritePath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/Disigmang_1.png";

    // Disigman 프리팹 외형 (스케일 1.7, 정렬 4)
    private const float UnitScale = 1.7f;
    private const int UnitOrder = 4;
    private static readonly Vector3[] UnitPositions =
    {
        new Vector3(-0.9f, -1.4f, 0f),
        new Vector3(0.95f, -0.7f, 0f),
        new Vector3(0.15f, -2.5f, 0f),
    };

    // IngameScene: MainUI_SafeAreaRoot(1080×1920 가운데) → MainUI_TopBar(위 100) → Main_Topbar_TimerPanel(위 가운데 -24, 380×105)
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly Vector2 SafeAreaSize = new Vector2(1080f, 1920f);
    private static readonly Vector2 TimerPanelPos = new Vector2(0f, -24f);
    private static readonly Vector2 TimerPanelSize = new Vector2(380f, 105f);
    private const float TimerFontSize = 105f;
    private static readonly Color TimerColor = new Color(1f, 0.965f, 0.9f, 1f);
    private const float CanvasPlaneDistance = 1f;

    private const float RowHeight = 76f;
    private const float RowStep = 84f;
    private const float RowBottom = 24f;
    private static readonly string[] VariantButtons = { "현재", "A 착탄 후", "B 시간 강탈", "D 보스 동시", "추천 조합" };

    [MenuItem("Tools/USW/Fx/Build Disigman Glitch Link Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[DisigmanGlitchLinkLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬(Single)을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 fileID 0으로 풀림 — PenaltyFxLabBuilder 참고)
        var settings = AssetDatabase.LoadAssetAtPath<TimerBonusLabSettings>(SettingsPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DisigmanRailgunFxLabBuilder.BgPath);
        var bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DisigmanRailgunFxLabBuilder.BossPath);
        var unitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UnitSpritePath);
        if (settings == null || font == null) { Debug.LogError("[DisigmanGlitchLinkLab] 타이머 설정/폰트 없음"); return; }

        // ── 월드: 레일건 실험실과 같은 카메라·배경·보스 ──
        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = false;
        cam.fieldOfView = DisigmanRailgunFxLabBuilder.CameraFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.SetPositionAndRotation(DisigmanRailgunFxLabBuilder.CameraPos,
            Quaternion.Euler(DisigmanRailgunFxLabBuilder.CameraPitch, 0f, 0f));

        if (bgSprite != null)
            DisigmanRailgunFxLabBuilder.NewSprite("Battlefield_BG", bgSprite, DisigmanRailgunFxLabBuilder.BgPos, DisigmanRailgunFxLabBuilder.BgScale, -100);
        var target = new GameObject("Target").transform;
        target.position = DisigmanRailgunFxLabBuilder.BossPos;
        SpriteRenderer boss = null;
        if (bossSprite != null)
        {
            var bossGo = DisigmanRailgunFxLabBuilder.NewSprite("Boss_Dummy", bossSprite, DisigmanRailgunFxLabBuilder.BossPos, DisigmanRailgunFxLabBuilder.BossScale, 0);
            bossGo.transform.SetParent(target, true);
            boss = bossGo.GetComponent<SpriteRenderer>();
        }
        else Debug.LogWarning($"[DisigmanGlitchLinkLab] 보스 스프라이트 없음: {DisigmanRailgunFxLabBuilder.BossPath}");

        var units = new SpriteRenderer[UnitPositions.Length];
        for (int i = 0; i < units.Length; i++)
            units[i] = DisigmanRailgunFxLabBuilder.NewSprite($"Disigman_{i}", unitSprite, UnitPositions[i], UnitScale, UnitOrder).GetComponent<SpriteRenderer>();
        if (unitSprite == null) Debug.LogWarning($"[DisigmanGlitchLinkLab] 디시그망 그림 없음: {UnitSpritePath}");

        var timer = new GameObject("TimerController", typeof(TimerController)).GetComponent<TimerController>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // ── HUD: 카메라 캔버스 (캡처가 카메라 렌더 텍스처로 찍으므로 오버레이 대신) ──
        var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = CanvasPlaneDistance;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        var canvasRt = (RectTransform)canvasGo.transform;

        var safe = TimerBonusFxLabBuilder.NewRect("SafeAreaRoot", canvasRt, SafeAreaSize);
        var panel = TimerBonusFxLabBuilder.NewRect("TimerPanel", safe, TimerPanelSize);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = TimerPanelPos;
        var timerText = TimerBonusFxLabBuilder.NewText("TimerText", panel, font, TimerFontSize, TimerColor, Vector2.zero);
        TimerBonusFxLabBuilder.Stretch(timerText.rectTransform);
        timerText.text = "90.00";

        // 연출 영역은 HUD보다 뒤 형제 — 해킹 숫자 사본이 타이머 위에 그려진다.
        var stage = TimerBonusFxLabBuilder.NewRect("HackStage", canvasRt, Vector2.zero);
        TimerBonusFxLabBuilder.Stretch(stage);
        var shardLayer = TimerBonusFxLabBuilder.NewRect("ShardLayer", canvasRt, Vector2.zero);
        TimerBonusFxLabBuilder.Stretch(shardLayer);
        var overlay = TimerBonusFxLabBuilder.NewRect("Overlay", canvasRt, Vector2.zero);
        TimerBonusFxLabBuilder.Stretch(overlay);

        var hack = new GameObject("TimerBonusLab (runtime binding)", typeof(TimerBonusLab)).GetComponent<TimerBonusLab>();
        var hackSo = new SerializedObject(hack);
        hackSo.FindProperty("_settings").objectReferenceValue = settings;
        hackSo.FindProperty("_font").objectReferenceValue = font;
        hackSo.FindProperty("_stage").objectReferenceValue = stage;
        hackSo.FindProperty("_overlay").objectReferenceValue = overlay;
        hackSo.FindProperty("_runtimeTimer").objectReferenceValue = timerText;
        hackSo.ApplyModifiedPropertiesWithoutUndo();

        var lab = new GameObject("DisigmanGlitchLinkLab").AddComponent<DisigmanGlitchLinkLab>();
        var skill = DisigmanRailgunFxLabBuilder.Template(lab.transform, "Fx_Skill", DisigmanRailgunFxLabBuilder.SkillTint, true);

        // ── 버튼 (아래) + 상태 줄 ──
        var rowControls = BottomRow(canvasRt, "Controls", RowBottom);
        var rowVariants = BottomRow(canvasRt, "Variants", RowBottom + RowStep);
        var variantImages = new Image[VariantButtons.Length];
        for (int i = 0; i < VariantButtons.Length; i++)
        {
            var b = TimerBonusFxLabBuilder.NewButton(rowVariants, VariantButtons[i], font);
            UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectVariant, i);
            variantImages[i] = b.GetComponent<Image>();
        }
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "스킬 발사", font).onClick, lab.FireOne);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "3마리 연사", font).onClick, lab.FireVolley);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "자동 켬/끔", font).onClick, lab.ToggleAuto);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "속도 1/0.5/0.25", font).onClick, lab.NextSpeed);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "처음부터", font).onClick, lab.Play);

        var status = TimerBonusFxLabBuilder.NewText("Status", canvasRt, font, 26f, Color.white, Vector2.zero);
        status.alignment = TextAlignmentOptions.BottomLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 0f);
        status.rectTransform.sizeDelta = new Vector2(-40f, 100f);
        status.rectTransform.anchoredPosition = new Vector2(0f, RowBottom + RowStep * 2f);

        var so = new SerializedObject(lab);
        var unitsProp = so.FindProperty("_units");
        unitsProp.arraySize = units.Length;
        for (int i = 0; i < units.Length; i++) unitsProp.GetArrayElementAtIndex(i).objectReferenceValue = units[i];
        so.FindProperty("_target").objectReferenceValue = target;
        so.FindProperty("_boss").objectReferenceValue = boss;
        so.FindProperty("_skillFx").objectReferenceValue = skill;
        so.FindProperty("_timer").objectReferenceValue = timer;
        so.FindProperty("_hack").objectReferenceValue = hack;
        so.FindProperty("_timerText").objectReferenceValue = timerText;
        so.FindProperty("_stage").objectReferenceValue = stage;
        so.FindProperty("_shardLayer").objectReferenceValue = shardLayer;
        so.FindProperty("_status").objectReferenceValue = status;
        var buttons = so.FindProperty("_variantButtons");
        buttons.arraySize = variantImages.Length;
        for (int i = 0; i < variantImages.Length; i++) buttons.GetArrayElementAtIndex(i).objectReferenceValue = variantImages[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        WireAudio(lab, canvasRt, font);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[DisigmanGlitchLinkLab] 씬 생성: {ScenePath}");
    }

    /// <summary>기존 실험실에 공용 팔레트 오디오와 일반공격·음소거 조작을 연결한다.</summary>
    public static void WireAudio(DisigmanGlitchLinkLab lab, RectTransform canvas, TMP_FontAsset font)
    {
        var audio = lab.GetComponent<DisigmanGlitchLinkAudio>();
        if (audio == null) audio = lab.gameObject.AddComponent<DisigmanGlitchLinkAudio>();
        var so = new SerializedObject(lab);
        so.FindProperty("_audio").objectReferenceValue = audio;
        if (so.FindProperty("_basicFx").objectReferenceValue == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/USW/Prefab/Effect/DisigmanRailgun/RailgunProjectile_Basic.prefab");
            var basic = Object.Instantiate(prefab.GetComponentInChildren<RailgunBeamFx>(true), lab.transform);
            basic.name = "Fx_Basic_AudioPreview";
            basic.gameObject.SetActive(false);
            so.FindProperty("_basicFx").objectReferenceValue = basic;
        }
        var status = (TMP_Text)so.FindProperty("_status").objectReferenceValue;
        if (status != null) status.rectTransform.anchoredPosition = new Vector2(0f, RowBottom + RowStep * 3f);
        so.ApplyModifiedPropertiesWithoutUndo();
        var row = canvas.Find("AudioControls") as RectTransform;
        if (row == null)
        {
            row = BottomRow(canvas, "AudioControls", RowBottom + RowStep * 2f);
            UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(row, "일반공격", font).onClick, lab.FireBasic);
            var toggle = TimerBonusFxLabBuilder.NewButton(row, "소리 켜짐", font);
            UnityEventTools.AddVoidPersistentListener(toggle.onClick, audio.ToggleSound);
            var audioSo = new SerializedObject(audio);
            audioSo.FindProperty("_toggleLabel").objectReferenceValue = toggle.GetComponentInChildren<TMP_Text>();
            audioSo.ApplyModifiedPropertiesWithoutUndo();
        }
        var settings = new SerializedObject(audio);
        settings.FindProperty("_palette").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameAudioPalette>("Assets/WorkSpace/USW/Data/Audio/StudioDartPalette.asset");
        settings.FindProperty("_mixer").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/WorkSpace/HSD/Resources_moved/Master.mixer");
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>플레이 중 실험실의 시안 5개를 차례로 캡처한다 (Temp/FxCapture/DisigmanGlitchLink).</summary>
    [MenuItem("Tools/USW/Fx/Disigman Glitch Link - Capture All (Play)")]
    public static void CaptureAll()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[DisigmanGlitchLinkLab] 플레이 중에만 캡처한다"); return; }
        var lab = Object.FindFirstObjectByType<DisigmanGlitchLinkLab>();
        if (lab == null) { Debug.LogError("[DisigmanGlitchLinkLab] 씬에 실험실 없음"); return; }
        lab.CaptureAllVariants();
    }

    private static RectTransform BottomRow(RectTransform canvasRt, string name, float y)
    {
        var row = TimerBonusFxLabBuilder.NewButtonRow(canvasRt, name, 0f);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.sizeDelta = new Vector2(-40f, RowHeight);
        row.anchoredPosition = new Vector2(0f, y);
        return row;
    }
}
