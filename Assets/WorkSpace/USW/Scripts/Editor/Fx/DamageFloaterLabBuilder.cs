using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 데미지 플로터 연구실 씬(FxLab_DamageFloater)과 제안 설정 에셋을 만든다 (사용자 요청 2026-09-30, 레퍼런스 design/데미지플로터예시.gif).
///   위 = 현재 인게임 설정(BossDamageNumberSettings), 아래 = 제안 설정(DamageFloaterLabProposal) — 같은 타격을 동시에 띄워 비교.
///   제안 에셋은 없을 때만 만든다 (다시 실행해도 인스펙터에서 튜닝한 값을 덮어쓰지 않음). 씬은 매번 덮어쓴다.
/// </summary>
public static class DamageFloaterLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_DamageFloater.unity";
    private const string CurrentSettingsPath = "Assets/WorkSpace/USW/Data/UI/BossDamageNumberSettings.asset";
    private const string ProposalPath = "Assets/WorkSpace/USW/Data/UI/DamageStyleLab/DamageFloaterLabProposal.asset";
    private const string BossSpritePath = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly Vector2 BossSlotSize = new Vector2(560f, 560f);
    private static readonly Vector2 TopBossPos = new Vector2(-230f, 110f);
    private static readonly Vector2 BottomBossPos = new Vector2(-230f, -520f);
    private const float CaptureDuration = 3f;

    [MenuItem("Tools/USW/Fx/Build Damage Floater Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[DamageFloaterLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(CurrentSettingsPath) == null)
        {
            Debug.LogError($"[DamageFloaterLab] 현재 설정 없음: {CurrentSettingsPath}");
            return;
        }
        EnsureProposal();
        BuildScene();
        Debug.Log($"[DamageFloaterLab] 완료: {ScenePath} (제안 설정 {ProposalPath})");
    }

    // 레퍼런스 GIF(10fps) 측정: 등장 첫 프레임에 크게·왼쪽 아래에서 찍힘 → 다음 프레임에 자리, 약 0.7초 정지, 0.15~0.2초 만에 사라짐 (총 약 0.85초),
    // 새 줄이 오면 기존 줄은 한 프레임 안에 위로 밀림, 동시에 보이는 줄은 3~4개, 치명타 흔들림 없음.
    // 글꼴·색·크기·위치·줄 간격은 현재 인게임 값을 그대로 둬서 시간·움직임 차이만 비교한다.
    private static void EnsureProposal()
    {
        if (AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(ProposalPath) != null) return;
        AssetDatabase.CopyAsset(CurrentSettingsPath, ProposalPath);
        var p = AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(ProposalPath);
        p.MobiLifetime = 0.85f;
        p.MobiFadeStart = 0.8f;
        p.MobiMaxLines = 4;
        p.MobiFollowSpeed = 40f;
        p.MobiCriticalShake = 0f;
        p.MobiEntry = DamageStyleLabSettings.MobiEntryStyle.Slam;
        p.MobiSlamSeconds = 0.1f;
        p.MobiSlamStartScale = 1.7f;
        p.MobiSlamStretch = new Vector2(1.25f, 0.8f);
        p.MobiSlamFrom = new Vector2(-60f, -40f);
        p.MobiSlamUndershoot = 0.06f;
        p.MobiFadeRise = 12f;
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬(Single)을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 fileID 0으로 풀림 — PenaltyFxLabBuilder 참고)
        var current = AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(CurrentSettingsPath);
        var proposal = AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(ProposalPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        var bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossSpritePath);
        if (bossSprite == null)
        {
            Debug.LogWarning($"[DamageFloaterLab] 보스 스프라이트 없음({BossSpritePath}) — 기본 사각형으로 대체");
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
        var topBoss = NewBoss("TopBoss_Current", bossSprite);
        var bottomBoss = NewBoss("BottomBoss_Proposal", bossSprite);

        NewLabel(canvasRt, font, "현재 (인게임)", TopBossPos + new Vector2(-40f, BossSlotSize.y * 0.5f - 10f), new Color(0.62f, 0.82f, 1f));
        NewLabel(canvasRt, font, "제안 (레퍼런스 분석)", BottomBossPos + new Vector2(-40f, BossSlotSize.y * 0.5f - 10f), new Color(1f, 0.82f, 0.5f));

        var numbers = NewRect("DamageNumbers", canvasRt, Vector2.zero);
        Stretch(numbers);

        var status = NewText("Status", canvasRt, font, 26f, Color.white, Vector2.zero);
        status.alignment = TextAlignmentOptions.BottomLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.richText = true;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 0f);
        status.rectTransform.sizeDelta = new Vector2(-40f, 150f);
        status.rectTransform.anchoredPosition = new Vector2(0f, 16f);

        var lab = new GameObject("DamageFloaterLab", typeof(DamageFloaterLab)).GetComponent<DamageFloaterLab>();
        var so = new SerializedObject(lab);
        so.FindProperty("_currentSettings").objectReferenceValue = current;
        so.FindProperty("_proposalSettings").objectReferenceValue = proposal;
        so.FindProperty("_container").objectReferenceValue = numbers;
        so.FindProperty("_topBoss").objectReferenceValue = topBoss;
        so.FindProperty("_bottomBoss").objectReferenceValue = bottomBoss;
        so.FindProperty("_topBossSlot").objectReferenceValue = topSlot;
        so.FindProperty("_bottomBossSlot").objectReferenceValue = bottomSlot;
        so.FindProperty("_status").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();

        var rowA = NewButtonRow(canvasRt, "LabPatterns", -20f);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowA, "레퍼런스 박자", font).onClick, lab.PlayReference);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowA, "느린 타격", font).onClick, lab.PlaySlow);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowA, "실전 밀도", font).onClick, lab.PlayDense);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowA, "한 방", font).onClick, lab.PlaySingle);
        var rowB = NewButtonRow(canvasRt, "LabControls", -122f);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowB, "속도 (1 / 0.5 / 0.25)", font).onClick, lab.NextSpeed);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowB, "멈춤", font).onClick, lab.Stop);

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = lab;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = true;
        capSo.FindProperty("_duration").floatValue = CaptureDuration;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 960);
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ── 도우미 ────────────────────────────────────────────────

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

    private static void NewLabel(RectTransform parent, TMP_FontAsset font, string label, Vector2 pos, Color color)
    {
        var text = NewText("Label_" + label, parent, font, 36f, color, new Vector2(460f, 60f));
        text.alignment = TextAlignmentOptions.Left;
        text.rectTransform.anchoredPosition = pos;
        text.text = label;
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
        var text = NewText("Label", (RectTransform)go.transform, font, 32f, Color.white, Vector2.zero);
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
