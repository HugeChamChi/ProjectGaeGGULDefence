using System;
using System.IO;
using GaeGGUL.DeckUpgrade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
public static class DeckSelectorPrefabAuthoring
{
    private const string Root = "Assets/WorkSpace/USW/DeckUpgrade";
    public const string Scene = Root + "/UnitUpgradeUI.unity";
    public const string Prefab = Root + "/Prefabs/UnitUpgradeUI.prefab";
    public static string Export()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Current scene has unsaved edits.");
        Folder(Root, "Legacy"); Folder(Root, "Prefabs");
        Move(Root + "/DroneUpgradeConcept2.unity", Root + "/Legacy/DroneUpgradeConcept2.unity");
        Move(Root + "/README.md", Root + "/Legacy/DroneUpgradeConcept2.md");
        Move(Root + "/DeckSelectorConcept.unity", Scene);
        EditorSceneManager.OpenScene(Scene);
        var view = UnityEngine.Object.FindFirstObjectByType<DeckSelectorConcept>();
        view.gameObject.name = "UnitUpgradeUI";
        if (PrefabUtility.IsPartOfPrefabInstance(view.gameObject))
            PrefabUtility.UnpackPrefabInstance(view.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var saved = PrefabUtility.SaveAsPrefabAsset(view.gameObject, Prefab);
        if (saved == null || PrefabUtility.GetPrefabAssetType(saved) != PrefabAssetType.Regular)
            throw new Exception("A regular prefab must be created.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), scene);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorSceneManager.SaveScene(scene, Scene);
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        return "Created prefab and clean prefab-instance scene; archived old scene preserving GUIDs.";
    }
    private static void Folder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }
    private static void Move(string source, string target)
    {
        if (AssetDatabase.LoadMainAssetAtPath(source) == null || AssetDatabase.LoadMainAssetAtPath(target) != null) return;
        string error = AssetDatabase.MoveAsset(source, target); if (!string.IsNullOrEmpty(error)) throw new Exception(error);
    }
}
