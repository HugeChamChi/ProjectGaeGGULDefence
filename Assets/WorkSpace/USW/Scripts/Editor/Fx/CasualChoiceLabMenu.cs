using UnityEditor;
using UnityEditor.SceneManagement;

public static class CasualChoiceLabMenu
{
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_CasualChoice.unity";

    [MenuItem("Tools/USW/Fx/Open Casual Choice Lab")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
    }
}
