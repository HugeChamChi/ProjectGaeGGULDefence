using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Builds the L35 HUD label lab: shipping prefabs/settings in HudLabels/, drafts and scene in HudLabels/Lab/.</summary>
public static class HudLabelsLabBuilder
{
    private const string Feature = "Assets/WorkSpace/USW/Scripts/IngameUI/HudLabels";
    private const string Lab = Feature + "/Lab";
    /// <summary>Lab scene copied from the game scene on first build.</summary>
    public const string ScenePath = Lab + "/FxLab_HudLabels.unity";
    private const string SettingsPath = Feature + "/HudLabelSettings.asset";
    private const string TimerPrefabPath = Feature + "/HudTimerLabel.prefab";
    private const string HealthPrefabPath = Feature + "/HudHealthLabel.prefab";
    private const string ButtonSprite = "Assets/WorkSpace/USW/Data/UI/BottomHudButtonSprite.asset";
    private const string ButtonFont = "Assets/Imports/Font/NotoSansKR-Bold SDF.asset";
    private const string LabelFont = "Assets/Imports/Font/KCC-Ganpan SDF.asset";

    private static readonly string[] PresetNames = { "A 글자 라벨", "B 아이콘 + 짧은 라벨", "C 글자 + 10초 경고" };
    private static readonly string[] StateNames = { "여유", "10초 이하", "체력 여러 줄", "등장 대기" };

    /// <summary>Menu entry.</summary>
    [MenuItem("Tools/USW/Fx/Build HUD Labels Lab (L35)")]
    public static void Build() => Run();

    /// <summary>Automation entry; copies the game scene only on first creation.</summary>
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rebuilding the lab.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("An open scene has unsaved changes; preserve it before rebuilding.");

        string[] presetPaths = { Lab + "/HudLabelSettings_A.asset", Lab + "/HudLabelSettings_B.asset", Lab + "/HudLabelSettings_C.asset" };
        EnsureSettings(SettingsPath, s => { s.ShowIcons = false; s.WarnWhenLow = true; });
        EnsureSettings(presetPaths[0], s => { s.ShowIcons = false; s.WarnWhenLow = false; });
        EnsureSettings(presetPaths[1], s => { s.ShowIcons = true; s.WarnWhenLow = false; s.TimerLabel = "시간"; s.WaitTimerLabel = "등장"; });
        EnsureSettings(presetPaths[2], s => { s.ShowIcons = false; s.WarnWhenLow = true; });
        AssetDatabase.SaveAssets();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset("Assets/Scenes/IngameScene.unity", ScenePath))
            throw new InvalidOperationException("Could not copy the game scene.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        // Load after opening: the scene switch unloads unreferenced assets held from before.
        var shipping = AssetDatabase.LoadAssetAtPath<HudLabelSettings>(SettingsPath);
        var presets = Array.ConvertAll(presetPaths, AssetDatabase.LoadAssetAtPath<HudLabelSettings>);
        var ui = UnityEngine.Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        var uiSo = new SerializedObject(ui);
        var timerText = (TMP_Text)uiSo.FindProperty("timerText").objectReferenceValue;
        var bar = (UI_BossHpBar)uiSo.FindProperty("_bossHpBar").objectReferenceValue;
        var timerPanel = (RectTransform)timerText.transform.parent;

        // Timer label root covers the timer panel: caption sits outside its left edge, warning outside its right edge.
        var timerPrefab = SaveLabel(TimerPrefabPath, "HudTimerLabel", HudValueLabel.ValueKind.Timer, HudGlyph.Shape.Hourglass,
            shipping, timerText.fontSharedMaterial, alignRight: true,
            rootMin: Vector2.zero, rootMax: Vector2.one, rootPivot: new Vector2(0.5f, 0.5f), rootPosition: Vector2.zero, rootSize: Vector2.zero,
            textMin: Vector2.zero, textMax: new Vector2(0f, 1f), textPivot: new Vector2(1f, 0.5f), textPosition: new Vector2(24f, -4f), textSize: new Vector2(190f, 0f),
            iconSize: new Vector2(34f, 42f), warning: true);
        var healthPrefab = SaveLabel(HealthPrefabPath, "HudHealthLabel", HudValueLabel.ValueKind.Health, HudGlyph.Shape.Heart,
            shipping, timerText.fontSharedMaterial, alignRight: false,
            rootMin: new Vector2(0f, 1f), rootMax: new Vector2(0f, 1f), rootPivot: new Vector2(0f, 0.5f), rootPosition: new Vector2(72f, -2f), rootSize: new Vector2(220f, 44f),
            textMin: Vector2.zero, textMax: Vector2.one, textPivot: new Vector2(0.5f, 0.5f), textPosition: Vector2.zero, textSize: Vector2.zero,
            iconSize: new Vector2(38f, 34f), warning: false);

        foreach (var old in timerPanel.GetComponentsInChildren<HudValueLabel>(true)) UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach (var old in bar.GetComponentsInChildren<HudValueLabel>(true)) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var timerLabel = ((GameObject)PrefabUtility.InstantiatePrefab(timerPrefab, timerPanel)).GetComponent<HudValueLabel>();
        var healthLabel = ((GameObject)PrefabUtility.InstantiatePrefab(healthPrefab, bar.transform)).GetComponent<HudValueLabel>();

        foreach (var old in scene.GetRootGameObjects())
            if (old.name == "HUD Labels Lab Controls" || old.name == "HUD Labels Lab Driver") UnityEngine.Object.DestroyImmediate(old);
        var controls = new GameObject("HUD Labels Lab Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = controls.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        var scaler = controls.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;

        var presetButtons = new Button[3];
        for (int i = 0; i < 3; i++)
            presetButtons[i] = MakeButton(controls.transform, "Preset " + (char)('A' + i), ((char)('A' + i)).ToString(),
                new Vector2(-330f + i * 170f, -400f), new Vector2(150f, 84f), out _);
        var freeze = MakeButton(controls.transform, "Freeze", "멈춤", new Vector2(330f, -400f), new Vector2(190f, 84f), out var freezeLabel);
        var stateButtons = new Button[StateNames.Length];
        for (int i = 0; i < StateNames.Length; i++)
            stateButtons[i] = MakeButton(controls.transform, "State " + i, StateNames[i], new Vector2(-363f + i * 242f, -500f), new Vector2(226f, 84f), out _);
        var caption = Text(controls.transform, "Caption", ButtonFont, 34f, new Vector2(0f, -580f), new Vector2(1000f, 56f));
        caption.alignment = TextAlignmentOptions.Center;

        var driver = new GameObject("HUD Labels Lab Driver").AddComponent<HudLabelsFxLab>();
        var so = new SerializedObject(driver);
        so.FindProperty("_timerLabel").objectReferenceValue = timerLabel;
        so.FindProperty("_healthLabel").objectReferenceValue = healthLabel;
        SetArray(so.FindProperty("_presets"), presets);
        var names = so.FindProperty("_presetNames");
        names.arraySize = PresetNames.Length;
        for (int i = 0; i < PresetNames.Length; i++) names.GetArrayElementAtIndex(i).stringValue = PresetNames[i];
        SetArray(so.FindProperty("_presetButtons"), presetButtons);
        SetArray(so.FindProperty("_stateButtons"), stateButtons);
        so.FindProperty("_freezeButton").objectReferenceValue = freeze;
        so.FindProperty("_freezeLabel").objectReferenceValue = freezeLabel;
        so.FindProperty("_caption").objectReferenceValue = caption;
        so.FindProperty("_controls").objectReferenceValue = controls;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return ScenePath;
    }

    private static void EnsureSettings(string path, Action<HudLabelSettings> configure)
    {
        if (AssetDatabase.LoadAssetAtPath<HudLabelSettings>(path) != null) return;
        var asset = ScriptableObject.CreateInstance<HudLabelSettings>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
    }

    private static GameObject SaveLabel(string path, string name, HudValueLabel.ValueKind kind, HudGlyph.Shape shape,
        HudLabelSettings settings, Material material, bool alignRight,
        Vector2 rootMin, Vector2 rootMax, Vector2 rootPivot, Vector2 rootPosition, Vector2 rootSize,
        Vector2 textMin, Vector2 textMax, Vector2 textPivot, Vector2 textPosition, Vector2 textSize,
        Vector2 iconSize, bool warning)
    {
        var root = new GameObject(name, typeof(RectTransform));
        Place((RectTransform)root.transform, rootMin, rootMax, rootPivot, rootPosition, rootSize);
        var text = Label(root.transform, "Text", material);
        Place(text.rectTransform, textMin, textMax, textPivot, textPosition, textSize);
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(HudGlyph)).GetComponent<HudGlyph>();
        icon.rectTransform.SetParent(text.rectTransform, false);
        icon.rectTransform.sizeDelta = iconSize;
        icon.GlyphShape = shape;
        icon.raycastTarget = false;
        TMP_Text warn = null;
        if (warning)
        {
            // Opposite side of the number from the caption; grows rightwards when it pulses.
            warn = Label(root.transform, "Warning", material);
            Place(warn.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, -4f), new Vector2(300f, 0f));
            warn.alignment = TextAlignmentOptions.Left;
            warn.textWrappingMode = TextWrappingModes.NoWrap;
            warn.gameObject.SetActive(false);
        }
        var label = root.AddComponent<HudValueLabel>();
        var so = new SerializedObject(label);
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_kind").enumValueIndex = (int)kind;
        so.FindProperty("_text").objectReferenceValue = text;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_warning").objectReferenceValue = warn;
        so.FindProperty("_alignRight").boolValue = alignRight;
        so.ApplyModifiedPropertiesWithoutUndo();
        label.Apply();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void Place(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, Material material)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabelFont);
        if (material != null) text.fontSharedMaterial = material;
        text.fontSize = 32f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.raycastTarget = false;
        return text;
    }

    private static Button MakeButton(Transform parent, string name, string caption, Vector2 position, Vector2 size, out TMP_Text label)
    {
        var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        var rt = (RectTransform)button.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        var image = button.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSprite);
        image.type = Image.Type.Sliced;
        label = Text(rt, "Label", ButtonFont, 32f, Vector2.zero, size);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.alignment = TextAlignmentOptions.Center;
        label.text = caption;
        return button;
    }

    private static TMP_Text Text(Transform parent, string name, string font, float sizePt, Vector2 position, Vector2 size)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(font);
        text.fontSize = sizePt;
        text.color = new Color(0.98f, 0.96f, 0.89f);
        text.raycastTarget = false;
        return text;
    }

    private static void SetArray(SerializedProperty property, UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
