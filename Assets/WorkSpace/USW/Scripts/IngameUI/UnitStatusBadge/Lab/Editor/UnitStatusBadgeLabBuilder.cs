using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Style = UnitStatusBadgeSettings.GaugeStyle;

/// <summary>
/// Builds the unit tier badge / skill charge lab: shipping prefab, material and settings in UnitStatusBadge/,
/// draft settings and the lab scene in UnitStatusBadge/Lab/.
/// </summary>
public static class UnitStatusBadgeLabBuilder
{
    private const string Feature = "Assets/WorkSpace/USW/Scripts/IngameUI/UnitStatusBadge";
    private const string Lab = Feature + "/Lab";
    /// <summary>Lab scene copied from the game scene on first build.</summary>
    public const string ScenePath = Lab + "/FxLab_UnitStatusBadge.unity";
    private const string SettingsPath = Feature + "/UnitStatusBadgeSettings.asset";
    private const string MaterialPath = Feature + "/UnitStatusBadge.mat";
    private const string ShaderPath = Feature + "/UnitStatusBadge.shader";
    private const string PrefabPath = Feature + "/Resources/UnitStatusBadge.prefab";
    private const string ButtonSprite = "Assets/WorkSpace/USW/Data/UI/BottomHudButtonSprite.asset";
    private const string ButtonFont = "Assets/Imports/Font/NotoSansKR-Bold SDF.asset";
    private const string DroneData = "Assets/WorkSpace/USW/Data/DroneUnits/";
    // Same world-canvas scale as the previous status prefab (100 UI units = 1 world unit).
    private const float PrefabScale = 0.01f;

    // Drafts all follow the reference tag (design/이렇게하는데.*): one dark tag, the tier number, quiet charge, no glow.
    private static readonly string[] DraftNames = { "1 태그+바", "2 등급색 숫자", "3 눈금 바", "4 테두리 충전", "5 칸 채움" };
    private static readonly Action<UnitStatusBadgeSettings>[] DraftConfigs = { ConfigureTagBar, ConfigureTierNumbers, ConfigureSegments, ConfigureBorder, ConfigureFill };
    private static readonly string[] ModeNames = { "고정 0/50/100%", "진행", "준비 순간" };
    // Roster: timed skills (Betan 14 s, Gamman 6 s), attack-charged (Deltan, Disigman), passive only (Zeltan).
    private static readonly string[] Roster = { "UnitData_Betan", "UnitData_Gamman", "UnitData_Zeltan", "UnitData_Deltan", "UnitData_Disigman" };
    private static readonly int[] ColumnUnit = { 0, 1, 2, 3, 1, 4 };
    private static readonly float[] Charges = { 0f, 0.5f, 1f };

    /// <summary>Menu entry.</summary>
    [MenuItem("Tools/USW/Fx/Build Unit Status Badge Lab")]
    public static void Build() => Run();

    /// <summary>Automation entry; copies the game scene and creates settings only on first creation.</summary>
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rebuilding the lab.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("An open scene has unsaved changes; preserve it before rebuilding.");

        var draftPaths = new string[DraftNames.Length];
        for (int i = 0; i < draftPaths.Length; i++)
        {
            draftPaths[i] = $"{Lab}/UnitStatusBadgeSettings_{i + 1}.asset";
            EnsureSettings(draftPaths[i], DraftConfigs[i]);
        }
        EnsureSettings(SettingsPath, ConfigureTagBar);
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
            AssetDatabase.CreateAsset(new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath)), MaterialPath);
        AssetDatabase.SaveAssets();
        SavePrefab();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset("Assets/Scenes/IngameScene.unity", ScenePath))
            throw new InvalidOperationException("Could not copy the game scene.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        // Load after opening: the scene switch unloads unreferenced assets held from before.
        var drafts = Array.ConvertAll(draftPaths, AssetDatabase.LoadAssetAtPath<UnitStatusBadgeSettings>);
        var prefab = AssetDatabase.LoadAssetAtPath<UnitStatusBadge>(PrefabPath);
        var roster = Array.ConvertAll(Roster, n => AssetDatabase.LoadAssetAtPath<UnitData>(DroneData + n + ".asset"));
        if (Array.Exists(roster, d => d == null)) throw new InvalidOperationException("Missing roster UnitData.");

        foreach (var old in scene.GetRootGameObjects())
            if (old.name == "Unit Status Badge Lab Controls" || old.name == "Unit Status Badge Lab Driver") UnityEngine.Object.DestroyImmediate(old);
        var controls = new GameObject("Unit Status Badge Lab Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = controls.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        var scaler = controls.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;

        var current = MakeButton(controls.transform, "Draft Current", "현재", new Vector2(-440f, -400f), new Vector2(164f, 84f));
        var draftButtons = new Button[DraftNames.Length];
        for (int i = 0; i < DraftNames.Length; i++)
            draftButtons[i] = MakeButton(controls.transform, "Draft " + (i + 1), DraftNames[i], new Vector2(-264f + i * 176f, -400f), new Vector2(164f, 84f), 26f);
        var modeButtons = new Button[ModeNames.Length];
        for (int i = 0; i < ModeNames.Length; i++)
            modeButtons[i] = MakeButton(controls.transform, "Mode " + i, ModeNames[i], new Vector2(-322f + i * 322f, -500f), new Vector2(306f, 84f));
        string[] legendNames = { "숫자 4", "흰 별", "빈 별", "금+쓸기", "금+가루", "금+무지개", "금+테두리" };
        var legendButtons = new Button[legendNames.Length];
        for (int i = 0; i < legendNames.Length; i++)
            legendButtons[i] = MakeButton(controls.transform, "Legend " + i, legendNames[i], new Vector2(-456f + i * 152f, -600f), new Vector2(144f, 84f), 24f);
        var caption = Text(controls.transform, "Caption", 30f, new Vector2(0f, -680f), new Vector2(1040f, 56f));
        caption.alignment = TextAlignmentOptions.Center;

        // 6 columns x 4 rows, column-major: tiers rotate per row so every column mixes all four.
        int cells = 24;
        var cellUnit = new int[cells];
        var cellTier = new Tier[cells];
        var cellCharge = new float[cells];
        for (int x = 0, i = 0; x < 6; x++)
        for (int z = 0; z < 4; z++, i++)
        {
            cellUnit[i] = ColumnUnit[x];
            cellTier[i] = (Tier)((x + z) % 4);
            cellCharge[i] = Charges[(x + 2 * z) % 3];
        }

        var driver = new GameObject("Unit Status Badge Lab Driver").AddComponent<UnitStatusBadgeFxLab>();
        var so = new SerializedObject(driver);
        so.FindProperty("_prefab").objectReferenceValue = prefab;
        SetArray(so.FindProperty("_drafts"), drafts);
        var names = so.FindProperty("_draftNames");
        names.arraySize = DraftNames.Length;
        for (int i = 0; i < DraftNames.Length; i++) names.GetArrayElementAtIndex(i).stringValue = DraftNames[i];
        SetArray(so.FindProperty("_roster"), roster);
        var unitProp = so.FindProperty("_cellUnit");
        var tierProp = so.FindProperty("_cellTier");
        var chargeProp = so.FindProperty("_cellCharge");
        unitProp.arraySize = tierProp.arraySize = chargeProp.arraySize = cells;
        for (int i = 0; i < cells; i++)
        {
            unitProp.GetArrayElementAtIndex(i).intValue = cellUnit[i];
            tierProp.GetArrayElementAtIndex(i).enumValueIndex = (int)cellTier[i];
            chargeProp.GetArrayElementAtIndex(i).floatValue = cellCharge[i];
        }
        so.FindProperty("_currentButton").objectReferenceValue = current;
        SetArray(so.FindProperty("_draftButtons"), draftButtons);
        SetArray(so.FindProperty("_modeButtons"), modeButtons);
        SetArray(so.FindProperty("_legendButtons"), legendButtons);
        so.FindProperty("_caption").objectReferenceValue = caption;
        so.FindProperty("_controls").objectReferenceValue = controls;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return ScenePath;
    }

    // 1: the reference itself. White number, thin yellow bar; the top tier only turns gold (number + thin rim).
    private static void ConfigureTagBar(UnitStatusBadgeSettings s)
    {
        s.Style = Style.TagBar;
        s.FrameSize = new Vector2(104f, 32f);
        s.TagSize = 30f;
        s.BarHeight = 11f;
    }

    // 2: as 1, with a faint tier tint on the number only.
    private static void ConfigureTierNumbers(UnitStatusBadgeSettings s)
    {
        ConfigureTagBar(s);
        s.Tiers[0].Number = new Color(0.93f, 0.94f, 0.96f);
        s.Tiers[1].Number = new Color(0.58f, 0.82f, 1f);
        s.Tiers[2].Number = new Color(0.82f, 0.64f, 1f);
    }

    // 3: as 1, with the bar split in quarters so half charge reads at a glance.
    private static void ConfigureSegments(UnitStatusBadgeSettings s)
    {
        ConfigureTagBar(s);
        s.BarHeight = 12f;
        s.BarSegments = 4;
        s.BarSegmentGap = 2.5f;
    }

    // 4: no bar; the tag border fills clockwise. Smallest footprint.
    private static void ConfigureBorder(UnitStatusBadgeSettings s)
    {
        s.Style = Style.TagBorder;
        s.FrameSize = new Vector2(40f, 40f);
        s.TagSize = 36f;
        s.BorderWidth = 0.2f;
        s.Tiers[3].RimWidth = 0f;
    }

    // 5: no bar; the tag background fills from the bottom behind the number.
    private static void ConfigureFill(UnitStatusBadgeSettings s)
    {
        s.Style = Style.TagFill;
        s.FrameSize = new Vector2(40f, 40f);
        s.TagSize = 36f;
        s.FillStrength = 0.55f;
    }

    private static void EnsureSettings(string path, Action<UnitStatusBadgeSettings> configure)
    {
        if (AssetDatabase.LoadAssetAtPath<UnitStatusBadgeSettings>(path) != null) return;
        var asset = ScriptableObject.CreateInstance<UnitStatusBadgeSettings>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
    }

    private static void SavePrefab()
    {
        var root = new GameObject("UnitStatusBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnitStatusBadge));
        root.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)root.transform;
        rt.localScale = new Vector3(PrefabScale, PrefabScale, 1f);
        var badge = root.GetComponent<UnitStatusBadge>();
        badge.material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        badge.raycastTarget = false;
        badge.Settings = AssetDatabase.LoadAssetAtPath<UnitStatusBadgeSettings>(SettingsPath);
        root.GetComponent<CanvasRenderer>().cullTransparentMesh = true;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static Button MakeButton(Transform parent, string name, string caption, Vector2 position, Vector2 size, float fontSize = 30f)
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
        var label = Text(rt, "Label", fontSize, Vector2.zero, size);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.alignment = TextAlignmentOptions.Center;
        label.text = caption;
        return button;
    }

    private static TMP_Text Text(Transform parent, string name, float sizePt, Vector2 position, Vector2 size)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ButtonFont);
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
