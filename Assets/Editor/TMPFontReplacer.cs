using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class TMPFontReplacer : EditorWindow
{
    private TMP_FontAsset targetFont;

    [MenuItem("Tools/TMP Font Replacer")]
    public static void ShowWindow()
    {
        GetWindow<TMPFontReplacer>("TMP Font Replacer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Replace All TMP Fonts", EditorStyles.boldLabel);

        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font", targetFont, typeof(TMP_FontAsset), false);

        if (targetFont == null)
        {
            EditorGUILayout.HelpBox("Please select a target font to enable replace buttons.", MessageType.Info);
            GUI.enabled = false;
        }

        if (GUILayout.Button("Replace Fonts in Current Scene"))
        {
            ReplaceInCurrentScene();
        }

        if (GUILayout.Button("Replace Fonts in All Scenes (Build Settings)"))
        {
            ReplaceInAllBuildScenes();
        }
        
        if (GUILayout.Button("Replace Fonts in All Scenes (Project)"))
        {
            ReplaceInAllProjectScenes();
        }
        
        if (GUILayout.Button("Replace Fonts in Project Prefabs"))
        {
            ReplaceInAllPrefabs();
        }

        GUI.enabled = true;
    }

    private void ReplaceInCurrentScene()
    {
        int count = ReplaceFontInScene(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Success", $"Replaced {count} TMP texts in the current scene.", "OK");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private void ReplaceInAllBuildScenes()
    {
        if (!EditorUtility.DisplayDialog("Warning", "This will modify and save all scenes in the build settings. Make sure you have a backup. Proceed?", "Yes", "No"))
        {
            return;
        }

        int totalCount = 0;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled) continue;

            Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            int count = ReplaceFontInScene(scene);
            totalCount += count;

            if (count > 0)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        EditorUtility.DisplayDialog("Success", $"Replaced a total of {totalCount} TMP texts in all build scenes.", "OK");
    }
    
    private void ReplaceInAllProjectScenes()
    {
        if (!EditorUtility.DisplayDialog("Warning", "This will modify and save ALL scenes in the project. Make sure you have a backup. Proceed?", "Yes", "No"))
        {
            return;
        }

        int totalCount = 0;
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            EditorUtility.DisplayProgressBar("Updating Scenes", $"Processing {path}", (float)i / sceneGuids.Length);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int count = ReplaceFontInScene(scene);
            totalCount += count;

            if (count > 0)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }
        
        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("Success", $"Replaced a total of {totalCount} TMP texts in all project scenes.", "OK");
    }

    private int ReplaceFontInScene(Scene scene)
    {
        int count = 0;
        GameObject[] rootObjects = scene.GetRootGameObjects();

        foreach (GameObject rootObj in rootObjects)
        {
            TMP_Text[] texts = rootObj.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.font != targetFont)
                {
                    Undo.RecordObject(text, "Change TMP Font");
                    text.font = targetFont;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    count++;
                }
            }
        }

        return count;
    }
    
    private void ReplaceInAllPrefabs()
    {
        if (!EditorUtility.DisplayDialog("Warning", "This will modify all prefabs with TMP texts. Make sure you have a backup. Proceed?", "Yes", "No"))
        {
            return;
        }

        int count = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            EditorUtility.DisplayProgressBar("Updating Prefabs", $"Processing {path}", (float)i / prefabGuids.Length);
            
            try
            {
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(path);
                if (prefabContents == null) continue;

                TMP_Text[] texts = prefabContents.GetComponentsInChildren<TMP_Text>(true);
                bool modified = false;

                foreach (TMP_Text text in texts)
                {
                    if (text.font != targetFont)
                    {
                        text.font = targetFont;
                        modified = true;
                        count++;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
                }
                
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to process prefab at {path}: {e.Message}");
            }
        }
        
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success", $"Replaced {count} TMP texts in project prefabs.", "OK");
    }
}
