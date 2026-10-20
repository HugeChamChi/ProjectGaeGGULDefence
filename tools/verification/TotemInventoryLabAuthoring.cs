using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>Creates only the isolated totem interaction scene and its presentation settings.</summary>
public static class TotemInventoryLabAuthoring
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_TotemInventory.unity";
    private const string SettingsPath = "Assets/WorkSpace/USW/Scene/FxLab_TotemInventorySettings.asset";

    /// <summary>Authors a saved scene through Unity APIs, refusing to replace an existing scene.</summary>
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Requires Edit Mode.");
        var current = SceneManager.GetActiveScene();
        if (current.isDirty) throw new Exception("Current scene has unsaved edits: " + current.path);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new Exception("Lab already exists; open it rather than overwriting manual edits.");
        var settings = AssetDatabase.LoadAssetAtPath<TotemInventoryLabSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<TotemInventoryLabSettings>();
            settings.Samples = new[] { 1001, 1005, 1006, 1008, 1009 }.Select(id =>
                AssetDatabase.LoadAssetAtPath<TotemData>($"Assets/WorkSpace/USW/Data/TotemData/Playable/TD{id}Data.asset")).ToArray();
            if (settings.Samples.Any(data => data == null || data.DisplaySprite == null))
                throw new Exception("An authored sample is missing data or a display sprite.");
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Imports/Font/NotoSansKR-Medium SDF.asset");
        if (font == null) throw new Exception("Korean font missing.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 8;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.075f, .12f, .13f);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var lab = new GameObject("Totem Inventory Interaction Lab").AddComponent<TotemInventoryLab>();
        var background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_Remake_001.png");
        lab.BuildPreview(settings, font, background);
        // This project redirects direct Play to BootScene. The lab must enter independently.
        EditorSceneManager.playModeStartScene = null;
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Could not save the lab scene.");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = lab.gameObject;
        return ScenePath + " | five authored samples | saved clean | no live game dependencies";
    }

    /// <summary>Reports the active scene without changing it.</summary>
    public static string Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        return $"{scene.path} | dirty={scene.isDirty} | play={EditorApplication.isPlaying}";
    }

    /// <summary>Removes only this task's accidentally imported screenshot folder after preserving the image outside Assets.</summary>
    public static string CleanupEvidence()
    {
        const string source = "Assets/outputs/totem-inventory-lab-20261020";
        const string destination = "outputs/totem-inventory-lab-20261020";
        if (!AssetDatabase.IsValidFolder(source)) return "No imported evidence to clean";
        System.IO.Directory.CreateDirectory(destination);
        System.IO.File.Copy(source + "/initial.png", destination + "/initial.png", true);
        if (!AssetDatabase.DeleteAsset(source)) throw new Exception("Evidence cleanup failed");
        return "Preserved initial.png outside Assets; removed only the task's imported evidence folder";
    }
}
