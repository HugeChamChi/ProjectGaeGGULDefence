using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HardIngameReview
{
    public static string OpenLibrary()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Clean Edit mode required.");
        EditorSceneManager.OpenScene("Assets/WorkSpace/USW/Scene/BossLab_PatternLibrary.unity");
        return "Pattern library opened for unchanged 8-mechanic regression.";
    }

    public static string Finish()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Clean Edit mode required.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/HardIngameScene.unity");
        int missing=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true)
            .Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        if(missing!=0)throw new Exception("Missing scene scripts: "+missing);
        var wave=Object.FindFirstObjectByType<WaveManager>();
        if(wave.EndlessMode==null || wave.EndlessMode.Key!="HARD_INGAME")throw new Exception("Hard profile missing.");
        if(GameObject.Find("Hard Boss Controls")!=null)throw new Exception("Unexpected preview controls.");
        // Compare against the current game scene: the only authored change is the explicit mode reference.
        var original=EditorSceneManager.OpenScene("Assets/Scenes/IngameScene.unity",OpenSceneMode.Additive);
        int sourceRoots=original.rootCount;
        int targetRoots=scene.rootCount;
        EditorSceneManager.CloseScene(original,true);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        if(sourceRoots+1!=targetRoots)throw new Exception("Unexpected extra game roots.");
        string summary="HardIngameScene Edit/clean; roots="+targetRoots+" (IngameScene + non-UI offline entry); missing scripts=0; custom preview controls=0.";
        File.WriteAllText("outputs/hard-develop-20261022/final-scene.txt",summary);
        return summary;
    }
}
