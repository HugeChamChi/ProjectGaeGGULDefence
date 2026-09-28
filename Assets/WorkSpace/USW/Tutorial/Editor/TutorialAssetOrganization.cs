using System;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Moves tutorial-owned assets through Unity, preserving GUIDs and authored references.</summary>
public static class TutorialAssetOrganization
{
    /// <summary>Organizes only this feature; the existing gacha tutorial stays in HSD.</summary>
    [MenuItem("Tools/USW/Tutorial/Organize Tutorial Assets")]
    public static void Organize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before organizing assets.");
        const string root = "Assets/WorkSpace/USW/Tutorial";
        Folder(root + "/Scripts"); Folder(root + "/Editor"); Folder(root + "/Scenes");
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var name in new[] { "IngameTutorialDirector", "IngameTutorialDialogue", "IngameTutorialOverlay", "IngameTutorialSettings", "IngameTutorialStage", "IngameTutorialStep" })
                Move("Assets/WorkSpace/HSD/Scripts/Tutorial/" + name + ".cs", root + "/Scripts/" + name + ".cs");
            foreach (var name in new[] { "IngameTutorialSetup", "IngameTutorialChecks" })
                Move("Assets/WorkSpace/USW/Scripts/Editor/" + name + ".cs", root + "/Editor/" + name + ".cs");
            Move("Assets/WorkSpace/HSD/Data/IngameTutorial", root + "/Data");
            Move("Assets/WorkSpace/USW/TutorialScene.unity", root + "/Scenes/TutorialScene.unity");
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.SaveAssets();
    }

    private static void Move(string from, string to)
    {
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(from))) return;
        var error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
