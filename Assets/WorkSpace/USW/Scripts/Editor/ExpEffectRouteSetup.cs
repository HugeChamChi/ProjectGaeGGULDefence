using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>기존 씬 컨트롤러에 Canvas와 실제 경험치 UI를 연결한다.</summary>
public static class ExpEffectRouteSetup
{
    private const string PrefabPath = "Assets/WorkSpace/HSD/Prefab/UI/Effect/ExpEffect.prefab";

    /// <summary>기존 그림/잔상을 유지하면서 경로 추적용 단발 파티클로 설정한다.</summary>
    [MenuItem("Tools/EXP Effect/Prepare ExpEffect Prefab")]
    public static void ConfigurePrefab()
    {
        if (Application.isPlaying) return;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            AssetKits.ParticleImage.ParticleImage particle = root.GetComponent<AssetKits.ParticleImage.ParticleImage>();
            particle.shape = AssetKits.ParticleImage.Enumerations.EmitterShape.Point;
            particle.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            var size = particle.startSize;
            size.mainCurve = new ParticleSystem.MinMaxCurve(25f, 35f);
            size.separated = false;
            particle.startSize = size;
            particle.gravityEnabled = particle.velocityEnabled = particle.noiseEnabled = particle.vortexEnabled = false;
            particle.attractorEnabled = true;
            particle.attractorLerp = new ParticleSystem.MinMaxCurve(1f);
            particle.loop = false;
            // 단발 한 개의 연출에는 개별 Job 스케줄링보다 기본 시뮬레이션이 단순하다.
            particle.multithreadEnabled = false;
            particle.duration = 0.05f;
            particle.lifetime = new ParticleSystem.MinMaxCurve(1.2f);
            particle.rateOverTime = particle.rateOverLifetime = particle.rateOverDistance = 0f;
            SerializedObject data = new SerializedObject(particle);
            SerializedProperty bursts = data.FindProperty("_bursts");
            bursts.ClearArray();
            bursts.InsertArrayElementAtIndex(0);
            bursts.GetArrayElementAtIndex(0).FindPropertyRelative("time").floatValue = 0f;
            bursts.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 1;
            bursts.GetArrayElementAtIndex(0).FindPropertyRelative("used").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            particle.raycastTarget = false;
            particle.maskable = false;
            if (particle.particleTrailRenderer != null)
            {
                particle.particleTrailRenderer.raycastTarget = false;
                particle.particleTrailRenderer.maskable = false;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>열린 씬의 경험치 경로를 연결한다. 씬 저장은 사용자가 수행한다.</summary>
    [MenuItem("Tools/EXP Effect/Connect Routes In Open Scenes")]
    public static void ConfigureOpenScenes()
    {
        foreach (ExpEffectController controller in Object.FindObjectsByType<ExpEffectController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (controller.GetComponentInParent<Canvas>() != null) Configure(controller);
    }

    /// <summary>선택한 컨트롤러의 기본 참조를 Undo 가능하게 연결한다.</summary>
    public static void Configure(ExpEffectController controller)
    {
        if (Application.isPlaying) return;
        Undo.RecordObject(controller, "Connect EXP route");
        controller.ResolveCanvas();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("particleImage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AssetKits.ParticleImage.ParticleImage>(PrefabPath);
        if (controller.SpawnPoint == null)
            serialized.FindProperty("spawnPoint").objectReferenceValue = controller.transform;
        ExpBarUI match = null;
        int matches = 0;
        foreach (ExpBarUI bar in Object.FindObjectsByType<ExpBarUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bar.gameObject.scene != controller.gameObject.scene) continue;
            Canvas canvas = bar.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.transform != controller.RouteCanvas) continue;
            match = bar;
            matches++;
        }
        if (controller.ArrivalTarget == null && matches == 1)
        {
            // 이미 지정된 도착점은 보존하고, 누락된 경우에만 실제 EXP UI에 연결한다.
            SerializedObject barData = new SerializedObject(match);
            Component slider = barData.FindProperty("expSlider").objectReferenceValue as Component;
            serialized.FindProperty("attractorTarget").objectReferenceValue = slider != null ? slider.transform : match.transform;
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Selection.activeGameObject = controller.gameObject;
        SceneView.RepaintAll();
        if (controller.ArrivalTarget == null)
            Debug.LogWarning("[EXP Route] 이 씬에 유일한 ExpBarUI가 없습니다. Attractor Target에 경험치 도착 UI를 직접 연결하세요.", controller);
    }
}
