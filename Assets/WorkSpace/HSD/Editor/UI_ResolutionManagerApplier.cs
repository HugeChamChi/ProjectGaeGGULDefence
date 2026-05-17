using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

public class UI_ResolutionManagerApplier : EditorWindow
{
    [MenuItem("Tools/Apply UI Resolution Manager to All")]
    public static void ApplyToAll()
    {
        int sceneCount = 0;
        int prefabCount = 0;

        // 1. 현재 열려있는 씬에서 처리
        CanvasScaler[] scalersInScene = Object.FindObjectsOfType<CanvasScaler>(true);
        foreach (var scaler in scalersInScene)
        {
            if (ApplyToGameObject(scaler.gameObject))
            {
                sceneCount++;
                EditorSceneManager.MarkSceneDirty(scaler.gameObject.scene);
            }
        }

        // 2. 프로젝트 내 모든 프리팹에서 처리
        string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in allPrefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            CanvasScaler[] scalersInPrefab = prefab.GetComponentsInChildren<CanvasScaler>(true);
            bool modified = false;

            foreach (var scaler in scalersInPrefab)
            {
                if (AddResolutionManager(scaler.gameObject))
                {
                    modified = true;
                    prefabCount++;
                }
            }

            if (modified)
            {
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("완료", 
            $"적용 완료!\n씬 내 오브젝트: {sceneCount}개\n프리팹: {prefabCount}개", "확인");
    }

    private static bool ApplyToGameObject(GameObject go)
    {
        return AddResolutionManager(go);
    }

    private static bool AddResolutionManager(GameObject go)
    {
        if (go.GetComponent<UI_RootScaler>() == null)
        {
            Undo.AddComponent<UI_RootScaler>(go);
            Debug.Log($"[UI_ResolutionManager] Added to {go.name}", go);
            return true;
        }
        return false;
    }
}
