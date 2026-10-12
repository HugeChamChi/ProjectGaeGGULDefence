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
/// 획득 선택지 보관함 실험실 씬(FxLab_SelectionInventory)을 만든다 — 사용자 10-13 A안 선택 → 프리팹화.
/// 실제 SelectionInventory 프리팹을 인스턴스로 놓고, 인게임처럼 SubUI(오버레이 4) 아래 Unit_Info_Panel을 둔다.
/// 프리팹이 없으면 먼저 SelectionInventoryPrefabBuilder로 만든다. 원본 프리팹은 수정하지 않는다.
/// </summary>
public static class SelectionInventoryLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_SelectionInventory.unity";
    private const string CardFolder = "Assets/WorkSpace/USW/Data/SelectionData";
    private const string InfoPanelPath = "Assets/WorkSpace/HSD/Prefab/UI/InGame/Unit_Info_Panel.prefab";
    private const string BgPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string FontGuid = "34571da45f3f3d8419493b418172203f"; // NotoSansKR-Bold SDF
    private const string CaptureFolder = "outputs/selection-inventory-20261013";
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const int SubUiOrder = 4;      // 인게임 '/// SubUI'와 같음
    private const int ControlsOrder = 100; // 실험실 조작 버튼은 모든 층 위

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
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SelectionInventoryPrefabBuilder.PrefabPath);
        if (prefab == null)
        {
            SelectionInventoryPrefabBuilder.Build();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SelectionInventoryPrefabBuilder.PrefabPath);
        }

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

        // 인게임처럼 SubUI 오버레이 캔버스 아래 실제 유닛 정보창
        var subUi = Overlay("SubUI", SubUiOrder);
        var holder = new GameObject("InfoPanelHolder", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(subUi, false);
        holder.anchorMin = Vector2.zero;
        holder.anchorMax = Vector2.one;
        holder.offsetMin = holder.offsetMax = Vector2.zero;
        var panelGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(InfoPanelPath), holder);
        var infoPanel = panelGo != null ? panelGo.GetComponent<UI_UnitInfoPanel>() : null;

        var inventory = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<SelectionInventoryUI>();

        var controlsCanvas = Overlay("LabControls", ControlsOrder);
        var controls = new GameObject("Samples", typeof(RectTransform)).GetComponent<RectTransform>();
        controls.SetParent(controlsCanvas, false);
        controls.anchorMin = new Vector2(0f, 1f);
        controls.anchorMax = new Vector2(1f, 1f);
        controls.pivot = new Vector2(0.5f, 1f);
        controls.sizeDelta = new Vector2(-24f, 62f);
        controls.anchoredPosition = new Vector2(0f, -24f);

        var cards = AssetDatabase.FindAssets("t:LevelUpData", new[] { CardFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelUpData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null).OrderBy(c => c.chooseId).ToArray();

        var lab = new GameObject("SelectionInventoryLab").AddComponent<SelectionInventoryLab>();
        var so = new SerializedObject(lab);
        so.FindProperty("_inventory").objectReferenceValue = inventory;
        so.FindProperty("_infoPanel").objectReferenceValue = infoPanel;
        so.FindProperty("_controls").objectReferenceValue = controls;
        so.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        so.FindProperty("_rounded").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var cardsProp = so.FindProperty("_cards");
        cardsProp.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++) cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        string msg = $"[SelectionInventoryLab] 씬 생성: {ScenePath} (프리팹 인스턴스, 선택지 {cards.Length}장)";
        Debug.Log(msg);
        return msg;
    }

    /// <summary>Play 중 표본·채움·용어·정보창·족장 카드를 PNG로 저장한다.</summary>
    [MenuItem("Tools/USW/Fx/Selection Inventory Lab - Capture All (Play)")]
    public static string CaptureAll()
    {
        var lab = Object.FindAnyObjectByType<SelectionInventoryLab>();
        if (!EditorApplication.isPlaying || lab == null) { Debug.LogError("[SelectionInventoryLab] 플레이 중인 실험실에서만"); return "not playing"; }
        string folder = Path.GetFullPath(CaptureFolder);
        lab.CaptureAll(folder);
        return "started: " + folder;
    }

    private static RectTransform Overlay(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        return (RectTransform)go.transform;
    }
}
