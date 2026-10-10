using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 보스 처치 시간 보너스 연출 실험실 씬(FxLab_TimerBonus)과 설정 에셋을 만든다 (사용자 요청 2026-10-02).
/// 시안: A 합체 / B 슬롯 릴 / C 질주 / D 스탬프 / E 해킹 / F 해킹 상승(글리치, 2026-10-09)
/// + G~J 해킹 중첩 비교(재시작·대기열·합산·합산+미니, 세 번째 버튼 줄) + 공통 텐션(처치 전 박동, 히트스톱·플래시·줌).
/// 처치 전 공격은 실제 드론 유닛 프리팹 3종과 각자의 레이저 투사체 — 피격 이펙트는 실행 중 복제 데이터에서 빼고 쏜다.
/// 설정 에셋은 없을 때만 만든다 (다시 실행해도 인스펙터에서 튜닝한 값을 덮어쓰지 않음). 씬은 매번 덮어쓴다.
/// </summary>
public static class TimerBonusFxLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_TimerBonus.unity";
    private const string SettingsFolderParent = "Assets/WorkSpace/USW/Data/UI";
    private const string SettingsFolderName = "TimerBonusLab";
    private const string SettingsPath = SettingsFolderParent + "/" + SettingsFolderName + "/TimerBonusLabSettings.asset";
    private const string BossSpritePath = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)
    private const string DronePrefabFolder = "Assets/WorkSpace/USW/Prefab/Unit/DronUnit";
    // 순서는 TimerBonusLabSettings.DroneSlots와 같다. 자리·크기·간격은 설정 에셋에서 조정 (빌더가 덮어쓰지 않음).
    private static readonly string[] Drones = { "Drone_Normal_Prefab", "Drone_Buffer_Prefab", "Drone_Debuffer_Prefab" };

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const float RowHeight = 76f;
    internal const float RowStep = 84f;
    // 버튼 두 줄 아래부터 상태 줄 위까지를 연출 영역으로 쓴다 (가운데 기준 좌표).
    private static readonly Vector2 StageSize = new Vector2(1080f, 1500f);
    private static readonly Vector2 StagePosition = new Vector2(0f, -70f);
    private static readonly string[] ConceptButtons = { "A 합체", "B 슬롯 릴", "C 질주", "D 스탬프", "E 해킹", "F 해킹 상승" };
    // 해킹 중첩 비교 (여러 캐릭터 연달아 발동) — 시안 번호 6~9, 이 실험실 씬에만 세 번째 줄로 붙인다.
    private static readonly string[] StackButtons = { "G 재시작", "H 대기열", "I 합산", "J 합산+미니" };
    private const float CaptureDuration = 3.8f;

    [MenuItem("Tools/USW/Fx/Build Timer Bonus Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[TimerBonusLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSettings();
        BuildScene();
        Debug.Log($"[TimerBonusLab] 완료: {ScenePath} (설정 {SettingsPath})");
    }

    internal static void EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<TimerBonusLabSettings>(SettingsPath) != null) return;
        if (!AssetDatabase.IsValidFolder(SettingsFolderParent + "/" + SettingsFolderName))
            AssetDatabase.CreateFolder(SettingsFolderParent, SettingsFolderName);
        var s = ScriptableObject.CreateInstance<TimerBonusLabSettings>();
        AssetDatabase.CreateAsset(s, SettingsPath);
        AssetDatabase.SaveAssets();
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lab = CreateTimerLab(out _, out _, out var cam, withStackRow: true);
        AddCapture(cam, lab, CaptureDuration, "Temp/FxCapture/TimerBonus");
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    /// <summary>
    /// 지금 열린 빈 씬에 타이머 보너스 실험실(카메라·캔버스·무대·버튼 2줄·상태 줄·드론)을 만든다.
    /// 다른 실험실(BossClearNoticeLabBuilder)이 같은 무대를 재사용한다. 씬 저장은 호출한 쪽에서.
    /// </summary>
    internal static TimerBonusLab CreateTimerLab(out RectTransform canvasRt, out TMP_FontAsset font, out Camera cam, bool withStackRow = false)
    {
        // 새 씬(Single)을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 fileID 0으로 풀림 — PenaltyFxLabBuilder 참고)
        var settings = AssetDatabase.LoadAssetAtPath<TimerBonusLabSettings>(SettingsPath);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        var bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossSpritePath);
        if (bossSprite == null) Debug.LogWarning($"[TimerBonusLab] 보스 스프라이트 없음({BossSpritePath}) — 흰 사각형으로 대체");

        cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 9.6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.17f, 0.17f, 1f); // HTML 시안 무대색
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
        canvasRt = (RectTransform)canvasGo.transform;

        var stage = NewRect("Stage", canvasRt, StageSize);
        stage.anchoredPosition = StagePosition;
        // 플래시는 화면 전체, 버튼보다 먼저 만들어 버튼 아래에 깔린다.
        var overlay = NewRect("Overlay", canvasRt, Vector2.zero);
        Stretch(overlay);

        var status = NewText("Status", canvasRt, font, 26f, Color.white, Vector2.zero);
        status.alignment = TextAlignmentOptions.BottomLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 0f);
        status.rectTransform.sizeDelta = new Vector2(-40f, 100f);
        status.rectTransform.anchoredPosition = new Vector2(0f, 16f);

        var lab = new GameObject("TimerBonusLab", typeof(TimerBonusLab)).GetComponent<TimerBonusLab>();

        var rowConcepts = NewButtonRow(canvasRt, "Concepts", -16f);
        var conceptImages = new Image[ConceptButtons.Length + (withStackRow ? StackButtons.Length : 0)];
        for (int i = 0; i < ConceptButtons.Length; i++)
        {
            var b = NewButton(rowConcepts, ConceptButtons[i], font);
            UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectConcept, i);
            conceptImages[i] = b.GetComponent<Image>();
        }
        if (withStackRow)
        {
            var rowStack = NewButtonRow(canvasRt, "StackConcepts", -16f - RowStep * 2f);
            for (int i = 0; i < StackButtons.Length; i++)
            {
                int index = ConceptButtons.Length + i;
                var b = NewButton(rowStack, StackButtons[i], font);
                UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectConcept, index);
                conceptImages[index] = b.GetComponent<Image>();
            }
            UnityEventTools.AddVoidPersistentListener(NewButton(rowStack, "발동 타이밍", font).onClick, lab.NextTriggerSet);
        }
        var rowControls = NewButtonRow(canvasRt, "Controls", -16f - RowStep);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowControls, "다시 보기", font).onClick, lab.Replay);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowControls, "속도 1/0.5/0.25", font).onClick, lab.NextSpeed);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowControls, "보너스 +초", font).onClick, lab.NextBonus);
        UnityEventTools.AddVoidPersistentListener(NewButton(rowControls, "반복 켬/끔", font).onClick, lab.ToggleLoop);

        var so = new SerializedObject(lab);
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_font").objectReferenceValue = font;
        so.FindProperty("_bossSprite").objectReferenceValue = bossSprite;
        so.FindProperty("_stage").objectReferenceValue = stage;
        so.FindProperty("_overlay").objectReferenceValue = overlay;
        so.FindProperty("_status").objectReferenceValue = status;
        var buttons = so.FindProperty("_conceptButtons");
        buttons.arraySize = conceptImages.Length;
        for (int i = 0; i < conceptImages.Length; i++) buttons.GetArrayElementAtIndex(i).objectReferenceValue = conceptImages[i];
        AssignDrones(so.FindProperty("_droneSquad"));
        so.FindProperty("_concept").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        return lab;
    }

    /// <summary>FxLabCapture를 카메라에 붙인다 (target = IFxLabPlayable).</summary>
    internal static void AddCapture(Camera cam, MonoBehaviour target, float duration, string outputFolder)
    {
        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = target;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = true;
        capSo.FindProperty("_duration").floatValue = duration;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 960);
        capSo.FindProperty("_outputFolder").stringValue = outputFolder;
        capSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 드론 프리팹마다 DroneUnit에 연결된 레이저 투사체·공격 프레임과, 레이저의 ProjectileData를 읽어 넣는다.
    private static void AssignDrones(SerializedProperty squad)
    {
        squad.arraySize = Drones.Length;
        for (int i = 0; i < Drones.Length; i++)
        {
            string path = $"{DronePrefabFolder}/{Drones[i]}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var unit = prefab != null ? prefab.GetComponent<DroneUnit>() : null;
            Projectile laser = null;
            Sprite attack = null;
            ProjectileData data = null;
            if (unit != null)
            {
                var unitSo = new SerializedObject(unit);
                laser = unitSo.FindProperty("_projectilePrefab").objectReferenceValue as Projectile;
                attack = unitSo.FindProperty("_attackSprite").objectReferenceValue as Sprite;
                if (laser != null) data = new SerializedObject(laser).FindProperty("_data").objectReferenceValue as ProjectileData;
            }
            if (prefab == null || laser == null || data == null)
                Debug.LogWarning($"[TimerBonusLab] 드론/레이저 연결 누락: {path} (프리팹 {prefab != null}, 레이저 {laser != null}, 데이터 {data != null})");

            var el = squad.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("DronePrefab").objectReferenceValue = prefab;
            el.FindPropertyRelative("LaserPrefab").objectReferenceValue = laser;
            el.FindPropertyRelative("LaserData").objectReferenceValue = data;
            el.FindPropertyRelative("AttackSprite").objectReferenceValue = attack;
        }
    }

    // ── 도우미 ────────────────────────────────────────────────

    internal static RectTransform NewRect(string name, RectTransform parent, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }

    internal static TextMeshProUGUI NewText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color, Vector2 box)
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

    internal static RectTransform NewButtonRow(RectTransform canvasRt, string name, float y)
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
        return row;
    }

    internal static Button NewButton(RectTransform parent, string label, TMP_FontAsset font)
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

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
