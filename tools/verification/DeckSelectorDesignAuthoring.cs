using System;
using System.IO;
using System.Security.Cryptography;
using GaeGGUL.DeckUpgrade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class DeckSelectorDesignAuthoring
{
    public const string Root = "Assets/WorkSpace/USW/DeckUpgrade";
    public static readonly string[] Names = { "UnitUpgrade_01_Clean", "UnitUpgrade_02_BoldFrame", "UnitUpgrade_03_Flat" };
    public static string Build()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and save pending changes.");
        string sceneHash = Hash(Root + "/UnitUpgradeUI.unity"), prefabHash = Hash(Root + "/Prefabs/UnitUpgradeUI.prefab");
        if (!AssetDatabase.IsValidFolder(Root + "/Concepts")) AssetDatabase.CreateFolder(Root, "Concepts");
        string[] oldNames = { "UnitUpgrade_01_Fleet", "UnitUpgrade_02_Casual", "UnitUpgrade_03_Arcade" };
        for (int i = 0; i < Names.Length; i++)
            if (AssetDatabase.LoadMainAssetAtPath(Root + "/Concepts/" + oldNames[i] + ".unity") != null)
                AssetDatabase.MoveAsset(Root + "/Concepts/" + oldNames[i] + ".unity", Root + "/Concepts/" + Names[i] + ".unity");
        for (int i = 0; i < 1; i++)
        {
            var scene = EditorSceneManager.OpenScene(Root + "/UnitUpgradeUI.unity");
            EditorSceneManager.SaveScene(scene, Root + "/Concepts/" + Names[i] + ".unity", true);
            scene = EditorSceneManager.OpenScene(Root + "/Concepts/" + Names[i] + ".unity");
            var selector = UnityEngine.Object.FindFirstObjectByType<DeckSelectorConcept>();
            PrefabUtility.UnpackPrefabInstance(selector.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var design = selector.gameObject.AddComponent<DeckUpgradeDesignVariant>(); design.variant = i + 1; design.lobbyTopTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Concepts/LobbyTopPreview.prefab");
            selector.screen.BuildView();
            EditorSceneManager.SaveScene(scene);
        }
        if (!AssetDatabase.IsValidFolder(Root + "/Legacy/DesignTrials")) AssetDatabase.CreateFolder(Root + "/Legacy", "DesignTrials");
        for (int i = 1; i < Names.Length; i++)
        {
            string source = Root + "/Concepts/" + Names[i] + ".unity";
            if (AssetDatabase.LoadMainAssetAtPath(source) != null)
            {
                string error = AssetDatabase.MoveAsset(source, Root + "/Legacy/DesignTrials/" + Names[i] + ".unity");
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            }
        }
        EditorSceneManager.OpenScene(Root + "/Concepts/" + Names[0] + ".unity");
        if (sceneHash != Hash(Root + "/UnitUpgradeUI.unity") || prefabHash != Hash(Root + "/Prefabs/UnitUpgradeUI.prefab")) throw new Exception("Current UI must remain unchanged.");
        var output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/unit-upgrade-designs"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "original-hashes.txt"), sceneHash + "\n" + prefabHash);
        return "Kept one clean design scene; archived other trials; original scene and prefab hashes unchanged.";
    }
    public static string Open01() { return Open(0); }
    public static string Open02() { return Open(1); }
    public static string Open03() { return Open(2); }
    private static string Open(int index)
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first.");
        EditorSceneManager.OpenScene(Root + "/Concepts/" + Names[index] + ".unity"); return Names[index];
    }
    private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
}
