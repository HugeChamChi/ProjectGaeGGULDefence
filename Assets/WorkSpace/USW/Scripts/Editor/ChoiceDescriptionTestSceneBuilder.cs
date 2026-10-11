using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ChoiceDescriptionTestSceneBuilder
{
    public const string ScenePath = "Assets/WorkSpace/USW/Scenes/ChoiceDescriptionTest.unity";
    public const string CardPath = "Assets/WorkSpace/USW/Prefabs/UI/LevelupSelectCardUi.prefab";
    private static Material _textMaterial;

    [MenuItem("Tools/Descriptions/Build Standalone Test Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Build the scene outside Play mode.");
        var sourceScene = SceneManager.GetSceneByPath("Assets/Scenes/IngameScene.unity");
        bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
        if (openedSource) sourceScene = EditorSceneManager.OpenScene("Assets/Scenes/IngameScene.unity", OpenSceneMode.Additive);
        var production = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelUpUI>(true)).First();
        var productionFields = new SerializedObject(production);
        var productionPanel = (GameObject)productionFields.FindProperty("obj").objectReferenceValue;
        var prefab = (LevelUpCardUI)productionFields.FindProperty("cardPrefab").objectReferenceValue;
        var source = prefab.DescriptionText;
        var sourceCanvas = productionPanel.GetComponentInParent<Canvas>(true);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var camera = new GameObject("PreviewCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.22f, 0.14f, 0.31f);
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvasRoot = new GameObject("ChoiceDescriptionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        EditorUtility.CopySerialized(sourceCanvas.GetComponent<CanvasScaler>(), canvasRoot.GetComponent<CanvasScaler>());
        _textMaterial = source.fontSharedMaterial;
        var field = Text(canvasRoot.transform, "FieldPreview", source, Vector2.zero, new Vector2(900, 300), 32, "필드 미리보기");
        var panelObject = UnityEngine.Object.Instantiate(productionPanel, canvasRoot.transform, false);
        panelObject.name = "LevelUpUI";
        panelObject.SetActive(true);
        var panel = (RectTransform)panelObject.transform;
        var cards = panel.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "LevelUp_UIArea");
        var timer = panel.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "TimeText");
        var group = panelObject.GetComponent<CanvasGroup>() ?? panelObject.AddComponent<CanvasGroup>();
        var peek = panelObject.GetComponentInChildren<UI_Peekthrough>(true);
        if (peek == null) peek = panelObject.AddComponent<UI_Peekthrough>();
        Set(peek, "_targetGroup", group);
        var controls = Stretch(canvasRoot.transform, "TestControls");
        var status = Text(controls, "Status", source, new Vector2(0, -760), new Vector2(950, 140), 24, "");
        status.enabled = false;
        var resetRect = Box(controls, "ShowChoicesAgain", new Vector2(0, -870), new Vector2(420, 70));
        resetRect.gameObject.AddComponent<Image>().color = new Color(0.32f, 0.21f, 0.43f);
        var reset = resetRect.gameObject.AddComponent<Button>();
        var resetLabel = Text(resetRect, "Label", source, Vector2.zero, new Vector2(420, 70), 28, "다음 선택지 3장");
        resetLabel.color = Color.white;
        var preview = DescriptionToggleView.Create(controls, source, _ => { });
        preview.name = "TutorialPreviewOnlyToggle";
        var previewRect = (RectTransform)preview.transform;
        previewRect.anchorMin = previewRect.anchorMax = new Vector2(0, 1);
        previewRect.pivot = new Vector2(0, 1);
        previewRect.anchoredPosition = new Vector2(24, -24);
        preview.GetComponentInChildren<TMP_Text>().text = "미리보기 전용";
        preview.isOn = false;
        preview.gameObject.SetActive(false);
        var manager = new GameObject("DescriptionRunContext").AddComponent<LevelUpManager>();
        var collect = canvasRoot.AddComponent<LevelUpCollectEffect>();
        EditorUtility.CopySerialized(production.GetComponent<LevelUpCollectEffect>(), collect);
        Set(collect, "_hudRoot", canvasRoot.GetComponent<RectTransform>());
        var reveal = canvasRoot.AddComponent<LevelUpRevealSequence>();
        EditorUtility.CopySerialized(production.GetComponent<LevelUpRevealSequence>(), reveal);
        Set(reveal, "_effectRoot", panel);
        Set(reveal, "_tierReveal", panel.GetComponentInChildren<TierRevealFx>(true));
        var select = canvasRoot.AddComponent<LevelUpSelectSequence>();
        EditorUtility.CopySerialized(production.GetComponent<LevelUpSelectSequence>(), select);
        var harness = canvasRoot.AddComponent<ChoiceDescriptionTestHarness>();
        Set(harness, "panel", panelObject); Set(harness, "cardContainer", cards); Set(harness, "cardPrefab", prefab);
        Set(harness, "status", status); Set(harness, "timer", timer); Set(harness, "field", field);
        Set(harness, "reset", reset); Set(harness, "previewOnly", preview); Set(harness, "manager", manager);
        var so = new SerializedObject(harness);
        var choices = so.FindProperty("choices"); choices.arraySize = 20;
        int[] indices = new[] { 1, 8, 11 }.Concat(Enumerable.Range(1, 20).Where(i => i != 1 && i != 8 && i != 11)).ToArray();
        for (int i = 0; i < indices.Length; i++)
            choices.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelUpData>(
                "Assets/WorkSpace/USW/Data/SelectionData/DroneSelection" + indices[i] + ".asset");
        so.ApplyModifiedPropertiesWithoutUndo();
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        foreach (var other in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != scene && s.path == ScenePath).ToArray())
            EditorSceneManager.CloseScene(other, true);
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (openedSource) EditorSceneManager.CloseScene(sourceScene, true);
        Debug.Log("[Descriptions] Test scene saved with production panel, cards and presentation: " + ScenePath);
    }
    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    { var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static RectTransform Stretch(Transform parent, string name)
    {
        var rect = Box(parent, name, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static TMP_Text Text(Transform parent, string name, TMP_Text source, Vector2 position, Vector2 size, float fontSize, string content)
    {
        var text = Box(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = source.font; text.fontSharedMaterial = _textMaterial;
        text.fontSize = fontSize; text.color = new Color(0.22f, 0.14f, 0.31f);
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal; text.text = content;
        return text;
    }
}
