using System;
using System.IO;
using System.Security.Cryptography;
using GaeGGUL.DeckUpgrade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeckSelectorConceptAuthoring
{
    public const string Source = "Assets/WorkSpace/USW/DeckUpgrade/Legacy/DroneUpgradeConcept2.unity";
    public const string Target = "Assets/WorkSpace/USW/DeckUpgrade/UnitUpgradeUI.unity";
    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Save the current scene before creating the separate concept.");
        string before = Hash(Source);
        EditorSceneManager.OpenScene(Source);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Target, true);
        var scene = EditorSceneManager.OpenScene(Target);
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        screen.BuildView();
        var selector = screen.gameObject.GetComponent<DeckSelectorConcept>() ?? screen.gameObject.AddComponent<DeckSelectorConcept>();
        selector.screen = screen;
        selector.frogPortrait = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/HeroUnits/Chief_UnitData_Frog_Warrior_Legend.asset").icon;
        selector.attackIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/GGD_ArtWork/KHJ_Artwork/In-game/SPR_UI2500_AttackIcon.png");
        selector.attackSpeedIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/GGD_ArtWork/KHJ_Artwork/In-game/SPR_UI2500_AttackSpeedIcon.png");
        selector.BuildView();
        EditorSceneManager.SaveScene(scene);
        if (Hash(Source) != before) throw new Exception("Original scene changed unexpectedly.");
        var output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/deck-selector"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "original-scene.sha256"), before);
        Selection.activeGameObject = screen.gameObject;
        return "Created three-column unit collection: same-deck units consecutive, chief first, direct detail and owned-only filter. Original SHA256 unchanged: " + before;
    }
    public static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
}
