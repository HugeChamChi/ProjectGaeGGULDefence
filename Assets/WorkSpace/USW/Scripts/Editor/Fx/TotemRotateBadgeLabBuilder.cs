using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 토템 회전 가능/불가 배지 실험실 씬(FxLab_TotemRotateBadge)을 만든다 — 사용자 10-12 "회전 가능/불가 아이콘을 정보창·선택지에서 보여줘야 함", A 원형 배지 선택.
/// 실제 화면: TotemRewardCanvas(선택지 개요·상세), Totem_Info_Panel(배치 토템 정보창). 원본 프리팹은 씬 인스턴스로만 쓰고 수정하지 않는다.
/// </summary>
public static class TotemRotateBadgeLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_TotemRotateBadge.unity";
    private const string RewardPrefabPath = "Assets/WorkSpace/USW/Prefab/UI/TotemRewardCanvas.prefab";
    private const string PanelPrefabPath = "Assets/WorkSpace/HSD/Prefab/UI/InGame/Totem_Info_Panel.prefab";
    private const string TotemRoot = "Assets/WorkSpace/USW/Data/TotemData/Playable";
    private const string RotateIconPath = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/In-game/Rotation_icon.png";
    private const string BgPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)
    private const string CaptureFolder = "outputs/totem-rotate-badge-20261012";

    private static readonly string[] RewardTotems = { "TD1002", "TD1005", "TD1010" }; // 가운데만 회전 가능
    private static readonly string[] PanelTotems = { "TD1001", "TD1002" };           // 회전 가능 / 불가

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const float LabPlane = 6f;
    private const float RewardPlane = 4f;
    private const float OverlayPlane = 2f;
    private const int OverlayOrder = 32100; // TotemRewardCanvas(32000)보다 위
    private const float SecondPanelOffset = 620f;
    private const float RowHeight = 64f;
    private const float RowStep = 70f;
    private const float RowBottom = 16f;
    private const float ButtonFontSize = 22f;

    [MenuItem("Tools/USW/Fx/Build Totem Rotate Badge Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[TotemRotateBadgeLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
    }

    /// <summary>저장 확인 없이 씬을 만든다(CLI 실행용). 현재 씬에 저장 안 된 변경이 있으면 중단한다.</summary>
    public static string BuildScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) return "현재 씬에 저장 안 된 변경이 있어 중단: " + active.path;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        if (bg != null) DisigmanRailgunFxLabBuilder.NewSprite("Battlefield_BG", bg, new Vector3(0f, 0f, 20f), 1.6f, -100);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // ── ③ 배치 정보창: 실제 프리팹 2개 (회전 가능 / 불가) ──
        var labCanvas = NewCanvas("LabCanvas", cam, LabPlane, 0);
        var panelStage = TimerBonusFxLabBuilder.NewRect("PanelStage", labCanvas, Vector2.zero);
        TimerBonusFxLabBuilder.Stretch(panelStage);
        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
        var panels = new UI_TotemInfoPanel[PanelTotems.Length];
        for (int i = 0; i < PanelTotems.Length; i++)
        {
            var holder = TimerBonusFxLabBuilder.NewRect($"PanelHolder_{i}", panelStage, Vector2.zero);
            TimerBonusFxLabBuilder.Stretch(holder);
            holder.anchoredPosition = new Vector2(0f, -SecondPanelOffset * i);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, holder);
            panels[i] = go.GetComponent<UI_TotemInfoPanel>();
        }

        // ── ①② 선택지: 실제 TotemRewardCanvas (실험실 씬에서만 카메라 캔버스로, 뽑기 풀은 3종 고정) ──
        var rewardGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RewardPrefabPath));
        var rewardCanvas = rewardGo.GetComponentInChildren<Canvas>(true);
        rewardCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        rewardCanvas.worldCamera = cam;
        rewardCanvas.planeDistance = RewardPlane;
        var reward = rewardGo.GetComponentInChildren<TotemRewardUI>(true);
        var rewardSo = new SerializedObject(reward);
        SetTotems(rewardSo.FindProperty("_totemPool"), RewardTotems);
        rewardSo.ApplyModifiedPropertiesWithoutUndo();

        // ── 배지·조작 캔버스 (맨 위) ──
        var overlayCanvas = NewCanvas("BadgeCanvas", cam, OverlayPlane, OverlayOrder);
        var badges = TimerBonusFxLabBuilder.NewRect("Badges", overlayCanvas, Vector2.zero);
        TimerBonusFxLabBuilder.Stretch(badges);

        var lab = new GameObject("TotemRotateBadgeLab").AddComponent<TotemRotateBadgeLab>();
        var rowStyles = Row(overlayCanvas, "Styles", RowBottom);
        var rowScreens = Row(overlayCanvas, "Screens", RowBottom + RowStep);
        var styleImages = new Image[TotemRotateBadgeLab.Labels.Length];
        for (int i = 0; i < styleImages.Length; i++)
        {
            var b = Button(rowStyles, TotemRotateBadgeLab.Labels[i], font);
            UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectStyle, i);
            styleImages[i] = b.GetComponent<Image>();
        }
        var screenImages = new Image[TotemRotateBadgeLab.Screens.Length];
        for (int i = 0; i < screenImages.Length; i++)
        {
            var b = Button(rowScreens, TotemRotateBadgeLab.Screens[i], font);
            UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectScreen, i);
            screenImages[i] = b.GetComponent<Image>();
        }
        UnityEventTools.AddVoidPersistentListener(Button(rowScreens, "상세 다음 토템", font).onClick, lab.NextDetail);
        UnityEventTools.AddVoidPersistentListener(Button(rowScreens, "불가 표시 켬/끔", font).onClick, lab.ToggleShowFixed);

        var status = TimerBonusFxLabBuilder.NewText("Status", overlayCanvas, font, 24f, Color.white, Vector2.zero);
        status.alignment = TextAlignmentOptions.BottomLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.outlineWidth = 0.2f;
        status.outlineColor = Color.black;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 0f);
        status.rectTransform.sizeDelta = new Vector2(-40f, 90f);
        status.rectTransform.anchoredPosition = new Vector2(0f, RowBottom + RowStep * 2f + 4f);

        var so = new SerializedObject(lab);
        so.FindProperty("_reward").objectReferenceValue = reward;
        var panelsProp = so.FindProperty("_panels");
        panelsProp.arraySize = panels.Length;
        for (int i = 0; i < panels.Length; i++) panelsProp.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
        SetTotems(so.FindProperty("_panelTotems"), PanelTotems);
        so.FindProperty("_panelStage").objectReferenceValue = panelStage.gameObject;
        so.FindProperty("_overlay").objectReferenceValue = badges;
        so.FindProperty("_rotateIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(RotateIconPath);
        so.FindProperty("_circle").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        so.FindProperty("_status").objectReferenceValue = status;
        SetArray(so.FindProperty("_styleButtons"), styleImages);
        SetArray(so.FindProperty("_screenButtons"), screenImages);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        string msg = $"[TotemRotateBadgeLab] 씬 생성: {ScenePath}";
        Debug.Log(msg);
        return msg;
    }

    /// <summary>Play 중 화면 × 위치 시안 전체를 PNG로 저장한다.</summary>
    [MenuItem("Tools/USW/Fx/Totem Rotate Badge Lab - Capture All (Play)")]
    public static string CaptureAll()
    {
        var lab = Object.FindAnyObjectByType<TotemRotateBadgeLab>();
        if (!EditorApplication.isPlaying || lab == null) { Debug.LogError("[TotemRotateBadgeLab] 플레이 중인 실험실에서만"); return "not playing"; }
        string folder = Path.GetFullPath(CaptureFolder);
        lab.CaptureAll(folder, 1080, 2336);
        return "started: " + folder;
    }

    private static RectTransform NewCanvas(string name, Camera cam, float plane, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = plane;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        return (RectTransform)go.transform;
    }

    private static Button Button(RectTransform row, string label, TMP_FontAsset font)
    {
        var b = TimerBonusFxLabBuilder.NewButton(row, label, font);
        b.GetComponentInChildren<TMP_Text>().fontSize = ButtonFontSize;
        b.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        return b;
    }

    private static void SetTotems(SerializedProperty prop, string[] ids)
    {
        prop.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++)
        {
            var data = AssetDatabase.LoadAssetAtPath<TotemData>($"{TotemRoot}/{ids[i]}Data.asset");
            if (data == null) Debug.LogWarning($"[TotemRotateBadgeLab] 토템 데이터 없음: {ids[i]}");
            prop.GetArrayElementAtIndex(i).objectReferenceValue = data;
        }
    }

    private static void SetArray(SerializedProperty prop, Object[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static RectTransform Row(RectTransform canvasRt, string name, float y)
    {
        var row = TimerBonusFxLabBuilder.NewButtonRow(canvasRt, name, 0f);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.sizeDelta = new Vector2(-24f, RowHeight);
        row.anchoredPosition = new Vector2(0f, y);
        return row;
    }
}
