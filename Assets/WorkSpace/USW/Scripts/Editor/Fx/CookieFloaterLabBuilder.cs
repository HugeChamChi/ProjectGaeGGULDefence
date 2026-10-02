using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 쿠키런 크럼블형 데미지 숫자 실험실 씬(FxLab_CookieFloater)과 후보 설정 에셋을 만든다 (사용자 요청 2026-10-01).
/// 팀 논의 후보: 중앙 영역은 옅게·바깥은 진하게 / 빠르게 위로 올라가며 사라짐 / 대각선 샘플(범위 침공 확인용).
/// 설정 에셋은 없을 때만 만든다 (다시 실행해도 인스펙터에서 튜닝한 값을 덮어쓰지 않음). 씬은 매번 덮어쓴다.
/// </summary>
public static class CookieFloaterLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_CookieFloater.unity";
    private const string SettingsPath = "Assets/WorkSpace/USW/Data/UI/DamageStyleLab/CookieFloaterLabSettings.asset";
    private const string LookSourcePath = "Assets/WorkSpace/USW/Data/UI/BossDamageNumberSettings.asset";
    private const string BossSpritePath = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    // 화면 배치 (1080x1920, 가운데 기준). 버튼 4줄 아래 남는 세로 공간(y 616 ~ -790)을 위·아래 반씩 나눠 쓴다.
    // 각 칸은 맨 위 라벨 → 숫자가 떠오를 여유 → 보스(칸 바닥에 붙임) 순서라, 위 칸 숫자가 아래 칸 라벨·보스와 겹치지 않는다.
    private const float RowHeight = 76f;
    private const float RowStep = 84f;
    private static readonly Vector2 BossSlotSize = new Vector2(480f, 480f);
    private static readonly Vector2 TopBossPos = new Vector2(0f, 173f);
    private static readonly Vector2 BottomBossPos = new Vector2(0f, -530f);
    private const float TopLabelY = 588f;
    private const float DividerY = -87f;
    private const float BottomLabelY = -115f;
    private static readonly string[] VariantButtons = { "A 기존", "B 중앙옅게", "C 빠르게 위", "D 대각선", "E B+C" };
    private const float CaptureDuration = 3f;
    // 숫자가 뜨는 범위 (보스 스프라이트 대비). 가로는 Settings.FullScreenWidth로 화면 폭 전체를 쓰고, 세로는 넉넉히 (사용자 요청 2026-10-01)
    private static readonly Vector2 ScatterArea = new Vector2(1f, 0.8f);

    [MenuItem("Tools/USW/Fx/Build Cookie Floater Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[CookieFloaterLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSettings();
        BuildScene();
        Debug.Log($"[CookieFloaterLab] 완료: {ScenePath} (설정 {SettingsPath})");
    }

    private static void EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<CookieFloaterLabSettings>(SettingsPath) != null) return;
        var s = ScriptableObject.CreateInstance<CookieFloaterLabSettings>();
        var look = AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(LookSourcePath);
        if (look != null)
        {
            if (look.Fonts != null && look.Fonts.Length > 0) { s.Font = look.Fonts[0].Font; s.FontMaterial = look.Fonts[0].Material; }
            s.NormalColor = look.NormalColor;
            s.CriticalColor = look.CriticalColor;
            s.BurnColor = look.BurnColor;
        }

        // A: 기존 쿠키런형 움직임 그대로 (BossDamageNumberSettings Cookie* 값), 범위만 넓힘
        var a = new CookieFloaterLabSettings.Variant { Name = "A 기존 흩뿌리기", Scatter = ScatterArea };
        // B: 가운데 영역에 일부를 일부러 옅게 배정, 바깥은 진하게
        var b = new CookieFloaterLabSettings.Variant
        {
            Name = "B 중앙 옅게·바깥 진하게",
            Scatter = ScatterArea,
            Alpha = 1f,
            UseCenterZone = true,
        };
        // C: 짧은 수명 동안 빠르게 위로 튀어 올라가며 사라짐
        var c = Fast("C 빠르게 위로 소멸", CookieFloaterLabSettings.Motion.Up);
        // D: C와 같은 박자로 대각선 (방향은 바깥쪽 고정 — 무작위 방향이면 범위를 침범)
        var d = Fast("D 빠르게 대각선", CookieFloaterLabSettings.Motion.Diagonal);
        // E: B의 영역 배정 + C의 움직임
        var e = Fast("E 중앙 옅게 + 빠르게 위로", CookieFloaterLabSettings.Motion.Up);
        e.Alpha = 1f;
        e.UseCenterZone = true;

        s.Variants = new[] { a, b, c, d, e };
        AssetDatabase.CreateAsset(s, SettingsPath);
        AssetDatabase.SaveAssets();
    }

    private static CookieFloaterLabSettings.Variant Fast(string name, CookieFloaterLabSettings.Motion motion) => new CookieFloaterLabSettings.Variant
    {
        Name = name,
        Motion = motion,
        DiagonalAngle = 35f,
        Lifetime = 0.35f,
        Rise = 150f,
        Scatter = ScatterArea,
        RiseEasePower = 3f,
        FadeStart = 0.25f,
        PopSeconds = 0.05f,
        PopOvershoot = 0.2f,
        Alpha = 0.95f,
        FlushInterval = 0.1f,
        MaxPopups = 18,
    };

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬(Single)을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 fileID 0으로 풀림 — PenaltyFxLabBuilder 참고)
        var settings = AssetDatabase.LoadAssetAtPath<CookieFloaterLabSettings>(SettingsPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        var bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossSpritePath);
        if (bossSprite == null)
        {
            Debug.LogWarning($"[CookieFloaterLab] 보스 스프라이트 없음({BossSpritePath}) — 기본 사각형으로 대체");
            bossSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 9.6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.13f, 0.19f, 1f); // 전투 필드 대역색
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
        scaler.matchWidthOrHeight = 0f; // 인게임 BossDamageNumbers와 같은 가로 기준
        var canvasRt = (RectTransform)canvasGo.transform;

        // 보스 대역: 월드 스프라이트 (인게임처럼 bounds로 화면 영역 계산) + 캔버스 위 자리
        var topSlot = NewRect("TopBossSlot", canvasRt, BossSlotSize);
        topSlot.anchoredPosition = TopBossPos;
        var bottomSlot = NewRect("BottomBossSlot", canvasRt, BossSlotSize);
        bottomSlot.anchoredPosition = BottomBossPos;
        var topBoss = NewBoss("TopBoss", bossSprite);
        var bottomBoss = NewBoss("BottomBoss", bossSprite);

        var topLabel = NewLabel(canvasRt, font, new Vector2(0f, TopLabelY), new Color(0.62f, 0.82f, 1f));
        var bottomLabel = NewLabel(canvasRt, font, new Vector2(0f, BottomLabelY), new Color(1f, 0.82f, 0.5f));
        var divider = NewRect("Divider", canvasRt, new Vector2(ReferenceResolution.x, 3f));
        divider.anchoredPosition = new Vector2(0f, DividerY);
        divider.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);

        var numbers = NewRect("DamageNumbers", canvasRt, Vector2.zero);
        Stretch(numbers);

        var status = NewText("Status", canvasRt, font, 22f, Color.white, Vector2.zero);
        status.alignment = TextAlignmentOptions.BottomLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.richText = true;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 0f);
        status.rectTransform.sizeDelta = new Vector2(-40f, 130f);
        status.rectTransform.anchoredPosition = new Vector2(0f, 16f);

        var lab = new GameObject("CookieFloaterLab", typeof(CookieFloaterLab)).GetComponent<CookieFloaterLab>();

        var rowA = NewButtonRow(canvasRt, "LabPatterns", -16f, null, font);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowA, "느린 타격", font).onClick, lab.PlaySlow);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowA, "실전 밀도", font).onClick, lab.PlayDense);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowA, "폭주", font).onClick, lab.PlaySwarm);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowA, "한 방", font).onClick, lab.PlaySingle);
        var rowB = NewButtonRow(canvasRt, "LabControls", -16f - RowStep, null, font);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowB, "속도 1/0.5/0.25", font).onClick, lab.NextSpeed);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowB, "멈춤", font).onClick, lab.Stop);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowB, "범위선", font).onClick, lab.ToggleGuides);

        var rowTop = NewButtonRow(canvasRt, "TopVariants", -16f - RowStep * 2f, "위", font);
        var rowBottom = NewButtonRow(canvasRt, "BottomVariants", -16f - RowStep * 3f, "아래", font);
        int count = Mathf.Min(VariantButtons.Length, settings.Variants.Length);
        var topImages = new Image[count];
        var bottomImages = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var top = NewButton(rowTop, VariantButtons[i], font);
            UnityEventTools.AddIntPersistentListener(top.onClick, lab.SelectTop, i);
            topImages[i] = top.GetComponent<Image>();
            var bottom = NewButton(rowBottom, VariantButtons[i], font);
            UnityEventTools.AddIntPersistentListener(bottom.onClick, lab.SelectBottom, i);
            bottomImages[i] = bottom.GetComponent<Image>();
        }

        var so = new SerializedObject(lab);
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_container").objectReferenceValue = numbers;
        so.FindProperty("_topBoss").objectReferenceValue = topBoss;
        so.FindProperty("_bottomBoss").objectReferenceValue = bottomBoss;
        so.FindProperty("_topBossSlot").objectReferenceValue = topSlot;
        so.FindProperty("_bottomBossSlot").objectReferenceValue = bottomSlot;
        so.FindProperty("_topLabel").objectReferenceValue = topLabel;
        so.FindProperty("_bottomLabel").objectReferenceValue = bottomLabel;
        so.FindProperty("_status").objectReferenceValue = status;
        SetArray(so.FindProperty("_topButtons"), topImages);
        SetArray(so.FindProperty("_bottomButtons"), bottomImages);
        so.FindProperty("_topVariant").intValue = 0;
        so.FindProperty("_bottomVariant").intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = lab;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = true;
        capSo.FindProperty("_duration").floatValue = CaptureDuration;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 960);
        capSo.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/CookieFloater";
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ── 도우미 ────────────────────────────────────────────────

    private static void SetArray(SerializedProperty prop, Object[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static SpriteRenderer NewBoss(string name, Sprite sprite)
    {
        var sr = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        return sr;
    }

    private static RectTransform NewRect(string name, RectTransform parent, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }

    private static TextMeshProUGUI NewLabel(RectTransform parent, TMP_FontAsset font, Vector2 pos, Color color)
    {
        var text = NewText("VariantLabel", parent, font, 34f, color, new Vector2(1000f, 56f));
        text.rectTransform.anchoredPosition = pos;
        return text;
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

    private static RectTransform NewButtonRow(RectTransform canvasRt, string name, float y, string title, TMP_FontAsset font)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        row.SetParent(canvasRt, false);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-40f, RowHeight);
        row.anchoredPosition = new Vector2(0f, y);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childForceExpandWidth = false;
        layout.childControlWidth = layout.childControlHeight = true;
        if (title != null)
        {
            var label = NewText("RowTitle", row, font, 28f, Color.white, Vector2.zero);
            label.text = title;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = 80f;
            le.flexibleWidth = 0f;
        }
        return row;
    }

    private static Button NewButton(RectTransform parent, string label, TMP_FontAsset font)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = 0f;
        le.flexibleWidth = 1f;
        var text = NewText("Label", (RectTransform)go.transform, font, 26f, Color.white, Vector2.zero);
        text.text = label;
        Stretch(text.rectTransform);
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
