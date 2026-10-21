using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>로비 외형을 복제해 백엔드와 독립된 모션 비교 씬을 만든다.</summary>
public static class LobbyBottomMotionLabBuilder
{
    /// <summary>실험실 씬 경로. Build Settings에는 추가하지 않는다.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_LobbyBottomMotion.unity";

    /// <summary>메뉴에서 독립된 하단 모션 실험실을 생성한다.</summary>
    [MenuItem("Tools/USW/UI/Build Lobby Bottom Motion Lab")]
    public static void Build() => BuildScene();

    /// <summary>원본 로비를 저장하지 않고 복제한다. 미저장 씬과 기존 실험실은 덮어쓰지 않는다.</summary>
    public static string BuildScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene; preserve it first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("Lab already exists; open it instead.");
        if (!AssetDatabase.CopyAsset("Assets/Scenes/LobbyScene.unity", ScenePath)) throw new InvalidOperationException("Lobby copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var controller = Object.FindFirstObjectByType<BottomPanelController>(FindObjectsInactive.Include);
        var tabs = controller.panelButtons;
        var bottom = (RectTransform)tabs[0]._mainRect.parent;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Imports/Font/NotoSansKR-Bold SDF.asset");
        // Unpack only the copy before stripping application behaviours.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject))
                    PrefabUtility.UnpackPrefabInstance(transform.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour == controller) continue;
                string ns = behaviour.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine.UI") || ns.StartsWith("TMPro") || ns.StartsWith("UnityEngine.EventSystems") || ns.StartsWith("UnityEngine.Rendering")) continue;
                Object.DestroyImmediate(behaviour);
            }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
                button.onClick.RemoveAllListeners();
            }
        for (var t = bottom.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        foreach (var canvas in bottom.GetComponentsInParent<Canvas>(true)) canvas.enabled = true;
        foreach (var tab in tabs) { tab._button.enabled = true; tab._button.interactable = true; }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) { camera.gameObject.SetActive(true); camera.enabled = true; }
        Object.DestroyImmediate(controller);

        var plate = Rect("SharedSelectionPlate", bottom, new Vector2(0.5f, 0f), new Vector2(210f, 228f), Vector2.zero);
        plate.SetSiblingIndex(0);
        plate.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.sprite = tabs[0]._background.sprite; plateImage.type = Image.Type.Sliced;
        plateImage.color = new Color(0.46f, 0.31f, 0.82f); plateImage.raycastTarget = false;
        var marker = Rect("SelectionUnderline", bottom, new Vector2(0.5f, 0f), new Vector2(64f, 5f), new Vector2(0f, 8f)).gameObject.AddComponent<Image>();
        marker.color = new Color(1f, 0.85f, 0.51f); marker.raycastTarget = false;
        marker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var canvasObject = new GameObject("MotionLabControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvasUI = canvasObject.GetComponent<Canvas>(); canvasUI.renderMode = RenderMode.ScreenSpaceOverlay; canvasUI.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 2340f); scaler.matchWidthOrHeight = 0.5f;
        var controls = Rect("Controls", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(1020f, 340f), new Vector2(0f, -280f));
        controls.pivot = new Vector2(0.5f, 1f);
        var bg = controls.gameObject.AddComponent<Image>(); bg.color = new Color(0.055f, 0.05f, 0.09f, 0.96f); bg.raycastTarget = false;
        var status = Text("Status", controls, font, new Vector2(980f, 135f), new Vector2(0f, -75f), 30f);
        var names = new[] { "현재", "A · 또렷", "B · 탄력", "C · 흐름", "D · B+C" };
        var variants = new Button[5];
        for (int i = 0; i < variants.Length; i++) variants[i] = Button(names[i], controls, font, new Vector2(186f, 72f), new Vector2(-396f + i * 198f, -191f));
        var demo = Button("자동 비교", controls, font, new Vector2(470f, 70f), new Vector2(-247f, -282f));
        var pages = Button("페이지 이동", controls, font, new Vector2(470f, 70f), new Vector2(247f, -282f));
        var lab = canvasObject.AddComponent<LobbyBottomMotionLab>();
        lab.Configure(tabs, plate, marker, status, variants, demo, pages);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasObject;
        return ScenePath;
    }

    /// <summary>기존 실험실에 D 비교 버튼을 추가한다. 원본 씬과 다른 구성은 보존한다.</summary>
    public static string UpgradeControls()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved lab in Edit mode first.");
        var lab = Object.FindFirstObjectByType<LobbyBottomMotionLab>();
        var serialized = new SerializedObject(lab);
        var entries = serialized.FindProperty("_variantButtons");
        var variants = new Button[5];
        for (int i = 0; i < Mathf.Min(entries.arraySize, variants.Length); i++)
            variants[i] = (Button)entries.GetArrayElementAtIndex(i).objectReferenceValue;
        var controls = variants[0].transform.parent;
        var font = variants[0].GetComponentInChildren<TMP_Text>().font;
        var names = new[] { "현재", "A · 또렷", "B · 탄력", "C · 흐름", "D · B+C" };
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] == null) variants[i] = Button(names[i], controls, font, new Vector2(186f, 72f), Vector2.zero);
            var rect = (RectTransform)variants[i].transform;
            rect.sizeDelta = new Vector2(186f, 72f);
            rect.anchoredPosition = new Vector2(-396f + i * 198f, -191f);
            var label = variants[i].GetComponentInChildren<TMP_Text>();
            label.text = names[i]; label.rectTransform.sizeDelta = new Vector2(174f, 66f);
        }
        lab.ConfigureVariantButtons(variants);
        EditorUtility.SetDirty(lab);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Five comparison buttons saved; D combines spring, glide and directional dark-surface displacement.";
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 size, Vector2 position, float fontSize)
    {
        var rect = Rect(name, parent, new Vector2(0.5f, 1f), size, position); rect.pivot = new Vector2(0.5f, 0.5f);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false; return text;
    }

    private static Button Button(string name, Transform parent, TMP_FontAsset font, Vector2 size, Vector2 position)
    {
        var rect = Rect(name, parent, new Vector2(0.5f, 1f), size, position); rect.pivot = new Vector2(0.5f, 0.5f);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.23f, 0.20f, 0.30f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Text("Label", rect, font, size - new Vector2(12f, 6f), Vector2.zero, 27f);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.text = name; return button;
    }
}
