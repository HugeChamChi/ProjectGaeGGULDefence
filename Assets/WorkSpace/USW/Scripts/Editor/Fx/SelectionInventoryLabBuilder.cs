using System.IO;
using System.Linq;
using GaeGGUL.UI.Unit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 획득 선택지 목록 실험실 씬(FxLab_SelectionInventory)을 만든다 — 사용자 10-13 "≡ 옵션의 레벨업 선택지 보유 목록 임시 UI 시안",
/// "어느 유닛 것인지 + 정보창 팝업", "번잡하지 않게" → A안. 2차: 채움 연출 + 용어 링크/용어 시트/핵심 용어 영상.
/// 실제 Unit_Info_Panel 프리팹은 씬 인스턴스로만 쓰고 수정하지 않는다.
/// </summary>
public static class SelectionInventoryLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_SelectionInventory.unity";
    private const string CardFolder = "Assets/WorkSpace/USW/Data/SelectionData";
    private const string UnitFolder = "Assets/WorkSpace/USW/Data/DroneUnits";
    private const string ChiefPath = "Assets/WorkSpace/USW/Data/Chieftain/AlphanChieftain.asset";
    private const string InfoPanelPath = "Assets/WorkSpace/HSD/Prefab/UI/InGame/Unit_Info_Panel.prefab";
    private const string DroneIconPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/Drone_1.png";
    private const string GlossaryPath = "Assets/WorkSpace/USW/Data/UI/TermGlossary.asset";          // 레벨업 선택지와 같은 용어 사전
    private const string BlurShaderPath = "Assets/WorkSpace/USW/Shaders/UI/UI_SnapshotBlur.shader"; // LevelUpUI._descriptionBlurShader와 같음
    private const string BgPath ="Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string TitleFontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)
    private const string BodyFontGuid = "34571da45f3f3d8419493b418172203f";  // NotoSansKR-Bold SDF
    private const string CaptureFolder = "outputs/selection-inventory-20261013";

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const float CanvasPlane = 5f;

    [MenuItem("Tools/USW/Fx/Build Selection Inventory Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[SelectionInventoryLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
    }

    /// <summary>저장 확인 없이 씬을 만든다(CLI 실행용). 현재 씬에 저장 안 된 변경이 있으면 중단한다.</summary>
    public static string BuildScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) return "현재 씬에 저장 안 된 변경이 있어 중단: " + active.path;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        if (bg != null) DisigmanRailgunFxLabBuilder.NewSprite("Battlefield_BG", bg, new Vector3(0f, 0f, 20f), 1.6f, -100);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = CanvasPlane;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;

        // 실제 유닛 정보창 (Override Sorting 30으로 목록 위에 뜬다)
        var holder = new GameObject("InfoPanelHolder", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(canvasGo.transform, false);
        holder.anchorMin = Vector2.zero;
        holder.anchorMax = Vector2.one;
        holder.offsetMin = Vector2.zero;
        holder.offsetMax = Vector2.zero;
        var panelGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(InfoPanelPath), holder);
        var infoPanel = panelGo != null ? panelGo.GetComponent<UI_UnitInfoPanel>() : null;
        if (infoPanel == null) Debug.LogWarning("[SelectionInventoryLab] Unit_Info_Panel 없음 — 얼굴을 눌러도 정보창이 뜨지 않는다");

        var cards = AssetDatabase.FindAssets("t:LevelUpData", new[] { CardFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelUpData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .OrderBy(c => c.chooseId)
            .ToArray();

        var lab = new GameObject("SelectionInventoryLab").AddComponent<SelectionInventoryLab>();
        var so = new SerializedObject(lab);
        var cardsProp = so.FindProperty("_cards");
        cardsProp.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++) cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        so.FindProperty("_betan").objectReferenceValue = Unit("Betan");
        so.FindProperty("_gamman").objectReferenceValue = Unit("Gamman");
        so.FindProperty("_deltan").objectReferenceValue = Unit("Deltan");
        so.FindProperty("_zeltan").objectReferenceValue = Unit("Zeltan");
        so.FindProperty("_chief").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ChieftainData>(ChiefPath);
        so.FindProperty("_droneIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(DroneIconPath);
        so.FindProperty("_glossary").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TermGlossary>(GlossaryPath);
        so.FindProperty("_blurShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(BlurShaderPath);
        so.FindProperty("_canvas").objectReferenceValue = canvasGo.transform;
        so.FindProperty("_infoPanel").objectReferenceValue = infoPanel;
        so.FindProperty("_titleFont").objectReferenceValue = Font(TitleFontGuid);
        so.FindProperty("_bodyFont").objectReferenceValue = Font(BodyFontGuid);
        so.FindProperty("_rounded").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        so.FindProperty("_circle").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        string msg = $"[SelectionInventoryLab] 씬 생성: {ScenePath} (선택지 {cards.Length}장)";
        Debug.Log(msg);
        return msg;
    }

    /// <summary>Play 중 시안 × 표본 전체와 정보창 연결을 PNG로 저장한다.</summary>
    [MenuItem("Tools/USW/Fx/Selection Inventory Lab - Capture All (Play)")]
    public static string CaptureAll()
    {
        var lab = Object.FindAnyObjectByType<SelectionInventoryLab>();
        if (!EditorApplication.isPlaying || lab == null) { Debug.LogError("[SelectionInventoryLab] 플레이 중인 실험실에서만"); return "not playing"; }
        string folder = Path.GetFullPath(CaptureFolder);
        lab.CaptureAll(folder);
        return "started: " + folder;
    }

    private static UnitData Unit(string name)
    {
        var data = AssetDatabase.LoadAssetAtPath<UnitData>($"{UnitFolder}/UnitData_{name}.asset");
        if (data == null) Debug.LogWarning($"[SelectionInventoryLab] 유닛 데이터 없음: {name}");
        return data;
    }

    private static TMP_FontAsset Font(string guid)
        => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
}
