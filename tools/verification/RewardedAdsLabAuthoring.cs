using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GGD.RewardedAdsLab;

public static class RewardedAdsLabAuthoring
{
    public static string Create()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before authoring.");
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(created);
        new GameObject("Rewarded Ads Lab (NO LIVE ACCOUNT)").AddComponent<RewardedAdsLabScreen>();
        var camera = new GameObject("Lab Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.07f, 0.11f);
        string path = "Assets/WorkSpace/USW/RewardedAdsLab/RewardedAdsLab.unity";
        if (!EditorSceneManager.SaveScene(created, path)) throw new Exception("Scene save failed.");
        EditorSceneManager.CloseScene(created, true);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        if (!SessionState.GetBool("RewardedAdsLab.StartOverride", false))
        {
            SessionState.SetString("RewardedAdsLab.PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool("RewardedAdsLab.StartOverride", true);
        }
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        return "Created " + path + "; isolated Play start scene set; original unsaved scene preserved; not added to build settings.";
    }
    public static string RestoreStartScene()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first.");
        string path = SessionState.GetString("RewardedAdsLab.PreviousStart", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        SessionState.EraseBool("RewardedAdsLab.StartOverride");
        return "Restored Play start scene; original loaded scenes remain preserved.";
    }
}
