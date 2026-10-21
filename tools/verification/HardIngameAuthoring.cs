using System;
using System.Linq;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Authors Crocus random mechanics and an ordinary in-game scene with only hard bosses.</summary>
public static class HardIngameAuthoring
{
    public const string ScenePath = "Assets/Scenes/HardIngameScene.unity";
    public const string Folder = "Assets/WorkSpace/USW/Data/BossPatterns/Hard";

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop play and preserve the current scene first.");
        EnsureFolder(Folder);
        string prefabPath = "Assets/WorkSpace/USW/Prefab/BossUnit/Crocus.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var skeletonData = prefab.GetComponentInChildren<SkeletonAnimation>(true).SkeletonDataAsset.GetSkeletonData(true);
        var patterns = new BossPatternData[6];
        patterns[0] = AssetDatabase.LoadAssetAtPath<BossPatternData>("Assets/WorkSpace/USW/Data/BossData/Crocodile/CrocodileEarthquake.asset");
        var catalog = AssetDatabase.LoadAssetAtPath<PatternLabCatalog>("Assets/WorkSpace/USW/Data/BossPatterns/Library/PatternCatalog.asset");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/WorkSpace/USW/Data/BossPatterns/Hard/Telegraph.mat");
        if (catalog == null || catalog.Patterns.Length != 8 || material == null)
            throw new InvalidOperationException("Eight authored placement mechanics and material are required.");
        var phases = Asset<PlacementBossPhaseData>(Folder + "/CrocusHpPhases.asset");
        phases.Thresholds = new[] { .7f, .3f };
        EditorUtility.SetDirty(phases);
        for (int i = 1; i < patterns.Length; i++)
        {
            var animation = skeletonData.FindAnimation("skill_" + (i + 1));
            if (animation == null) throw new InvalidOperationException("Missing Crocus skill " + (i + 1));
            var data = Asset<PlacementBossPatternData>(Folder + "/CrocusSkill" + (i + 1) + ".asset");
            data.Candidates = catalog.Patterns.ToArray();
            data.Phases = phases;
            data.TelegraphMaterial = material;
            data.NativeDuration = animation.Duration;
            data.patternType = BossPatternType.PlacementMechanic;
            data.patternName = "배치 기믹";
            data.description = "8가지 배치 기믹 중 하나를 무작위로 시전합니다.";
            data.AnimationTrigger = "CrocusSkill" + (i + 1);
            data.triggerType = PatternTriggerType.Phase;
            data.interval = 0f;
            data.Priority = 10;
            data.UseScreenShake = false;
            EditorUtility.SetDirty(data);
            patterns[i] = data;
        }
        var editable = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var so = new SerializedObject(editable.GetComponent<BossBase>());
            var array = so.FindProperty("_patterns");
            array.arraySize = patterns.Length;
            for (int i = 0; i < patterns.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = patterns[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            so = new SerializedObject(editable.GetComponent<BossSpineAnimator>());
            array = so.FindProperty("_patternAnimations");
            array.arraySize = patterns.Length;
            for (int i = 0; i < patterns.Length; i++)
            {
                var mapping = array.GetArrayElementAtIndex(i);
                mapping.FindPropertyRelative("Trigger").stringValue = patterns[i].AnimationTrigger;
                mapping.FindPropertyRelative("Animation").stringValue = "skill_" + (i + 1);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(editable, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(editable); }

        // Local authored profile: existing endless combat, rewards, penalties and growth, with two hard slots.
        var mode = Asset<EndlessModeData>(Folder + "/HardIngameMode.asset");
        EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<EndlessModeData>("Assets/WorkSpace/USW/Data/Endless/Modes/NORMAL.asset"), mode);
        mode.name = "HardIngameMode";
        var modeSo = new SerializedObject(mode);
        modeSo.FindProperty("_key").stringValue = "HARD_INGAME";
        modeSo.FindProperty("_enabled").boolValue = true;
        modeSo.FindProperty("_cycleLength").intValue = 2;
        var progression = modeSo.FindProperty("_bossProgression");
        if (progression != null) progression.objectReferenceValue = null;
        modeSo.FindProperty("_bossSelection").enumValueIndex = (int)EndlessBossSelection.FixedCycle;
        var slots = modeSo.FindProperty("_slots");
        slots.arraySize = 2;
        string[] bossPaths = {
            "Assets/WorkSpace/USW/Data/BossData/Crocodile/CrocodilePrototypeData.asset",
            "Assets/WorkSpace/USW/Data/BossData/Lizard/LizardBossData.asset"
        };
        for (int i = 0; i < slots.arraySize; i++)
        {
            var boss = AssetDatabase.LoadAssetAtPath<BossData>(bossPaths[i]);
            var slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("_position").intValue = i + 1;
            slot.FindPropertyRelative("_role").enumValueIndex = (int)EndlessBossRole.Hard;
            slot.FindPropertyRelative("_bossKey").stringValue = boss.name;
            slot.FindPropertyRelative("_boss").objectReferenceValue = boss;
        }
        modeSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mode);
        AssetDatabase.SaveAssets();
        if (!EndlessConfigurationValidator.TryValidateForRun(mode, out var error)) throw new InvalidOperationException(error);

        // Recreate from the real scene through the Editor API so no preview objects or prefab overrides survive.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/IngameScene.unity");
        mode = AssetDatabase.LoadAssetAtPath<EndlessModeData>(Folder + "/HardIngameMode.asset");
        if (mode == null) throw new InvalidOperationException("Saved hard profile could not be reloaded.");
        var wave = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        var waveSo = new SerializedObject(wave);
        waveSo.FindProperty("_endlessMode").objectReferenceValue = mode;
        waveSo.ApplyModifiedPropertiesWithoutUndo();
        var entry = new GameObject("Hard Ingame Entry").AddComponent<HardIngameEntry>();
        var entrySo = new SerializedObject(entry);
        entrySo.FindProperty("_scope").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<InGameLifetimeScope>();
        entrySo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.DeleteAsset(Folder + "/HardIngameSettings.asset");
        Selection.activeGameObject = wave.gameObject;
        return "Saved " + ScenePath + ": ordinary in-game HUD/start/summon/choices; Crocus and Lizard alternate endlessly. At HP70/30 Crocus selects one skill_2..6, each drawing from all 8 mechanics; Lizard unchanged.";
    }

    public static string Probe()
    {
        var scene = EditorSceneManager.GetActiveScene();
        return scene.path + " dirty=" + scene.isDirty + " roots=" + scene.rootCount;
    }

    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
