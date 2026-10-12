using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GaeGGUL.Tutorial;
using HSD.UI.Setting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Packages the production options UI and replaces scene copies while preserving scene references.</summary>
public static class IngameOptionsPrefabBuilder
{
    /// <summary>The shared prefab used by both gameplay scenes.</summary>
    public const string PrefabPath = "Assets/WorkSpace/USW/Prefab/UI/IngameOptionsMenu.prefab";
    private const string IngamePath = "Assets/Scenes/IngameScene.unity";
    private const string TutorialPath = "Assets/WorkSpace/USW/Tutorial/Scenes/TutorialScene.unity";
    private const string SettingsPath = "Assets/WorkSpace/HSD/Prefab/UI/InGame/InGame_Setting_Popup.prefab";
    private const string InventoryPath = "Assets/WorkSpace/USW/Prefab/UI/SelectionInventory/SelectionInventory.prefab";
    private const string Evidence = "outputs/ingame-options-20261013/";

    [MenuItem("Tools/USW/UI/Create Ingame Options Prefab And Install")]
    private static void CreateMenu() => Debug.Log(CreateAndInstall());

    /// <summary>Creates the bundle once. Refuses to overwrite hand-edited prefabs or dirty scenes.</summary>
    public static string CreateAndInstall()
    {
        RequireSavedEditor();
        if (File.Exists(PrefabPath)) throw new InvalidOperationException("Options prefab already exists; edit the prefab directly. Creation never overwrites it.");
        Directory.CreateDirectory(Evidence + "before");
        foreach (var path in new[] { IngamePath, TutorialPath })
            if (!File.Exists(Evidence + "before/" + Path.GetFileName(path)))
                File.Copy(path, Evidence + "before/" + Path.GetFileName(path));
        var source = EditorSceneManager.OpenPreviewScene(IngamePath);
        try { Build(source); }
        finally { EditorSceneManager.ClosePreviewScene(source); }
        return InstallExisting();
    }

    /// <summary>Installs the saved prefab without rebuilding or overwriting its authored contents.</summary>
    public static string InstallExisting()
    {
        RequireSavedEditor();
        if (!File.Exists(PrefabPath)) throw new InvalidOperationException("Create the options prefab first.");
        Install(IngamePath);
        Install(TutorialPath);
        return "Installed in both saved scenes; TutorialScene remains open for review.";
    }

    /// <summary>Installs only the gameplay scene and leaves it open for inspection.</summary>
    public static void InstallIngameScene() { RequireSavedEditor(); Install(IngamePath); }

    /// <summary>Installs only the tutorial scene and leaves it open for inspection.</summary>
    public static void InstallTutorialScene() { RequireSavedEditor(); Install(TutorialPath); }

    /// <summary>Reads the original unsafe iterator's pending writes without applying them.</summary>
    public static string AuditProtectedReferences()
    {
        var scene = EditorSceneManager.OpenPreviewScene(IngamePath);
        try
        {
            var menu = Components<IngameHudMenu>(scene).Single();
            var owned = menu.GetComponentsInChildren<Component>(true).Where(c=>c!=null).ToArray();
            int ownershipWrites = 0;
            foreach(var component in owned)
            {
                if(component is Transform) continue;
                var p = new SerializedObject(component).GetIterator();
                while(p.Next(true))
                    if(p.propertyPath == "m_GameObject" && p.propertyType == SerializedPropertyType.ObjectReference &&
                        p.objectReferenceValue is GameObject go && go.transform.IsChildOf(menu.transform)) ownershipWrites++;
            }
            return "Original Next(true) remapper would rewrite " + ownershipWrites + " native m_GameObject ownership references. Corrected remapper skips protected fields and removed components.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Build(Scene scene)
    {
        var oldMenu = Components<IngameHudMenu>(scene).Single();
        var menuFields = new SerializedObject(oldMenu);
        var oldDismiss = ((Button)menuFields.FindProperty("_dismissButton").objectReferenceValue).gameObject;
        var oldBackdrop = ((Image)menuFields.FindProperty("_backdrop").objectReferenceValue).gameObject;
        var oldButtons = (RectTransform)oldMenu.transform.parent;
        var oldBar = (RectTransform)oldButtons.parent;
        var oldSettings = Components<UI_SettingPanel_InGame>(scene).Single();
        // A valid parent canvas is required while authoring nested Canvas overrides.
        // Otherwise Unity treats them as root canvases and clears their sorting/layout.
        var authoringCanvas = new GameObject("OptionsAuthoringCanvas", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(authoringCanvas, scene);
        authoringCanvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)authoringCanvas.transform).sizeDelta = new Vector2(1080f, 1920f);
        var root = new GameObject("IngameOptionsMenu", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.SetParent(authoringCanvas.transform, false);
        Stretch((RectTransform)root.transform);
        try
        {
            var bar = NewRect(oldBar.name, root.transform); CopyRect(oldBar, bar);
            var buttons = NewRect(oldButtons.name, bar); CopyRect(oldButtons, buttons);
            var map = new Dictionary<Object, Object>();
            foreach (var original in new[] { oldDismiss, oldBackdrop, oldMenu.gameObject })
            {
                var copy = Object.Instantiate(original, buttons, false); copy.name = original.name;
                MapTree(original.transform, copy.transform, map);
            }
            var menu = root.GetComponentInChildren<IngameHudMenu>(true);
            menu.gameObject.SetActive(true);
            var audio = buttons.gameObject.AddComponent<BottomHudAudioScope>();
            var audioFields = new SerializedObject(audio);
            var audioButtons = audioFields.FindProperty("_buttons"); audioButtons.arraySize = 1;
            audioButtons.GetArrayElementAtIndex(0).objectReferenceValue = menu.GetComponent<Button>();
            audioFields.ApplyModifiedPropertiesWithoutUndo();

            var settingsLayer = NewRect("SettingsLayer", root.transform); Stretch(settingsLayer);
            var settingsCanvas = settingsLayer.gameObject.AddComponent<Canvas>();
            settingsCanvas.overrideSorting = true; settingsCanvas.sortingOrder = 4;
            settingsLayer.gameObject.AddComponent<GraphicRaycaster>();
            var settingsObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath), scene);
            settingsObject.transform.SetParent(settingsLayer, false); settingsObject.SetActive(false);
            var settings = settingsObject.GetComponent<UI_SettingPanel_InGame>();
            MapTree(oldSettings.transform, settings.transform, map);
            Remap(root, map);
            SetReference(menu, "_settingsPanel", settings);

            var inventoryObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPath), scene);
            inventoryObject.transform.SetParent(root.transform, false);
            Stretch((RectTransform)inventoryObject.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(inventoryObject.transform);
            var inventory = inventoryObject.GetComponent<SelectionInventoryUI>();
            SetReference(inventory, "_menu", menu);
            inventoryObject.GetComponent<Canvas>().overrideSorting = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(inventoryObject.GetComponent<Canvas>());
            ValidateInternalReferences(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(authoringCanvas); }
    }

    private static void Install(string path)
    {
        var scene = EditorSceneManager.OpenScene(path);
        if (Components<SelectionInventoryUI>(scene).Any()) throw new InvalidOperationException("Options already installed in " + path);
        var oldMenu = Components<IngameHudMenu>(scene).Single();
        var menuActive = oldMenu.gameObject.activeSelf;
        var fields = new SerializedObject(oldMenu);
        var oldDismiss = ((Button)fields.FindProperty("_dismissButton").objectReferenceValue).gameObject;
        var oldBackdrop = ((Image)fields.FindProperty("_backdrop").objectReferenceValue).gameObject;
        var oldSettings = Components<UI_SettingPanel_InGame>(scene).Single();
        var safeArea = oldMenu.transform.parent.parent.parent;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        instance.transform.SetParent(safeArea, false); Stretch((RectTransform)instance.transform);
        var menu = instance.GetComponentInChildren<IngameHudMenu>(true);
        var settings = instance.GetComponentInChildren<UI_SettingPanel_InGame>(true);
        menu.gameObject.SetActive(menuActive);
        PrefabUtility.RecordPrefabInstancePropertyModifications(menu.gameObject);
        var map = new Dictionary<Object, Object>();
        MapTree(oldMenu.transform, menu.transform, map);
        MapTree(oldDismiss.transform, ((Button)new SerializedObject(menu).FindProperty("_dismissButton").objectReferenceValue).transform, map);
        MapTree(oldBackdrop.transform, ((Image)new SerializedObject(menu).FindProperty("_backdrop").objectReferenceValue).transform, map);
        MapTree(oldSettings.transform, settings.transform, map);
        foreach (var root in scene.GetRootGameObjects()) Remap(root, map);
        Object.DestroyImmediate(oldMenu.gameObject);
        Object.DestroyImmediate(oldDismiss);
        Object.DestroyImmediate(oldBackdrop);
        Object.DestroyImmediate(oldSettings.gameObject);
        var inventory = instance.GetComponentInChildren<SelectionInventoryUI>(true);
        SetReference(inventory, "_infoPanel", Components<GaeGGUL.UI.Unit.UI_UnitInfoPanel>(scene).Single());
        if (Components<IngameHudMenu>(scene).Count() != 1 || Components<IngameDamageChart>(scene).Count() != 1 ||
            Components<SelectionInventoryUI>(scene).Count() != 1 || Components<UI_SettingPanel_InGame>(scene).Count() != 1)
            throw new InvalidOperationException("Duplicate or missing options components in " + path);
        if (path == TutorialPath)
        {
            var director = Components<IngameTutorialDirector>(scene).Single();
            if (new SerializedObject(director).FindProperty("_optionsButton").objectReferenceValue != menu.gameObject)
                throw new InvalidOperationException("Tutorial visibility binding was not preserved.");
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    private static void MapTree(Transform from, Transform to, Dictionary<Object, Object> map)
    {
        map[from.gameObject] = to.gameObject;
        var candidates = to.GetComponents<Component>();
        foreach (var component in from.GetComponents<Component>())
        {
            if (component == null) continue;
            var match = candidates.FirstOrDefault(c => c != null && c.GetType() == component.GetType());
            if (match != null) map[component] = match;
        }
        foreach (Transform child in from)
        {
            var match = to.Find(child.name);
            if (match != null) MapTree(child, match, map);
        }
    }

    private static void Remap(GameObject root, Dictionary<Object, Object> map)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            // Removed objects must retain their native ownership and graph until DestroyImmediate.
            if (component == null || component is Transform || map.ContainsKey(component)) continue;
            var owner = component.gameObject;
            var so = new SerializedObject(component); var p = so.GetIterator(); bool changed = false;
            while (p.NextVisible(true))
                if (!IsProtectedReference(p.propertyPath) && p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null && map.TryGetValue(p.objectReferenceValue, out var replacement))
                { p.objectReferenceValue = replacement; changed = true; }
            if (!changed) continue;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (component.gameObject != owner) throw new InvalidOperationException("Component ownership changed during UI reference remapping.");
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    private static bool IsProtectedReference(string path)
        => path == "m_GameObject" || path == "m_Script" || path == "m_CorrespondingSourceObject" ||
           path == "m_PrefabInstance" || path == "m_PrefabAsset";

    private static void ValidateInternalReferences(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) throw new InvalidOperationException("Missing script in options bundle.");
            if (component is Transform) continue;
            var p = new SerializedObject(component).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null || EditorUtility.IsPersistent(p.objectReferenceValue)) continue;
                var t = p.objectReferenceValue is Component c ? c.transform : (p.objectReferenceValue as GameObject)?.transform;
                if (t != null && t != root.transform && !t.IsChildOf(root.transform))
                    throw new InvalidOperationException(component.name + "." + p.propertyPath + " points outside the prefab: " + t.name);
            }
        }
    }

    private static void SetReference(Object target, string field, Object reference)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = reference; so.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }

    private static void CopyRect(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot;
        to.sizeDelta = from.sizeDelta; to.anchoredPosition3D = from.anchoredPosition3D;
        to.localScale = from.localScale; to.localRotation = from.localRotation;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }

    private static IEnumerable<T> Components<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    private static void RequireSavedEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scenes first.");
    }
}
