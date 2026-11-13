using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Opens the offline encounter while preserving the project's prior Play start scene.</summary>
[InitializeOnLoad]
public static class DeertreeBossLabMenu
{
    private const string ScenePath = "Assets/WorkSpace/USW/DeertreeBoss/Lab/BossLab_Deertree.unity";
    private const string Key = "DeertreeLab.PriorStart";
    static DeertreeBossLabMenu() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

    /// <summary>Runs only the Deertree lab, independent of the game's boot scene override.</summary>
    [MenuItem("Tools/Boss Lab/Play Deertree")]
    public static void Play()
    {
        if (EditorApplication.isPlaying) return;
        if (SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // This one scene owns the exception, including the normal toolbar Play button.
        if (state == PlayModeStateChange.ExitingEditMode && SceneManager.GetActiveScene().path == ScenePath)
        {
            if (!SessionState.GetBool(Key + ".Active", false))
                SessionState.SetString(Key, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key + ".Active", true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            return;
        }
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key + ".Active", false)) return;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key, ""));
        SessionState.SetBool(Key + ".Active", false);
    }
}
